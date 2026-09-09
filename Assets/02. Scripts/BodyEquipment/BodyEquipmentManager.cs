using System;
using System.Collections.Generic;
using UnityEngine;

// 신체 장비 뽑기, 인벤토리, 장착, 파기와 실제 시간 연구를 관리합니다.
[DefaultExecutionOrder(-9000)]
public sealed class BodyEquipmentManager : Singleton<BodyEquipmentManager>, ISaveDataProvider, ISaveDataPreparation
{
    private const string ProviderKey = "body_equipment"; // 통합 저장에서 사용할 안정적인 Provider 키
    private const int CurrentSaveVersion = 1; // Body Equipment 내부 저장 형식 버전
    private static readonly BodyDrawResult[] EmptyDrawResults = Array.Empty<BodyDrawResult>(); // 실패 시 재사용할 빈 뽑기 결과
    [SerializeField] private BodyEquipmentDatabaseSO database; // 장비, 레어도와 스탯 원형 Database
    [SerializeField] private BodyDrawResearchSettingsSO researchSettings; // 뽑기 비용, 연구 시간과 실제 확률 Weight
    private BodyEquipmentState _state = new(); // 인벤토리와 연구의 영구 원본
    private readonly Dictionary<string, BodyEquipmentInstance> _inventoryById = new(StringComparer.Ordinal); // Instance ID 기반 인벤토리 캐시
    private readonly Dictionary<BodyEquipmentSlot, string> _equippedBySlot = new(); // Slot 기반 장착 ID 캐시
    private readonly List<EquipmentStatDefinitionSO> _subStatCandidates = new(); // Draw마다 새 List를 만들지 않는 보조 스탯 후보 버퍼
    private CurrencyWalletManager _wallet; // 신체 뽑기권을 소유한 타입형 지갑
    private SaveManager _save; // 기존 통합 저장 서비스
    private EquipmentModifierSnapshot _currentModifiers; // 장착 변경 때만 다시 계산하는 최종 Modifier
    private bool _ready; // Database 검증과 저장 등록이 끝난 상태
    private bool _mutating; // 중복 Draw, Equip, Dismantle과 Research 실행 방지

    public event Action StateChanged; // 인벤토리, 장착 또는 연구 상태 변경 알림
    public event Action EquippedStatsChanged; // 장착 Modifier가 실제로 바뀐 경우에만 보내는 전투 스탯 갱신 알림
    public event Action<IReadOnlyList<BodyDrawResult>> BodyDrawCompleted; // 실제 생성된 장비 개수를 포함한 Draw 완료 알림
    public event Action<BodyEquipmentInstance, string> EquipmentEquipped; // 새 장비와 교체된 이전 ID 알림
    public event Action<string, int> EquipmentDismantled; // 파기한 Instance ID와 지급 포인트 알림
    public event Action<int> DrawResearchLevelChanged; // 실제 시간 완료 후 적용된 연구 레벨 알림
    public static event Action<BodyEquipmentManager> AvailabilityChanged; // 장비 원본 생성 및 제거 알림
    public string SaveKey => ProviderKey; // 통합 저장에 노출할 Provider 키
    public Type SaveDataType => typeof(BodyEquipmentState); // 역직렬화할 저장 DTO 형식
    public BodyEquipmentDatabaseSO Database => database; // UI와 Aggregator가 공유할 원형 Database
    public BodyDrawResearchSettingsSO ResearchSettings => researchSettings; // UI가 공유할 실제 연구 확률표
    public IReadOnlyList<BodyEquipmentInstance> Inventory => _state.inventory; // 현재 소유 장비 읽기 전용 목록
    public int InventoryCount => _state == null || _state.inventory == null ? 0 : _state.inventory.Count; // 현재 소유 장비 개수
    public int ResearchLevel => _state == null ? 1 : _state.researchLevel; // 현재 적용 중인 연구 레벨
    public int ResearchPoints => _state == null ? 0 : _state.researchPoints; // 현재 보유 연구 포인트
    public bool IsResearching => _state != null && _state.isResearching; // 실제 시간 연구 진행 여부
    public long TotalBodyDrawCount => _state == null ? 0L : _state.totalBodyDrawCount; // Quest에서 읽을 실제 누적 Draw 횟수
    public long TotalEquipCount => _state == null ? 0L : _state.totalEquipCount; // Quest에서 읽을 실제 누적 장착 횟수
    public long TotalDismantleCount => _state == null ? 0L : _state.totalDismantleCount; // Quest에서 읽을 실제 누적 파기 횟수
    public int HighestRarityTier => _state == null ? 0 : _state.highestRarityTier; // Quest에서 읽을 최고 획득 레어도
    public EquipmentModifierSnapshot CurrentModifiers => _currentModifiers; // 좀비 최종 스탯 계산에 사용할 장착 합산 결과
    public long BodyDrawTickets => _wallet == null ? 0L : _wallet.GetAmount(GameCurrencyType.BodyDrawTicket); // 현재 신체 뽑기권 수

