using System;
using System.Collections.Generic;
using UnityEngine;

// 신체 뽑기권과 향후 분리 재화를 타입 기반으로 저장하고 지급합니다.
[DefaultExecutionOrder(-9500)]
public sealed class CurrencyWalletManager : Singleton<CurrencyWalletManager>, ISaveDataProvider
{
    private const string ProviderKey = "currency_wallet"; // 통합 저장에서 사용할 안정적인 Provider 키
    private const int CurrentSaveVersion = 1; // Currency Wallet 내부 저장 형식 버전
    [SerializeField, Min(0)] private int startingBodyDrawTickets = 10; // 새 저장이 시작할 기본 신체 뽑기권
    [SerializeField, Min(0)] private int testBodyDrawTickets = 100; // ContextMenu에서 지급할 테스트 뽑기권 수
    private CurrencyWalletState _state = new(); // 종류별 재화 영구 원본
    private readonly Dictionary<GameCurrencyType, GameCurrencyEntry> _entries = new(); // 반복 탐색을 막는 타입별 캐시
    private SaveManager _save; // 기존 통합 저장 서비스
    private bool _ready; // 저장 Provider 등록 완료 여부

    public event Action<GameCurrencyType, long> CurrencyChanged; // 한 재화의 변경 결과 알림
    public static event Action<CurrencyWalletManager> AvailabilityChanged; // 타입형 지갑 생성 및 제거 알림
    public string SaveKey => ProviderKey; // 통합 저장에 노출할 Provider 키
    public Type SaveDataType => typeof(CurrencyWalletState); // 역직렬화할 저장 DTO 형식

    // 씬 배치 없이 타입형 지갑 인스턴스를 준비합니다.
    public static CurrencyWalletManager EnsureInstance()
    {
        if (HasInstance) return Instance;
        return new GameObject(nameof(CurrencyWalletManager)).AddComponent<CurrencyWalletManager>();
    }

