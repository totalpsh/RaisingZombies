using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// 실제 게임 원본을 관찰해 순차 목표, 수동 수령, 해금과 저장을 관리합니다.
public sealed class QuestManager : Singleton<QuestManager>, ISaveDataProvider
{
    private const int CurrentSaveVersion = 2; // 과거 자동 수령 저장과 호환하는 현재 Provider 버전
    [SerializeField] private QuestSettings settings; // 초반 및 무한 목표·보상 정의
    private QuestState _state = new(); // 다른 시스템의 진행도를 복제하지 않는 퀘스트 원본
    private UpgradeManager _upgrade; // 현재 강화·재화 원본
    private StageManager _stage; // 현재 씬의 스테이지 원본
    private IProductionUpgradeProgressSource _production; // 실제 생산 강화가 제공할 선택적 원본
    private BodyEquipmentManager _bodyEquipment; // 신체 Draw, 장착, 파기와 연구 진행 원본
    private CurrencyWalletManager _wallet; // 신체 뽑기권 보상 지급 원본
    private SaveManager _save; // 기존 통합 저장 서비스
    private bool _ready; // 저장 등록 완료 여부
    private bool _claiming; // 연타와 이벤트 재진입을 막는 수령 잠금
    public event Action Changed; // 진행·상태·해금 UI 갱신 알림
    public bool CurrencyUpgradeUnlocked => _ready && _state.currencyUpgradeUnlocked; // 재화 화면 접근 허용 여부
    public bool ProductionUpgradeUnlocked => _ready && _state.productionUpgradeUnlocked; // 생산 화면 접근 허용 여부
    public int CurrentQuestIndex => _state.currentQuestIndex; // 아직 수령하지 않은 현재 인덱스
    public long EnemyKillCount => _state.enemyKillCount; // 다른 원본이 없는 영구 처치 횟수
    public QuestDefinition CurrentQuest => GetQuest(_state.currentQuestIndex); // 현재 필요한 정의 하나
    public QuestStatus CurrentStatus => GetQuestStatus(_state.currentQuestIndex); // 현재 목표의 실시간 상태
    public QuestSettings Settings => settings; // UI와 테스트가 읽는 생성 밸런스
    public string SaveKey => "main_quest"; // 기존 통합 파일의 독립 저장 구역
    public Type SaveDataType => typeof(QuestState); // 역직렬화 형식