    // Dungeon, Offline Reward와 Quest가 같은 타입형 경로로 신체 뽑기권을 지급합니다.
    public bool AddBodyDrawTickets(long amount)
    {
        return _wallet != null && _wallet.AddCurrency(GameCurrencyType.BodyDrawTicket, amount);
    }

    // 씬 배치 없이 신체 장비 원본 인스턴스를 준비합니다.
    public static BodyEquipmentManager EnsureInstance()
    {
        if (HasInstance) return Instance;
        return new GameObject(nameof(BodyEquipmentManager)).AddComponent<BodyEquipmentManager>();
    }

    // 첫 씬보다 먼저 장비와 연구 저장 원본을 준비합니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    // Default Asset을 한 번 읽고 기존 SaveManager에 Provider를 등록합니다.
    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        DontDestroyOnLoad(gameObject);
        if (database == null) database = Resources.Load<BodyEquipmentDatabaseSO>("BodyEquipment/BodyEquipmentDatabase_Default");
        if (researchSettings == null) researchSettings = Resources.Load<BodyDrawResearchSettingsSO>("BodyEquipment/BodyDrawResearchSettings_Default");
        string databaseError = string.Empty; // Database가 없을 때도 출력할 검증 메시지
        string researchError = string.Empty; // Research Settings가 없을 때도 출력할 검증 메시지
        if (database == null || !database.TryInitialize(out databaseError) || researchSettings == null || !researchSettings.TryValidate(out researchError))
        {
            Debug.LogError($"[BodyEquipmentManager] Default Asset이 없거나 유효하지 않습니다. Database={databaseError}, Research={researchError}", this);
            return;
        }
        _wallet = CurrencyWalletManager.EnsureInstance();
        _save = SaveManager.EnsureInstance();
        _ready = _save.RegisterProvider(this);
        if (_ready) RefreshResearchCompletion();
        AvailabilityChanged?.Invoke(this);
    }

    // 요청 횟수의 비용, 용량과 원형 데이터를 모두 만족하는지 확인합니다.
    public bool CanDraw(int count)
    {
        if (!_ready || _mutating || (count != 1 && count != 10) || _wallet == null) return false;
        int cost = GetDrawTicketCost(count); // 실제 Settings의 Ticket 비용
        if (_wallet.GetAmount(GameCurrencyType.BodyDrawTicket) < cost) return false;
        if (!HasInventorySpace(count)) return false;
        return HasDrawableDefinitions() && GetCurrentResearchDefinition() != null;
    }

    // 1회 또는 10회 비용을 Settings에서 반환합니다.
    public int GetDrawTicketCost(int count)
    {
        if (researchSettings == null) return 0;
        return count == 10 ? researchSettings.TenDrawTicketCost : count == 1 ? researchSettings.SingleDrawTicketCost : 0;
    }

    // Ticket을 소비하고 요청한 수만큼 실제 장비 Instance를 Inventory에 생성합니다.
    public bool TryDraw(int count, out IReadOnlyList<BodyDrawResult> results)
    {
        results = EmptyDrawResults;
        if (!CanDraw(count)) return false;
        _mutating = true;
        try
        {
            int cost = GetDrawTicketCost(count); // 이번 Draw에 한 번만 소비할 Ticket 수
            if (!_wallet.TrySpendCurrency(GameCurrencyType.BodyDrawTicket, cost)) return false;
            List<BodyDrawResult> created = new(count); // UI와 Quest에 전달할 이번 결과
            for (int index = 0; index < count; index++)
            {
                BodyEquipmentInstance equipment = CreateRandomEquipment(); // Inventory에 추가할 실제 랜덤 장비
                if (equipment == null)
                {
                    _wallet.AddCurrency(GameCurrencyType.BodyDrawTicket, cost);
                    RollbackCreatedEquipment(created);
                    return false;
                }
                _state.inventory.Add(equipment);
                _inventoryById.Add(equipment.uniqueId, equipment);
                IncrementSaturated(ref _state.totalBodyDrawCount);
                _state.highestRarityTier = Mathf.Max(_state.highestRarityTier, equipment.rarityTier);
                created.Add(new BodyDrawResult(equipment.Clone()));
            }
            results = created;
            SaveNow();
            StateChanged?.Invoke();
            BodyDrawCompleted?.Invoke(created);
            return true;
        }
        finally { _mutating = false; }
    }

    // Instance ID에 해당하는 소유 장비 원본을 읽기 전용으로 조회합니다.
    public BodyEquipmentInstance GetEquipment(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return null;
        return _inventoryById.TryGetValue(instanceId, out BodyEquipmentInstance equipment) ? equipment : null;
    }

    // 지정 슬롯에 현재 장착된 장비를 반환합니다.
    public BodyEquipmentInstance GetEquipped(BodyEquipmentSlot slot)
    {
        return _equippedBySlot.TryGetValue(slot, out string instanceId) ? GetEquipment(instanceId) : null;
    }

    // 새 장비를 해당 슬롯에 장착하고 기존 장비는 소유 Inventory에 그대로 보존합니다.
    public bool TryEquip(string instanceId, out string replacedInstanceId)
    {
        replacedInstanceId = string.Empty;
        if (!_ready || _mutating || !TryGetValidEquipment(instanceId, out BodyEquipmentInstance equipment, out BodyEquipmentDefinitionSO definition)) return false;
        if (_equippedBySlot.TryGetValue(definition.Slot, out string currentId) && string.Equals(currentId, instanceId, StringComparison.Ordinal)) return false;
        _mutating = true;
        try
        {
            if (_equippedBySlot.TryGetValue(definition.Slot, out string existingId)) replacedInstanceId = existingId;
            SetEquippedSlot(definition.Slot, equipment.uniqueId);
            IncrementSaturated(ref _state.totalEquipCount);
            RebuildModifiers();
            SaveNow();
            EquippedStatsChanged?.Invoke();
            StateChanged?.Invoke();
            EquipmentEquipped?.Invoke(equipment.Clone(), replacedInstanceId);
            return true;
        }
        finally { _mutating = false; }
    }

    // 지정 슬롯의 장비를 Inventory에 남긴 채 장착 상태만 해제합니다.
    public bool TryUnequip(BodyEquipmentSlot slot)
    {
        if (!_ready || _mutating || !_equippedBySlot.Remove(slot)) return false;
        for (int index = _state.equippedSlots.Count - 1; index >= 0; index--)
            if (_state.equippedSlots[index] != null && _state.equippedSlots[index].slot == slot) _state.equippedSlots.RemoveAt(index);
        RebuildModifiers();
        SaveNow();
        EquippedStatsChanged?.Invoke();
        StateChanged?.Invoke();
        return true;
    }

    // 장비 파기 보호 상태를 저장하고 UI에 알립니다.
    public bool TrySetLocked(string instanceId, bool locked)
    {
        if (!_ready || _mutating || !_inventoryById.TryGetValue(instanceId, out BodyEquipmentInstance equipment) || equipment.isLocked == locked) return false;
        equipment.isLocked = locked;
        SaveNow();
        StateChanged?.Invoke();
        return true;
    }

    // 장착 또는 잠금되지 않은 장비를 제거하고 Rarity 포인트를 지급합니다.
    public bool TryDismantle(string instanceId, out int researchPointsGranted)
    {
        researchPointsGranted = 0;
        if (!_ready || _mutating || !_inventoryById.TryGetValue(instanceId, out BodyEquipmentInstance equipment)) return false;
        if (equipment.isLocked || IsEquipped(instanceId) || !database.TryGetRarity(equipment.rarityTier, out BodyRarityDefinitionSO rarity)) return false;
        _mutating = true;
        try
        {
            researchPointsGranted = rarity.DismantleResearchPoint;
            _state.inventory.Remove(equipment);
            _inventoryById.Remove(instanceId);
            _state.researchPoints = SaturatingAdd(_state.researchPoints, researchPointsGranted);
            IncrementSaturated(ref _state.totalDismantleCount);
            SaveNow();
            StateChanged?.Invoke();
            EquipmentDismantled?.Invoke(instanceId, researchPointsGranted);
            return true;
        }
        finally { _mutating = false; }
    }

    // 장비가 어느 슬롯에든 현재 장착되어 있는지 확인합니다.
    public bool IsEquipped(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return false;
        foreach (string equippedId in _equippedBySlot.Values) // 슬롯별 현재 장착 ID
            if (string.Equals(equippedId, instanceId, StringComparison.Ordinal)) return true;
        return false;
    }

    // 포인트가 충분하고 연구 중이 아닐 때 다음 레벨의 실제 시간 연구를 시작합니다.
    public bool TryStartResearch()
    {
        RefreshResearchCompletion();
        BodyDrawResearchLevelDefinition current = GetCurrentResearchDefinition(); // 현재 단계가 제공하는 다음 연구 비용과 시간
        if (!_ready || _mutating || _state.isResearching || current == null || _state.researchLevel >= researchSettings.MaximumResearchLevel) return false;
        if (_state.researchPoints < current.requiredResearchPoints) return false;
        _state.researchPoints -= current.requiredResearchPoints;
        _state.isResearching = true;
        long duration = (long)Math.Ceiling(Math.Max(0d, current.durationSeconds)); // 저장할 정수 초 연구 시간
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); // 연구 시작 UTC 초
        _state.researchEndUtc = duration >= long.MaxValue - now ? long.MaxValue : now + duration;
        CompleteResearchIfReady(true);
        SaveNow();
        StateChanged?.Invoke();
        return true;
    }

    // 앱 종료 중에도 흐른 UTC 시간을 반영해 완료된 연구를 자동 적용합니다.
    public bool RefreshResearchCompletion()
    {
        return CompleteResearchIfReady(true);
    }

    // 진행 중인 연구의 남은 실제 시간을 초 단위로 반환합니다.
    public double GetRemainingResearchSeconds()
    {
        if (_state == null || !_state.isResearching) return 0d;
        return Math.Max(0d, _state.researchEndUtc - DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    // 현재 연구 레벨의 실제 Weight를 정규화한 12개 확률을 만듭니다.
    public List<BodyRarityProbability> GetCurrentRarityProbabilities()
    {
        List<BodyRarityProbability> values = new(12); // UI 한 번 갱신에 사용할 확률 결과
        if (database == null || researchSettings == null) return values;
        for (int tier = 1; tier <= 12; tier++)
        {
            string displayName = database.TryGetRarity(tier, out BodyRarityDefinitionSO rarity) ? rarity.DisplayName : $"Tier {tier}"; // 데이터 기반 표시명
            values.Add(new BodyRarityProbability(tier, displayName, researchSettings.GetProbability(ResearchLevel, tier)));
        }
        return values;
    }

    // 지정 연구 레벨과 Tier의 실제 Draw 확률을 같은 Weight 원본에서 반환합니다.
    public float GetRarityProbability(int researchLevel, int rarityTier)
    {
        return researchSettings == null ? 0f : researchSettings.GetProbability(researchLevel, rarityTier);
    }

    // 현재 상태를 저장하기 전에 완료된 실제 시간 연구를 반영합니다.
    public bool PrepareSaveData()
    {
        return CompleteResearchIfReady(true);
    }

    // 현재 인벤토리와 연구 원본을 통합 저장에 제공합니다.
    public object CaptureSaveData()
    {
        NormalizeState();
        return _state;
    }

    // 기존 저장에 새 구역이 없어도 빈 인벤토리 기본값으로 안전하게 복원합니다.
    public void RestoreSaveData(object data)
    {
        BodyEquipmentState restored = data as BodyEquipmentState; // JSON에서 복원한 장비 DTO
        if (restored == null || restored.version > CurrentSaveVersion) throw new InvalidOperationException("지원하지 않는 Body Equipment 저장 형식입니다.");
        _state = restored;
        NormalizeState();
        bool completed = CompleteResearchIfReady(false); // 접속하지 않은 동안 끝난 연구 여부
        if (completed && _save != null) _save.MarkDirty();
        EquippedStatsChanged?.Invoke();
        StateChanged?.Invoke();
    }

    // 새 게임은 빈 인벤토리와 설정의 초기 연구 레벨로 시작합니다.
    public void ResetSaveData()
    {
        int initialLevel = researchSettings == null ? 1 : researchSettings.InitialResearchLevel; // 새 저장의 연구 레벨
        _state = new BodyEquipmentState { researchLevel = initialLevel };
        NormalizeState();
        EquippedStatsChanged?.Invoke();
        StateChanged?.Invoke();
    }

    // 한 번의 Draw에서 Rarity, 원형, Main과 중복 없는 Sub Stat을 결정합니다.
    private BodyEquipmentInstance CreateRandomEquipment()
    {
        BodyRarityDefinitionSO rarity = RollRarity(); // 현재 연구 Weight로 결정한 레어도
        BodyEquipmentDefinitionSO definition = RollEquipmentDefinition(); // 전체 원형 Weight로 결정한 장비
        if (rarity == null || definition == null || definition.PossibleMainStats.Count == 0) return null;
        EquipmentStatDefinitionSO mainDefinition = definition.PossibleMainStats[UnityEngine.Random.Range(0, definition.PossibleMainStats.Count)]; // 슬롯 Pool에서 결정한 주 스탯
        float baseMainRoll = Roll(rarity.MainRollMin, rarity.MainRollMax); // 레어도의 기준 주 스탯 Roll
        BodyEquipmentInstance equipment = new() // Inventory에 저장할 실제 장비
        {
            uniqueId = Guid.NewGuid().ToString("N"),
            definitionId = definition.DefinitionId,
            rarityTier = rarity.Tier,
            mainStat = new EquipmentStatRoll { statType = mainDefinition.StatType, value = SanitizeRoll(baseMainRoll * mainDefinition.MainStatScale) }
        };
        BuildUniqueSubCandidates(definition, mainDefinition.StatType);
        int maximumSubCount = Mathf.Min(rarity.SubStatMaxCount, _subStatCandidates.Count); // 후보 수를 넘지 않는 최대 옵션 수
        int minimumSubCount = Mathf.Min(rarity.SubStatMinCount, maximumSubCount); // 최대보다 크지 않은 최소 옵션 수
        int subCount = maximumSubCount <= minimumSubCount ? minimumSubCount : UnityEngine.Random.Range(minimumSubCount, maximumSubCount + 1); // 이번 장비의 보조 옵션 수
        for (int index = 0; index < subCount; index++)
        {
            int selectedIndex = UnityEngine.Random.Range(index, _subStatCandidates.Count); // 중복 없는 부분 셔플 대상
            (_subStatCandidates[index], _subStatCandidates[selectedIndex]) = (_subStatCandidates[selectedIndex], _subStatCandidates[index]);
            EquipmentStatDefinitionSO subDefinition = _subStatCandidates[index]; // 이번 보조 옵션 정의
            float value = Roll(subDefinition.SubStatMin, subDefinition.SubStatMax) * rarity.SubStatMultiplier; // Rarity 배율을 반영한 보조 Roll
            equipment.subStats.Add(new EquipmentStatRoll { statType = subDefinition.StatType, value = SanitizeRoll(value) });
        }
        return equipment;
    }

    // 현재 연구 레벨 Weight 합에서 실제 Rarity를 결정합니다.
    private BodyRarityDefinitionSO RollRarity()
    {
        BodyDrawResearchLevelDefinition level = GetCurrentResearchDefinition(); // 실제 확률표 원본
        double total = level == null ? 0d : level.GetTotalWeight(); // 정규화하지 않은 전체 Weight
        if (total <= 0d) return null;
        double roll = UnityEngine.Random.value * total; // 전체 Weight 안의 랜덤 위치
        double accumulated = 0d; // Tier 순서대로 누적한 Weight
        BodyRarityDefinitionSO fallback = null; // 부동소수 오차 때 사용할 마지막 양수 Rarity
        for (int tier = 1; tier <= 12; tier++)
        {
            float weight = level.GetWeight(tier); // 현재 Tier의 실제 Weight
            if (weight <= 0f || !database.TryGetRarity(tier, out BodyRarityDefinitionSO rarity)) continue;
            fallback = rarity;
            accumulated += weight;
            if (roll < accumulated) return rarity;
        }
        return fallback;
    }

    // 전체 장비 원형 Weight 합에서 한 Definition을 결정합니다.
    private BodyEquipmentDefinitionSO RollEquipmentDefinition()
    {
        return database == null ? null : database.GetWeightedEquipment(UnityEngine.Random.value);
    }

    // 같은 종류와 Main Stat을 제외한 보조 Stat 후보를 재사용 버퍼에 만듭니다.
    private void BuildUniqueSubCandidates(BodyEquipmentDefinitionSO definition, EquipmentStatType mainType)
    {
        _subStatCandidates.Clear();
        HashSet<EquipmentStatType> types = new() { mainType }; // 같은 보조 옵션과 Main 옵션 중복 방지 집합
        foreach (EquipmentStatDefinitionSO stat in definition.PossibleSubStats) // 슬롯별 허용 보조 옵션
            if (stat != null && types.Add(stat.StatType)) _subStatCandidates.Add(stat);
    }

    // 복원된 Inventory ID, Definition, Rarity, 장착 슬롯과 연구 상태를 정규화합니다.
    private void NormalizeState()
    {
        if (_state == null) _state = new BodyEquipmentState();
        _state.version = CurrentSaveVersion;
        _state.researchLevel = Mathf.Clamp(_state.researchLevel <= 0 ? researchSettings.InitialResearchLevel : _state.researchLevel, researchSettings.InitialResearchLevel, researchSettings.MaximumResearchLevel);
        _state.researchPoints = Mathf.Max(0, _state.researchPoints);
        _state.totalBodyDrawCount = Math.Max(0L, _state.totalBodyDrawCount);
        _state.totalEquipCount = Math.Max(0L, _state.totalEquipCount);
        _state.totalDismantleCount = Math.Max(0L, _state.totalDismantleCount);
        _state.highestRarityTier = Mathf.Clamp(_state.highestRarityTier, 0, 12);
        if (!_state.isResearching) _state.researchEndUtc = 0L;
        if (_state.inventory == null) _state.inventory = new List<BodyEquipmentInstance>();
        if (_state.equippedSlots == null) _state.equippedSlots = new List<EquippedBodySlotEntry>();
        _inventoryById.Clear();
        HashSet<string> ids = new(StringComparer.Ordinal); // 중복 Instance ID 검사 집합
        for (int index = _state.inventory.Count - 1; index >= 0; index--)
        {
            BodyEquipmentInstance equipment = _state.inventory[index]; // 복원된 소유 장비
            if (!NormalizeEquipment(equipment, ids)) { _state.inventory.RemoveAt(index); continue; }
            _inventoryById.Add(equipment.uniqueId, equipment);
            _state.highestRarityTier = Mathf.Max(_state.highestRarityTier, equipment.rarityTier);
        }
        _equippedBySlot.Clear();
        for (int index = _state.equippedSlots.Count - 1; index >= 0; index--)
        {
            EquippedBodySlotEntry entry = _state.equippedSlots[index]; // 복원된 슬롯 연결
            if (entry == null || !_inventoryById.TryGetValue(entry.instanceId, out BodyEquipmentInstance equipment) ||
                !database.TryGetEquipment(equipment.definitionId, out BodyEquipmentDefinitionSO definition) || definition.Slot != entry.slot || _equippedBySlot.ContainsKey(entry.slot))
            { _state.equippedSlots.RemoveAt(index); continue; }
            _equippedBySlot.Add(entry.slot, entry.instanceId);
        }
        RebuildModifiers();
    }

    // 장비 하나의 ID, 원형, 레어도와 Roll 값이 안전한지 확인합니다.
    private bool NormalizeEquipment(BodyEquipmentInstance equipment, HashSet<string> ids)
    {
        if (equipment == null || !database.TryGetEquipment(equipment.definitionId, out BodyEquipmentDefinitionSO definition) ||
            !database.TryGetRarity(equipment.rarityTier, out _) || equipment.mainStat == null || !IsAllowedStat(definition.PossibleMainStats, equipment.mainStat.statType)) return false;
        if (string.IsNullOrWhiteSpace(equipment.uniqueId) || !ids.Add(equipment.uniqueId))
        {
            equipment.uniqueId = Guid.NewGuid().ToString("N");
            ids.Add(equipment.uniqueId);
        }
        equipment.mainStat.value = SanitizeRoll(equipment.mainStat.value);
        if (equipment.subStats == null) equipment.subStats = new List<EquipmentStatRoll>();
        HashSet<EquipmentStatType> subTypes = new() { equipment.mainStat.statType }; // 복원 데이터의 중복 옵션 검사 집합
        for (int index = equipment.subStats.Count - 1; index >= 0; index--)
        {
            EquipmentStatRoll roll = equipment.subStats[index]; // 검사할 보조 옵션
            if (roll == null || !IsAllowedStat(definition.PossibleSubStats, roll.statType) || !subTypes.Add(roll.statType)) { equipment.subStats.RemoveAt(index); continue; }
            roll.value = SanitizeRoll(roll.value);
        }
        return true;
    }

    // 지정 Pool에 같은 스탯 종류가 존재하는지 확인합니다.
    private static bool IsAllowedStat(IReadOnlyList<EquipmentStatDefinitionSO> pool, EquipmentStatType type)
    {
        if (pool == null) return false;
        for (int index = 0; index < pool.Count; index++)
            if (pool[index] != null && pool[index].StatType == type) return true;
        return false;
    }

    // 현재 슬롯의 저장 Entry와 조회 캐시를 같은 ID로 갱신합니다.
    private void SetEquippedSlot(BodyEquipmentSlot slot, string instanceId)
    {
        _equippedBySlot[slot] = instanceId;
        foreach (EquippedBodySlotEntry entry in _state.equippedSlots) // 교체할 기존 슬롯 Entry
        {
            if (entry == null || entry.slot != slot) continue;
            entry.instanceId = instanceId;
            return;
        }
        _state.equippedSlots.Add(new EquippedBodySlotEntry { slot = slot, instanceId = instanceId });
    }

    // 실제 시간 종료 시 연구 레벨을 한 단계 자동 적용합니다.
    private bool CompleteResearchIfReady(bool notify)
    {
        if (_state == null || !_state.isResearching || DateTimeOffset.UtcNow.ToUnixTimeSeconds() < _state.researchEndUtc) return false;
        _state.isResearching = false;
        _state.researchEndUtc = 0L;
        _state.researchLevel = Mathf.Min(researchSettings.MaximumResearchLevel, _state.researchLevel + 1);
        if (_save != null) _save.MarkDirty();
        if (notify)
        {
            DrawResearchLevelChanged?.Invoke(_state.researchLevel);
            StateChanged?.Invoke();
        }
        return true;
    }

    // 현재 적용 중인 연구 레벨의 원형을 반환합니다.
    private BodyDrawResearchLevelDefinition GetCurrentResearchDefinition()
    {
        return researchSettings == null ? null : researchSettings.GetLevel(ResearchLevel);
    }

    // Inventory Capacity가 요청 Draw 개수를 모두 받을 수 있는지 확인합니다.
    private bool HasInventorySpace(int count)
    {
        int capacity = researchSettings == null ? 0 : researchSettings.InventoryCapacity; // 0이면 무제한인 설정 용량
        return capacity <= 0 || (long)InventoryCount + count <= capacity;
    }

    // 현재 Database에 양수 Weight와 Main Stat을 가진 장비가 있는지 확인합니다.
    private bool HasDrawableDefinitions()
    {
        if (database == null || database.EquipmentDefinitions == null) return false;
        foreach (BodyEquipmentDefinitionSO definition in database.EquipmentDefinitions) // 실제 Draw 가능한 장비 원형
            if (definition != null && definition.DrawWeight > 0f && definition.PossibleMainStats.Count > 0) return true;
        return false;
    }

    // Instance와 Definition을 한 번에 안전하게 조회합니다.
    private bool TryGetValidEquipment(string instanceId, out BodyEquipmentInstance equipment, out BodyEquipmentDefinitionSO definition)
    {
        equipment = GetEquipment(instanceId);
        definition = null;
        return equipment != null && database != null && database.TryGetEquipment(equipment.definitionId, out definition);
    }

    // 실패한 Draw에서 이미 만든 장비를 Inventory 캐시와 목록에서 제거합니다.
    private void RollbackCreatedEquipment(List<BodyDrawResult> created)
    {
        foreach (BodyDrawResult result in created) // 이번 실패 전에 생성된 장비
        {
            if (result.Equipment == null) continue;
            BodyEquipmentInstance stored = GetEquipment(result.Equipment.uniqueId); // 제거할 실제 Inventory 원본
            if (stored != null) _state.inventory.Remove(stored);
            _inventoryById.Remove(result.Equipment.uniqueId);
            if (_state.totalBodyDrawCount > 0L) _state.totalBodyDrawCount--;
        }
    }

    // 현재 장착 원본으로 최종 Modifier Snapshot을 다시 만듭니다.
    private void RebuildModifiers()
    {
        _currentModifiers = EquipmentStatAggregator.Calculate(this);
    }

    // Dirty 처리 후 Draw, 장착과 파기 손실을 막기 위해 즉시 저장합니다.
    private void SaveNow()
    {
        if (_save == null) return;
        _save.MarkDirty();
        if (!_save.SaveGame()) Debug.LogWarning("[BodyEquipmentManager] 저장에 실패했습니다. 메모리 상태를 유지하고 다음 저장에서 재시도합니다.", this);
    }

    // 지정 범위에서 NaN 없이 랜덤 실수값을 반환합니다.
    private static float Roll(float minimum, float maximum)
    {
        float safeMinimum = Mathf.Max(0f, minimum); // 안전한 최소값
        float safeMaximum = Mathf.Max(safeMinimum, maximum); // 안전한 최대값
        return Mathf.Lerp(safeMinimum, safeMaximum, UnityEngine.Random.value);
    }

    // Roll 결과에서 음수와 비정상 부동소수 값을 제거합니다.
    private static float SanitizeRoll(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
    }

    // int 연구 포인트 덧셈을 최대값 안에서 처리합니다.
    private static int SaturatingAdd(int current, int amount)
    {
        return (int)Math.Min(int.MaxValue, (long)Math.Max(0, current) + Math.Max(0, amount));
    }

    // 장기 누적 횟수를 long 최대값에서 멈춥니다.
    private static void IncrementSaturated(ref long value)
    {
        if (value < long.MaxValue) value++;
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
