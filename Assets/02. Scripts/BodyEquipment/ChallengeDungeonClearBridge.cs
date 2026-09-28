using System;
using System.Collections.Generic;
using UnityEngine;

// 던전 ID와 실제 클리어 보상 에셋을 한 곳에서 연결한다.
[Serializable]
public sealed class ChallengeDungeonRewardBinding
{
    public string dungeonId; // 던전 담당자가 사용하는 안정적인 던전 ID
    public ChallengeDungeonRewardSO reward; // UI 표시와 실제 지급이 공유할 보상 에셋
}

// 같은 클리어 실행의 재보고를 저장 후에도 식별한다.
[Serializable]
public sealed class ChallengeProcessedClear
{
    public string dungeonId; // 클리어한 던전 ID
    public string clearRunId; // 던전 담당자가 전달한 한 실행의 고유 ID
}

// 보상 중복 방지 기록을 기존 SaveManager의 한 구역에 저장한다.
[Serializable]
public sealed class ChallengeClearRewardState
{
    public int version = 1; // 도전 클리어 보상 저장 형식 버전
    public List<ChallengeProcessedClear> processedClears = new(); // 이미 보상을 지급한 실행 목록
}

// Challenge 쪽에서 확정된 클리어 한 건만 전달받아 보상, 퀘스트와 저장을 묶는다.
public sealed class ChallengeDungeonClearBridge : MonoBehaviour, ISaveDataProvider
{
    private const string ProviderKey = "challenge_clear_rewards"; // 통합 저장에서 중복 클리어를 기록할 안정적인 키
    [SerializeField] private ChallengeDungeonRewardBinding[] dungeonRewards = Array.Empty<ChallengeDungeonRewardBinding>(); // 던전 ID별 보상 원본
    private readonly Dictionary<string, ChallengeDungeonRewardSO> _rewardsById = new(StringComparer.Ordinal); // 표시와 지급이 공유하는 던전별 캐시
    private readonly HashSet<(string dungeonId, string clearRunId)> _processed = new(); // 같은 실행의 재보고를 막는 캐시
    private ChallengeClearRewardState _state = new(); // 저장할 처리 완료 실행 원본
    private SaveManager _save; // 기존 통합 저장 원본
    private bool _ready; // 보상 데이터와 저장 Provider가 준비됐는지 여부
    private bool _processing; // 이벤트 중 재진입으로 중복 지급되지 않게 하는 잠금

    public string SaveKey => ProviderKey; // 저장 Provider 키
    public Type SaveDataType => typeof(ChallengeClearRewardState); // 저장 DTO 형식

    // Inspector의 던전 보상을 캐시하고 기존 통합 저장에 중복 기록을 등록한다.
    private void Awake()
    {
        if (!BuildRewardLookup()) return;
        _save = SaveManager.EnsureInstance();
        _ready = _save.RegisterProvider(this);
    }

    // 브리지가 제거될 때 해당 저장 Provider 등록을 해제한다.
    private void OnDestroy()
    {
        if (_save != null) _save.UnregisterProvider(this);
    }

    // Challenge UI가 실제 지급에 쓰는 동일한 던전 보상 에셋을 조회한다.
    public bool TryGetReward(string dungeonId, out ChallengeDungeonRewardSO reward)
    {
        reward = null;
        if (_rewardsById.Count == 0) BuildRewardLookup();
        return !string.IsNullOrWhiteSpace(dungeonId) && _rewardsById.TryGetValue(dungeonId, out reward) && reward != null;
    }