    // 전투나 UI 생성 전에 저장된 퀘스트를 준비합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (!HasInstance) new GameObject(nameof(QuestManager)).AddComponent<QuestManager>();
    }

    // 기존 싱글턴과 Save Provider 방식으로 한 번 초기화합니다.
    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        DontDestroyOnLoad(gameObject);
        if (settings == null) settings = Resources.Load<QuestSettings>("QuestSettings_Default");
        string error = string.Empty; // Settings가 없을 때도 출력할 안전한 검증 메시지
        if (settings == null || !settings.TryValidate(out error))
        { Debug.LogError($"[QuestManager] 유효한 QuestSettings_Default가 필요합니다. {error}", this); return; }
        _save = SaveManager.EnsureInstance();
        _ready = _save.RegisterProvider(this);
        _save.SaveLoaded += RefreshProgress;
        _save.SaveReset += RefreshProgress;
        UpgradeManager.AvailabilityChanged += BindUpgrade;
        BodyEquipmentManager.AvailabilityChanged += BindBodyEquipment;
        CurrencyWalletManager.AvailabilityChanged += BindWallet;
        StageManager.ActiveInstanceChanged += BindStage;
        UnitController.AnyDied += HandleUnitDied;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BindUpgrade(UpgradeManager.HasInstance ? UpgradeManager.Instance : null);
        BindBodyEquipment(BodyEquipmentManager.HasInstance ? BodyEquipmentManager.Instance : null);
        BindWallet(CurrencyWalletManager.HasInstance ? CurrencyWalletManager.Instance : null);
        BindStage(StageManager.ActiveInstance);
    }

    // 씬 교체 후에도 현재 매니저만 관찰합니다.
    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindUpgrade(UpgradeManager.HasInstance ? UpgradeManager.Instance : null);
        BindStage(StageManager.ActiveInstance);
    }

    // 강화 매니저가 재생성되면 이전 이벤트를 해제하고 새 원본을 읽습니다.
    private void BindUpgrade(UpgradeManager manager)
    {
        if (_upgrade != null) _upgrade.stateChanged -= RefreshProgress;
        _upgrade = manager;
        if (_upgrade != null) { _upgrade.stateChanged -= RefreshProgress; _upgrade.stateChanged += RefreshProgress; }
        RefreshProgress();
    }

    // 신체 장비 원본이 재생성되면 이전 이벤트를 해제하고 새 진행도를 읽습니다.
    private void BindBodyEquipment(BodyEquipmentManager manager)
    {
        if (_bodyEquipment != null) _bodyEquipment.StateChanged -= RefreshProgress;
        _bodyEquipment = manager;
        if (_bodyEquipment != null) { _bodyEquipment.StateChanged -= RefreshProgress; _bodyEquipment.StateChanged += RefreshProgress; }
        RefreshProgress();
    }

    // 타입형 지갑이 재생성되면 Quest 보상 지급 원본을 교체합니다.
    private void BindWallet(CurrencyWalletManager manager)
    {
        _wallet = manager;
    }

    // 기존 StageChanged 이벤트를 중복 없이 연결합니다.
    private void BindStage(StageManager manager)
    {
        if (_stage != null) _stage.StageChanged -= HandleStageChanged;
        _stage = manager;
        if (_stage != null) { _stage.StageChanged -= HandleStageChanged; _stage.StageChanged += HandleStageChanged; }
        RefreshProgress();
    }

    // 실제 생산 강화 시스템이 생기면 동일 계산 없이 레벨 원본만 연결합니다.
    public void RegisterProductionProgressSource(IProductionUpgradeProgressSource source)
    {
        _production = source;
        RefreshProgress();
    }

    // 제거되는 생산 시스템의 원본 참조만 해제합니다.
    public void UnregisterProductionProgressSource(IProductionUpgradeProgressSource source)
    {
        if (ReferenceEquals(_production, source)) _production = null;
        RefreshProgress();
    }

    // 생산 강화가 변경됐을 때 해당 시스템이 호출할 이벤트 기반 갱신 진입점입니다.
    public void NotifyProductionProgressChanged() { RefreshProgress(); }

    // Dungeon 시스템이 실제 클리어 한 건을 같은 Quest 원본에 기록할 Hook입니다.
    public void NotifyDungeonCleared()
    {
        if (!_ready || _save == null || _save.IsRestoring) return;
        if (_state.dungeonClearCount < long.MaxValue) _state.dungeonClearCount++;
        _save.MarkDirty();
        RefreshProgress();
    }

    // Stage 원본이 다음 번호로 변경되면 클리어 상태를 갱신합니다.
    private void HandleStageChanged(int stageNumber) { RefreshProgress(); }

    // 실제 사망만 집계하고 좀비 사망이나 풀 반환은 세지 않습니다.
    private void HandleUnitDied(UnitController unit)
    {
        if (!_ready || _save == null || _save.IsRestoring || unit == null || unit.Data == null || unit.Team != UnitTeam.Human) return;
        if (_state.enemyKillCount < long.MaxValue) _state.enemyKillCount++;
        _save.MarkDirty();
        RefreshProgress();
    }

    // 수동 정의 또는 인덱스 기반 반복 정의 하나만 반환합니다.
    public QuestDefinition GetQuest(int questIndex) { return settings == null ? null : settings.GetQuest(questIndex); }

    // 과거는 수령 완료, 현재는 실제 원본 조건으로 상태를 계산합니다.
    public QuestStatus GetQuestStatus(int questIndex)
    {
        if (questIndex < _state.currentQuestIndex) return QuestStatus.Claimed;
        if (questIndex > _state.currentQuestIndex) return QuestStatus.InProgress;
        QuestDefinition quest = GetQuest(questIndex); // 현재 상태를 확인할 정의
        return quest != null && GetRawProgress(quest) >= GetTarget(quest) ? QuestStatus.Claimable : QuestStatus.InProgress;
    }

    // 현재 목표값은 실제 원본에서 읽고 UI 진행 범위만 제한합니다.
    public long GetProgress(QuestDefinition quest) { return quest == null ? 0 : Math.Min(GetTarget(quest), GetRawProgress(quest)); }

    // 상세 팝업에서 현재 실제 값 자체를 보여주기 위한 원본 조회입니다.
    public long GetRawProgress(QuestDefinition quest)
    {
        if (quest == null) return 0;
        long progress = quest.type switch // 같은 진행도를 퀘스트 저장에 복제하지 않는 원본 조회
        {
            QuestType.StatGachaCount => _bodyEquipment == null ? 0 : _bodyEquipment.TotalBodyDrawCount,
            QuestType.StageClear => _stage != null && _stage.CurrentStageNumber > quest.targetStage ? 1 : 0,
            QuestType.EnemyKillCount => _state.enemyKillCount,
            QuestType.CurrencyUpgradeLevel => _upgrade == null ? 0 : _upgrade.GetCurrencyUpgradeSnapshot(quest.targetUpgrade).CurrentLevel,
            QuestType.StatResearchLevel => _bodyEquipment == null ? 0 : _bodyEquipment.ResearchLevel,
            QuestType.ProductionUpgradeLevel => _production == null ? 0 : _production.GetProductionUpgradeLevel(quest.productionUpgradeIndex),
            QuestType.BodyDrawCount => _bodyEquipment == null ? 0 : _bodyEquipment.TotalBodyDrawCount,
            QuestType.EquipmentEquipCount => _bodyEquipment == null ? 0 : _bodyEquipment.TotalEquipCount,
            QuestType.EquipmentDismantleCount => _bodyEquipment == null ? 0 : _bodyEquipment.TotalDismantleCount,
            QuestType.DrawResearchLevel => _bodyEquipment == null ? 0 : _bodyEquipment.ResearchLevel,
            QuestType.MinimumRarityObtained => _bodyEquipment == null ? 0 : _bodyEquipment.HighestRarityTier,
            QuestType.DungeonClearCount => _state.dungeonClearCount,
            _ => 0
        };
        return Math.Max(0L, progress);
    }

    // 스테이지와 최소 레어도 조건은 전용 목표값을 사용하고 나머지는 공통 target을 사용합니다.
    public long GetTarget(QuestDefinition quest)
    {
        if (quest == null) return 0L;
        if (quest.type == QuestType.StageClear) return 1L;
        if (quest.type == QuestType.MinimumRarityObtained) return Mathf.Clamp(quest.targetRarityTier, 1, 12);
        return Math.Max(0, quest.target);
    }

    // 상세 팝업에 계산식 복제 없이 현재 원본 상태를 설명합니다.
    public string GetRelatedProgressText(QuestDefinition quest)
    {
        if (quest == null) return string.Empty;
        long raw = GetRawProgress(quest); // 목표에 제한하지 않은 실제 현재값
        return quest.type switch
        {
            QuestType.StageClear => $"현재 스테이지: {(_stage == null ? 0 : _stage.CurrentStageNumber)}\n목표: 스테이지 {quest.targetStage} 클리어",
            QuestType.StatGachaCount => $"현재 누적 신체 뽑기: {raw}\n목표: {quest.target}회",
            QuestType.EnemyKillCount => $"현재 누적 처치: {raw}\n목표: {quest.target}명",
            QuestType.CurrencyUpgradeLevel => BuildCurrencyUpgradeInfo(quest, raw),
            QuestType.StatResearchLevel => $"현재 뽑기 연구: Lv.{raw}\n목표: Lv.{quest.target}",
            QuestType.ProductionUpgradeLevel => _production == null ? "생산 강화 레벨 원본이 아직 연결되지 않았습니다." : $"현재 생산 강화 레벨: Lv.{raw}\n목표: Lv.{quest.target}",
            QuestType.BodyDrawCount => $"현재 누적 신체 뽑기: {raw}\n목표: {quest.target}회",
            QuestType.EquipmentEquipCount => $"현재 누적 장착: {raw}\n목표: {quest.target}회",
            QuestType.EquipmentDismantleCount => $"현재 누적 파기: {raw}\n목표: {quest.target}회",
            QuestType.DrawResearchLevel => $"현재 뽑기 연구: Lv.{raw}\n목표: Lv.{quest.target}",
            QuestType.MinimumRarityObtained => $"현재 최고 획득 Tier: {raw}\n목표: Tier {quest.targetRarityTier}",
            QuestType.DungeonClearCount => $"현재 Dungeon 클리어: {raw}\n목표: {quest.target}회",
            _ => $"현재: {raw} / {GetTarget(quest)}"
        };
    }

    // 기존 Currency Snapshot의 레벨과 실제 효과를 상세 표시합니다.
    private string BuildCurrencyUpgradeInfo(QuestDefinition quest, long raw)
    {
        if (_upgrade == null) return "재화 강화 데이터를 불러오는 중입니다.";
        CurrencyUpgradeSnapshot snapshot = _upgrade.GetCurrencyUpgradeSnapshot(quest.targetUpgrade); // 실제 강화 계산 결과
        return $"현재 강화 레벨: Lv.{raw}\n목표: Lv.{quest.target}\n현재 효과: {snapshot.CurrentEffect:0.##}";
    }

    // Main UI, 목록, 상세 팝업이 함께 사용하는 유일한 보상 수령 함수입니다.
    public bool TryClaimCurrentQuest()
    {
        if (!_ready || _claiming || _save == null || _save.IsRestoring || _state.currentQuestIndex == int.MaxValue) return false;
        QuestDefinition quest = CurrentQuest; // 수령 직전에 다시 확인할 현재 정의
        if (quest == null || CurrentStatus != QuestStatus.Claimable) return false;
        if (quest.rewardType == QuestRewardType.Currency && _upgrade == null) return false;
        if (quest.rewardType == QuestRewardType.BodyDrawTicket && _wallet == null) return false;
        _claiming = true;
        try
        {
            _state.currentQuestIndex++; // 같은 세션의 연타가 이전 보상을 다시 볼 수 없도록 먼저 이동
            if (quest.unlockReward == QuestUnlockType.CurrencyUpgrade) _state.currencyUpgradeUnlocked = true;
            if (quest.unlockReward == QuestUnlockType.ProductionUpgrade) _state.productionUpgradeUnlocked = true;
            if (quest.rewardType == QuestRewardType.Currency) _upgrade.AddCurrency(quest.rewardAmount);
            if (quest.rewardType == QuestRewardType.BodyDrawTicket && _wallet != null) _wallet.AddCurrency(GameCurrencyType.BodyDrawTicket, quest.rewardAmount);
            _save.MarkDirty();
            if (!_save.SaveGame()) Debug.LogWarning("[QuestManager] 보상 수령 저장에 실패했습니다. 현재 메모리 상태는 유지하며 자동 저장에서 재시도합니다.", this);
        }
        finally { _claiming = false; }
        Changed?.Invoke();
        return true;
    }

    // 관련 원본 이벤트가 올 때 Claimable 표시만 갱신하며 자동 수령하지 않습니다.
    public void RefreshProgress()
    {
        if (!_ready || _claiming || _save == null || _save.IsRestoring) return;
        Changed?.Invoke();
    }

    // 현재 퀘스트 원본을 기존 SaveManager에 제공합니다.
    public object CaptureSaveData() { return _state; }

    // 버전 1의 자동 수령 인덱스와 해금을 그대로 보존하고 누적 목록은 더 이상 사용하지 않습니다.
    public void RestoreSaveData(object data)
    {
        QuestState restored = data as QuestState; // 기존 저장에서 읽은 최소 원본
        if (restored == null || restored.version > CurrentSaveVersion) throw new InvalidOperationException("지원하지 않는 Quest 저장 형식입니다.");
        restored.version = CurrentSaveVersion;
        restored.currentQuestIndex = Math.Max(0, restored.currentQuestIndex);
        restored.enemyKillCount = Math.Max(0L, restored.enemyKillCount);
        restored.dungeonClearCount = Math.Max(0L, restored.dungeonClearCount);
        _state = restored;
        Changed?.Invoke();
    }

    // 새 게임과 기존 저장의 누락 구역은 첫 목표 및 잠금 상태로 시작합니다.
    public void ResetSaveData() { _state = new QuestState(); Changed?.Invoke(); }

    // 씬이나 플레이 종료 시 모든 구독과 Provider를 정리합니다.
    protected override void OnDestroy()
    {
        if (_upgrade != null) _upgrade.stateChanged -= RefreshProgress;
        if (_bodyEquipment != null) _bodyEquipment.StateChanged -= RefreshProgress;
        if (_stage != null) _stage.StageChanged -= HandleStageChanged;
        UpgradeManager.AvailabilityChanged -= BindUpgrade;
        BodyEquipmentManager.AvailabilityChanged -= BindBodyEquipment;
        CurrencyWalletManager.AvailabilityChanged -= BindWallet;
        StageManager.ActiveInstanceChanged -= BindStage;
        UnitController.AnyDied -= HandleUnitDied;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (_save != null)
        {
            _save.SaveLoaded -= RefreshProgress;
            _save.SaveReset -= RefreshProgress;
            _save.UnregisterProvider(this);
        }
        base.OnDestroy();
    }
}
