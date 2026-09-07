using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// 실제 게임 원본을 관찰해 순차 목표, 보상, 해금과 저장만 관리합니다.
public sealed class QuestManager : Singleton<QuestManager>, ISaveDataProvider
{
    [SerializeField] private QuestSettings settings; // 목표와 보상 및 이미지 정의
    private QuestState _state = new(); // 다른 시스템의 진행도를 복제하지 않는 퀘스트 원본
    private UpgradeManager _upgrade; // 현재 강화·재화 원본
    private StageManager _stage; // 현재 씬의 스테이지 원본
    private SaveManager _save; // 기존 통합 저장 서비스
    private bool _ready; // 저장 등록 완료 여부
    private bool _checking; // 보상 지급 이벤트의 재진입 방지
    public event Action Changed; // 진행 및 해금 표시 갱신 알림
    public bool CurrencyUpgradeUnlocked => _ready && _state.currencyUpgradeUnlocked; // 재화 화면 접근 허용 여부
    public bool ProductionUpgradeUnlocked => _ready && _state.productionUpgradeUnlocked; // 생산 화면 접근 허용 여부
    public int CurrentQuestIndex => _state.currentQuestIndex; // 저장된 순차 진행 인덱스
    public long EnemyKillCount => _state.enemyKillCount; // 아직 다른 원본이 없는 영구 처치 횟수
    public QuestDefinition CurrentQuest => settings != null && settings.quests != null && _state.currentQuestIndex < settings.quests.Length
        ? settings.quests[_state.currentQuestIndex] : null; // 현재 하나만 표시할 목표
    public string SaveKey => "main_quest"; // 기존 통합 파일에 추가할 독립 저장 구역
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
        if (settings == null || !settings.TryValidate(out _))
        { Debug.LogError("[QuestManager] 유효한 QuestSettings_Default가 필요합니다.", this); return; }
        _save = SaveManager.EnsureInstance();
        _ready = _save.RegisterProvider(this);
        _save.SaveLoaded += RefreshProgress;
        _save.SaveReset += RefreshProgress;
        UpgradeManager.AvailabilityChanged += BindUpgrade;
        StageManager.ActiveInstanceChanged += BindStage;
        UnitController.AnyDied += HandleUnitDied;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        BindUpgrade(UpgradeManager.HasInstance ? UpgradeManager.Instance : null);
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
        if (_upgrade != null) _upgrade.stateChanged += RefreshProgress;
        RefreshProgress();
    }

    // 기존 StageChanged 이벤트를 중복 없이 연결합니다.
    private void BindStage(StageManager manager)
    {
        if (_stage != null) _stage.StageChanged -= HandleStageChanged;
        _stage = manager;
        if (_stage != null) _stage.StageChanged += HandleStageChanged;
        RefreshProgress();
    }

    // Stage 원본이 다음 번호로 변경되면 클리어 조건을 재검사합니다.
    private void HandleStageChanged(int stageNumber) { RefreshProgress(); }

    // 실제 사망만 집계하고 좀비 사망이나 풀 반환은 세지 않습니다.
    private void HandleUnitDied(UnitController unit)
    {
        if (!_ready || _save.IsRestoring || unit == null || unit.Data == null || unit.Team != UnitTeam.Human) return;
        if (_state.enemyKillCount < long.MaxValue) _state.enemyKillCount++;
        _save.MarkDirty();
        RefreshProgress();
    }

    // 현재 목표값은 원본에서 읽고 표시 범위만 제한합니다.
    public long GetProgress(QuestDefinition quest)
    {
        if (quest == null) return 0;
        long progress = quest.type switch // 퀘스트용 진행값을 별도로 누적하지 않는 원본 조회
        {
            QuestType.StatGachaCount => _upgrade == null ? 0 : _upgrade.TotalDrawCount,
            QuestType.StageClear => _stage != null && _stage.CurrentStageNumber > quest.targetStage ? 1 : 0,
            QuestType.EnemyKillCount => _state.enemyKillCount,
            QuestType.CurrencyUpgradeLevel => _upgrade == null ? 0 : _upgrade.GetCurrencyUpgradeSnapshot(quest.targetUpgrade).CurrentLevel,
            QuestType.StatResearchLevel => _upgrade == null ? 0 : _upgrade.GetStatSnapshot(quest.targetStat).ResearchLevel,
            _ => 0
        };
        return Math.Min(GetTarget(quest), Math.Max(0L, progress));
    }

    // 스테이지 클리어는 지정 스테이지 완료 여부 하나로 표시합니다.
    public long GetTarget(QuestDefinition quest) { return quest.type == QuestType.StageClear ? 1 : quest.target; }

    // 보상 지급 전에 완료 ID와 순서를 기록하고 전체 상태를 한 파일로 저장합니다.
    public void RefreshProgress()
    {
        if (!_ready || _checking || _save == null || _save.IsRestoring || _upgrade == null) return;
        _checking = true;
        bool completed = false; // 이번 갱신에서 저장할 완료 발생 여부
        try
        {
            while (CurrentQuest != null)
            {
                QuestDefinition quest = CurrentQuest; // 이번에 검사할 순차 목표
                bool alreadyRewarded = _state.rewardedQuestIds.Contains(quest.id); // 데이터 이동 이후에도 중복 보상 방지
                if (!alreadyRewarded && GetProgress(quest) < GetTarget(quest)) break;
                _state.currentQuestIndex++;
                if (quest.unlockReward == QuestUnlockType.CurrencyUpgrade) _state.currencyUpgradeUnlocked = true;
                if (quest.unlockReward == QuestUnlockType.ProductionUpgrade) _state.productionUpgradeUnlocked = true;
                if (!alreadyRewarded)
                {
                    _state.rewardedQuestIds.Add(quest.id);
                    if (quest.rewardType == QuestRewardType.Currency) _upgrade.AddCurrency(quest.rewardAmount);
                }
                _save.MarkDirty();
                completed = true;
            }
            if (completed && !_save.SaveGame()) Debug.LogWarning("[QuestManager] 보상과 진행 저장에 실패했습니다. 기존 자동 저장에서 재시도합니다.", this);
        }
        finally { _checking = false; }
        Changed?.Invoke();
    }

    // 현재 퀘스트 원본을 기존 SaveManager에 제공합니다.
    public object CaptureSaveData() { return _state; }

    // 전체 Provider 복원이 끝나기 전에는 보상을 판정하지 않습니다.
    public void RestoreSaveData(object data)
    {
        QuestState restored = data as QuestState; // 기존 저장에서 읽은 퀘스트 원본
        if (restored == null || restored.version > 1) throw new InvalidOperationException("지원하지 않는 Quest 저장 형식입니다.");
        _state = restored;
        _state.currentQuestIndex = Math.Max(0, _state.currentQuestIndex);
        _state.enemyKillCount = Math.Max(0L, _state.enemyKillCount);
        _state.rewardedQuestIds ??= new();
        Changed?.Invoke();
    }

    // 새 게임과 기존 저장의 누락 구역은 첫 목표 및 잠금 상태로 시작합니다.
    public void ResetSaveData() { _state = new QuestState(); Changed?.Invoke(); }

    // 씬이나 플레이 종료 시 모든 구독과 Provider를 정리합니다.
    protected override void OnDestroy()
    {
        if (_upgrade != null) _upgrade.stateChanged -= RefreshProgress;
        if (_stage != null) _stage.StageChanged -= HandleStageChanged;
        UpgradeManager.AvailabilityChanged -= BindUpgrade;
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