    // 첫 씬보다 먼저 신체 뽑기권 저장 원본을 준비합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    // 기존 SaveManager에 독립 지갑 Provider를 등록합니다.
    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        DontDestroyOnLoad(gameObject);
        _save = SaveManager.EnsureInstance();
        _ready = _save.RegisterProvider(this);
        AvailabilityChanged?.Invoke(this);
    }

    // 지정 종류의 현재 보유량을 반환합니다.
    public long GetAmount(GameCurrencyType type)
    {
        EnsureEntryCache();
        return _entries.TryGetValue(type, out GameCurrencyEntry entry) ? Math.Max(0L, entry.amount) : 0L;
    }

    // 외부 보상 시스템이 같은 경로로 양수 재화를 지급합니다.
    public bool AddCurrency(GameCurrencyType type, long amount)
    {
        if (!_ready || amount <= 0L) return false;
        GameCurrencyEntry entry = GetOrCreateEntry(type); // 증가할 실제 지갑 항목
        entry.amount = amount >= long.MaxValue - entry.amount ? long.MaxValue : entry.amount + amount;
        MarkChanged(type, entry.amount);
        return true;
    }

    // 현재 보유량이 충분할 때 지정 재화를 정확히 한 번 차감합니다.
    public bool TrySpendCurrency(GameCurrencyType type, long amount)
    {
        if (!_ready || amount < 0L) return false;
        GameCurrencyEntry entry = GetOrCreateEntry(type); // 차감할 실제 지갑 항목
        if (entry.amount < amount) return false;
        entry.amount -= amount;
        MarkChanged(type, entry.amount);
        return true;
    }

    // Inspector에서 신체 뽑기권 테스트 지급을 실행합니다.
    [ContextMenu("테스트 신체 뽑기권 지급")]
    private void GrantTestBodyDrawTickets()
    {
        AddCurrency(GameCurrencyType.BodyDrawTicket, testBodyDrawTickets);
        if (_save != null) _save.SaveGame();
    }

    // 현재 지갑 원본을 통합 저장에 제공합니다.
    public object CaptureSaveData()
    {
        NormalizeState();
        return _state;
    }

    // 이전 저장에 지갑 구역이 없어도 안전한 기본값으로 복원합니다.
    public void RestoreSaveData(object data)
    {
        CurrencyWalletState restored = data as CurrencyWalletState; // JSON에서 복원한 지갑 DTO
        if (restored == null || restored.version > CurrentSaveVersion) throw new InvalidOperationException("지원하지 않는 Currency Wallet 저장 형식입니다.");
        _state = restored;
        NormalizeState();
        NotifyAllCurrencies();
    }

    // 새 게임은 모든 타입형 재화를 0으로 시작합니다.
    public void ResetSaveData()
    {
        _state = new CurrencyWalletState();
        NormalizeState();
        GetOrCreateEntry(GameCurrencyType.BodyDrawTicket).amount = Math.Max(0, startingBodyDrawTickets);
        NotifyAllCurrencies();
    }

    // 목록 누락, 중복과 음수 값을 안전하게 정리합니다.
    private void NormalizeState()
    {
        if (_state == null) _state = new CurrencyWalletState();
        _state.version = CurrentSaveVersion;
        if (_state.currencies == null) _state.currencies = new List<GameCurrencyEntry>();
        Dictionary<GameCurrencyType, long> totals = new(); // 중복 저장 항목을 합칠 임시 값
        foreach (GameCurrencyEntry entry in _state.currencies) // 복원된 모든 지갑 항목
        {
            if (entry == null || !Enum.IsDefined(typeof(GameCurrencyType), entry.type)) continue;
            long amount = Math.Max(0L, entry.amount); // 음수를 제거한 저장값
            totals[entry.type] = totals.TryGetValue(entry.type, out long previous) && amount >= long.MaxValue - previous ? long.MaxValue : previous + amount;
        }
        _state.currencies.Clear();
        _entries.Clear();
        foreach (GameCurrencyType type in Enum.GetValues(typeof(GameCurrencyType))) // 정의된 모든 재화 타입
        {
            GameCurrencyEntry entry = new() { type = type, amount = totals.TryGetValue(type, out long amount) ? amount : 0L }; // 정규화된 단일 항목
            _state.currencies.Add(entry);
            _entries.Add(type, entry);
        }
    }

    // 캐시가 비어 있으면 현재 저장 상태를 한 번 정규화합니다.
    private void EnsureEntryCache()
    {
        if (_entries.Count == Enum.GetValues(typeof(GameCurrencyType)).Length) return;
        NormalizeState();
    }

    // 지정 타입의 지갑 항목을 캐시에서 반환합니다.
    private GameCurrencyEntry GetOrCreateEntry(GameCurrencyType type)
    {
        EnsureEntryCache();
        if (_entries.TryGetValue(type, out GameCurrencyEntry entry)) return entry;
        entry = new GameCurrencyEntry { type = type };
        _state.currencies.Add(entry);
        _entries[type] = entry;
        return entry;
    }

    // 저장 Dirty와 현재 타입의 UI 이벤트를 함께 알립니다.
    private void MarkChanged(GameCurrencyType type, long amount)
    {
        if (_save != null) _save.MarkDirty();
        CurrencyChanged?.Invoke(type, amount);
    }

    // 복원과 초기화 후 모든 타입의 현재 값을 알립니다.
    private void NotifyAllCurrencies()
    {
        foreach (GameCurrencyType type in Enum.GetValues(typeof(GameCurrencyType))) CurrencyChanged?.Invoke(type, GetAmount(type));
    }

    // 제거될 때 Provider와 정적 생성 알림을 정리합니다.
    protected override void OnDestroy()
    {
        bool wasActiveInstance = HasInstance && ReferenceEquals(Instance, this); // 중복 오브젝트 제거인지 실제 원본 제거인지 구분
        if (_save != null) _save.UnregisterProvider(this);
        if (wasActiveInstance) AvailabilityChanged?.Invoke(null);
        base.OnDestroy();
    }
}