    // 던전 담당자가 확정한 한 실행을 중복 없이 보상 지급과 퀘스트에 반영한다.
    public bool TryProcessDungeonClear(string dungeonId, string clearRunId)
    {
        if (!_ready || _processing || _save == null || _save.IsRestoring) return false;
        if (string.IsNullOrWhiteSpace(dungeonId) || string.IsNullOrWhiteSpace(clearRunId))
        {
            Debug.LogWarning("[ChallengeClear] Dungeon ID와 Clear Run ID가 모두 필요합니다.", this);
            return false;
        }
        if (!_rewardsById.TryGetValue(dungeonId, out ChallengeDungeonRewardSO reward))
        {
            Debug.LogWarning($"[ChallengeClear] 알 수 없는 Dungeon ID: {dungeonId}", this);
            return false;
        }
        if (reward == null || reward.Amount <= 0L || reward.CurrencyType != GameCurrencyType.BodyDrawTicket)
        {
            Debug.LogWarning($"[ChallengeClear] {dungeonId}의 Body Draw Ticket 보상 에셋 또는 수량이 유효하지 않습니다.", this);
            return false;
        }
        (string dungeonId, string clearRunId) key = (dungeonId, clearRunId); // 던전과 한 실행을 결합한 중복 방지 키
        if (_processed.Contains(key)) return false;
        QuestManager quests = QuestManager.HasInstance ? QuestManager.Instance : null; // 기존 퀘스트 진행 원본
        if (quests == null || !quests.CanRecordDungeonClear) return false;
        CurrencyWalletManager wallet = CurrencyWalletManager.EnsureInstance(); // 기존 타입형 지갑
        long before = wallet.GetAmount(GameCurrencyType.BodyDrawTicket); // 정확한 보상 지급이 가능한지 검사할 현재 수량
        if (before > long.MaxValue - reward.Amount) return false;

        _processing = true;
        try
        {
            if (!reward.TryGrantClearReward(wallet)) return false;
            if (!quests.TryNotifyDungeonCleared())
            {
                if (!wallet.TrySpendCurrency(GameCurrencyType.BodyDrawTicket, reward.Amount))
                    Debug.LogError("[ChallengeClear] 퀘스트 기록 실패 후 티켓 되돌리기에도 실패했습니다.", this);
                return false;
            }
            _processed.Add(key);
            _state.processedClears.Add(new ChallengeProcessedClear { dungeonId = dungeonId, clearRunId = clearRunId });
            _save.MarkDirty();
            if (!_save.SaveGame()) Debug.LogWarning("[ChallengeClear] 보상을 지급했지만 즉시 저장하지 못했습니다. 자동 저장에서 재시도합니다.", this);
            return true;
        }
        finally { _processing = false; }
    }

    // 현재 중복 방지 기록을 기존 저장 서비스에 제공한다.
    public object CaptureSaveData()
    {
        return _state;
    }

    // 저장된 실행 ID를 복원해 재시작 후 중복 보상을 막는다.
    public void RestoreSaveData(object data)
    {
        ChallengeClearRewardState restored = data as ChallengeClearRewardState; // 역직렬화된 처리 완료 기록
        if (restored == null || restored.version > 1) throw new InvalidOperationException("지원하지 않는 Challenge Clear 저장 형식입니다.");
        _state = restored;
        _state.processedClears ??= new List<ChallengeProcessedClear>();
        _processed.Clear();
        foreach (ChallengeProcessedClear clear in _state.processedClears)
            if (clear != null && !string.IsNullOrWhiteSpace(clear.dungeonId) && !string.IsNullOrWhiteSpace(clear.clearRunId))
                _processed.Add((clear.dungeonId, clear.clearRunId));
    }

    // 새 게임에서는 지급 완료 실행 기록을 비운다.
    public void ResetSaveData()
    {
        _state = new ChallengeClearRewardState();
        _processed.Clear();
    }

    // 던전 ID 중복을 막고 UI와 지급이 사용할 동일한 보상표를 만든다.
    private bool BuildRewardLookup()
    {
        _rewardsById.Clear();
        if (dungeonRewards == null || dungeonRewards.Length == 0)
        {
            Debug.LogWarning("[ChallengeClear] Dungeon Rewards가 비어 있습니다.", this);
            return false;
        }
        foreach (ChallengeDungeonRewardBinding binding in dungeonRewards)
        {
            if (binding == null || string.IsNullOrWhiteSpace(binding.dungeonId) || !_rewardsById.TryAdd(binding.dungeonId, binding.reward))
            {
                Debug.LogError("[ChallengeClear] Dungeon ID 누락 또는 중복이 있습니다.", this);
                _rewardsById.Clear();
                return false;
            }
        }
        return true;
    }
}
