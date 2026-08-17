using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 가챠, 연구, 재화 강화, 저장과 최종 스탯 조회를 담당합니다.
public sealed class UpgradeManager : Singleton<UpgradeManager>, ISaveDataProvider, ISaveDataPreparation, ISaveResetCleanup
{
    private const string ProviderKey = "upgrade"; // 통합 저장에서 사용할 안정적인 Provider 키
    private const string LegacyPlayerPrefsKey = "RaisingZombies.Upgrade.State"; // 기존 PlayerPrefs 저장 키
    private const int CurrentUpgradeSaveVersion = 2; // Upgrade Provider 내부 데이터 형식 버전
    [SerializeField] private UpgradeBalanceSettings balanceSettings; // 좀비 가챠 및 연구 밸런스
    [SerializeField] private CurrencyUpgradeBalanceSettings currencyUpgradeBalance; // 재화 강화 및 오프라인 보상 밸런스
    [SerializeField, Min(0)] private int startingCurrency = 10000; // 최초 저장 생성 시 지급할 테스트 재화
    private UpgradeState _state; // 현재 저장 상태
    private float _currencyProductionSeconds; // 다음 정수 초 지급까지 누적한 게임 시간
    private float _currencyProductionRemainder; // 정수로 지급하지 못한 재화 소수 잔여량
    private OfflineCurrencyReward _pendingOfflineReward; // 아직 UI가 소비하지 않은 오프라인 보상 결과
    private bool _hasPendingOfflineReward; // 오프라인 결과가 표시 대기 중인지 여부
    private bool _legacyMigrationPending; // 통합 파일 저장 성공을 기다리는 Legacy 이전 상태
    private bool _isInBackground; // 현재 앱이 Background 상태인지 여부
    private int _lastResumeProcessFrame = -1; // 같은 Background 구간의 Resume 중복 처리를 막는 프레임

    public event Action stateChanged; // 저장 상태 변경 이벤트
    public event Action<IReadOnlyList<GachaDrawResult>> drawCompleted; // 가챠 완료 이벤트
    public event Action<OfflineCurrencyReward> offlineRewardGranted; // 오프라인 보상 실제 지급 이벤트
    public int Currency => _state == null ? 0 : _state.currency;
    public int GachaLevel => _state == null ? 1 : _state.gachaLevel;
    public int DrawsAtCurrentLevel => _state == null ? 0 : _state.drawsAtCurrentLevel;
    public UpgradeBalanceSettings BalanceSettings => balanceSettings;
    public CurrencyUpgradeBalanceSettings CurrencyUpgradeBalance => currencyUpgradeBalance;
    string ISaveDataProvider.SaveKey => ProviderKey; // 통합 저장에 노출하는 Provider 키
    Type ISaveDataProvider.SaveDataType => typeof(UpgradeState); // 업그레이드 저장 DTO 형식

    // 싱글턴을 등록하고 기존 저장 및 오프라인 보상을 불러옵니다.
    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;
        if (currencyUpgradeBalance == null)
            Debug.LogError("[UpgradeManager] CurrencyUpgradeBalanceSettings 참조가 없습니다. 재화 생산과 오프라인 보상이 비활성화됩니다.", this);
        InitializeSaveProvider();
    }

    // Time.timeScale 영향을 받는 초 단위 재화를 지급합니다.
    private void Update()
    {
        if (_isInBackground) return;
        if (currencyUpgradeBalance == null || Time.deltaTime <= 0f) return;
        _currencyProductionSeconds += Time.deltaTime;
        int elapsedWholeSeconds = Mathf.FloorToInt(_currencyProductionSeconds); // 이번 프레임까지 완성된 정수 초
        if (elapsedWholeSeconds <= 0) return;
        _currencyProductionSeconds -= elapsedWholeSeconds;
        _currencyProductionRemainder += GetCurrencyPerSecond() * elapsedWholeSeconds;
        int wholeCurrency = Mathf.FloorToInt(_currencyProductionRemainder); // 실제 지급 가능한 정수 재화
        if (wholeCurrency <= 0) return;
        _currencyProductionRemainder -= wholeCurrency;
        AddCurrency(wholeCurrency);
    }

    // Inspector의 잘못된 시작 재화 값을 보정합니다.
    private void OnValidate()
    {
        if (startingCurrency < 0) startingCurrency = 0;
    }

    // 양수 재화를 안전하게 더하고 Dirty 상태로 표시합니다.
    public void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        if (_state == null) _state = CreateInitialState();
        _state.currency = (int)Math.Min(int.MaxValue, (long)_state.currency + amount);
        SaveAndNotify(false);
    }

    // Pause 진입을 기록하고 해제될 때 Background 보상을 한 번 처리합니다.
    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            _isInBackground = true;
            return;
        }

        TryProcessBackgroundResume();
    }

    // Focus 상실을 기록하고 복귀할 때 Background 보상을 한 번 처리합니다.
    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
        {
            _isInBackground = true;
            return;
        }

        TryProcessBackgroundResume();
    }

    // 한 Background 구간의 오프라인 보상을 처리하고 즉시 통합 저장합니다.
    private void TryProcessBackgroundResume()
    {
        if (!_isInBackground || _lastResumeProcessFrame == Time.frameCount || _state == null) return;
        _isInBackground = false;
        _lastResumeProcessFrame = Time.frameCount;
        bool rewardGranted = ProcessOfflineReward(); // 이번 Background 구간에 실제 보상이 지급됐는지 여부
        SaveManager saveManager = SaveManager.EnsureInstance(); // Resume 결과를 즉시 기록할 통합 저장 매니저
        saveManager.MarkDirty();
        bool saved = saveManager.SaveGame(); // 갱신된 재화와 처리 시각의 디스크 저장 성공 여부
        if (!saved) Debug.LogWarning("[Save] Background Resume 보상 저장에 실패했습니다. 다음 저장에서 다시 시도합니다.");
        if (rewardGranted) stateChanged?.Invoke();
    }

    // 현재 재화에서 요청 금액을 한 번만 차감합니다.
    public bool TrySpendCurrency(int amount)
    {
        if (_state == null || amount < 0 || _state.currency < amount) return false;
        _state.currency -= amount;
        SaveAndNotify(true);
        return true;
    }

    // 테스트용 재화를 지급합니다.
    [ContextMenu("테스트 재화 지급")]
    private void GrantTestCurrency()
    {
        AddCurrency(startingCurrency);
    }

    // 기존 키의 업그레이드 저장을 초기 상태로 되돌립니다.
    [ContextMenu("업그레이드 저장 초기화")]
    private void ResetSavedState()
    {
        SaveManager.Instance.ResetSave();
    }

    // 현재 가챠 표에서 1회 뽑고 결과를 영구 저장합니다.
    public bool TryDrawOne(out GachaDrawResult result)
    {
        result = default;
        if (!CanUseBalance() || !HasUnlockedDrawPool() || _state.currency < GetCurrentDrawCost()) return false;
        result = ExecuteOneDraw();
        SaveAndNotify(true);
        drawCompleted?.Invoke(new[] { result });
        return true;
    }

    // 레벨 상승에 따른 비용까지 반영해 10회를 순서대로 뽑습니다.
    public bool TryDrawTen(out IReadOnlyList<GachaDrawResult> results)
    {
        results = null;
        if (!CanUseBalance() || !HasUnlockedDrawPool() || _state.currency < GetDrawCostForCount(10)) return false;
        List<GachaDrawResult> values = new(10); // 이번 10회 뽑기 결과
        for (int index = 0; index < 10; index++) values.Add(ExecuteOneDraw()); // 뽑기 순번
        SaveAndNotify(true);
        results = values;
        drawCompleted?.Invoke(values);
        return true;
    }

    // 원본 누적값은 유지하고 지정 스탯의 연구 레벨만 올립니다.
    public bool TryUpgradeResearch(UpgradeStatType type)
    {
        UpgradeStatDefinition definition = balanceSettings == null ? null : balanceSettings.GetStat(type); // 연구할 스탯 정의
        if (definition == null || !IsUnlocked(type)) return false;
        UpgradeStatValue value = GetValue(type); // 저장된 스탯 값
        int cost = GetResearchCost(type); // 현재 연구 비용
        if (_state.currency < cost) return false;
        _state.currency -= cost;
        value.researchLevel++;
        SaveAndNotify(true);
        return true;
    }

    // 현재 가챠 레벨의 1회 비용을 반환합니다.
    public int GetCurrentDrawCost()
    {
        GachaLevelDefinition level = balanceSettings == null || _state == null ? null : balanceSettings.GetGachaLevel(_state.gachaLevel); // 현재 가챠 정의
        return level == null ? 0 : level.drawCost;
    }

    // 레벨 상승 중 비용 변화까지 포함한 정확한 연속 뽑기 비용을 반환합니다.
    public int GetDrawCostForCount(int count)
    {
        if (count <= 0 || !CanUseBalance()) return 0;
        int level = _state.gachaLevel; // 계산 중 가챠 레벨
        int progress = _state.drawsAtCurrentLevel; // 계산 중 레벨 진행도
        long total = 0; // 오버플로를 막는 누적 비용
        for (int index = 0; index < count; index++) // 계산할 뽑기 순번
        {
            GachaLevelDefinition definition = balanceSettings.GetGachaLevel(level); // 계산 중 레벨 정의
            if (definition == null) return (int)Math.Min(int.MaxValue, total);
            total = Math.Min(int.MaxValue, total + definition.drawCost);
            progress++;
            if (definition.drawsToNextLevel > 0 && progress >= definition.drawsToNextLevel &&
                balanceSettings.GetGachaLevel(level + 1) != null)
            {
                level++;
                progress = 0;
            }
        }

        return (int)total;
    }

    // 현재 연구 레벨의 다음 비용을 반환합니다.
    public int GetResearchCost(UpgradeStatType type)
    {
        UpgradeStatDefinition definition = balanceSettings == null ? null : balanceSettings.GetStat(type); // 연구 비용 정의
        if (definition == null) return 0;
        double cost = definition.researchBaseCost * Math.Pow(definition.researchCostGrowth, GetValue(type).researchLevel); // 반올림 전 연구 비용
        return cost >= int.MaxValue ? int.MaxValue : Mathf.CeilToInt((float)cost);
    }

    // 현재 가챠 레벨에서 스탯이 해금됐는지 확인합니다.
    public bool IsUnlocked(UpgradeStatType type)
    {
        return CanUseBalance() && GetUnlockedStats().Contains(type);
    }

    // 현재 가챠 레벨까지 해금된 중복 없는 스탯 목록을 반환합니다.
    public List<UpgradeStatType> GetUnlockedStats()
    {
        List<UpgradeStatType> result = new(); // 해금 스탯 결과
        if (!CanUseBalance()) return result;
        for (int level = 1; level <= _state.gachaLevel; level++) // 확인할 가챠 레벨
        {
            GachaLevelDefinition definition = balanceSettings.GetGachaLevel(level); // 레벨별 해금 정의
            if (definition?.newlyUnlockedStats == null) continue;
            foreach (UpgradeStatType stat in definition.newlyUnlockedStats) // 새 해금 스탯
                if (!result.Contains(stat)) result.Add(stat);
        }

        return result;
    }

    // 전투 시스템이 그대로 사용하는 최종 스탯 Snapshot API입니다.
    public UpgradeStatSnapshot GetStatSnapshot(UpgradeStatType type)
    {
        UpgradeStatDefinition definition = balanceSettings == null ? null : balanceSettings.GetStat(type); // 요청 스탯 정의
        UpgradeStatValue value = GetValue(type); // 요청 스탯 저장값
        if (definition == null)
            return new UpgradeStatSnapshot(type, value.accumulatedValue, value.accumulatedValue, value.researchLevel, 0f, 0f);
        float efficiency = definition.baseCoefficient * GetResearchMultiplier(definition, value.researchLevel); // 수치 1당 효율
        float effective = value.accumulatedValue; // 전역 증폭 적용 전 유효 누적값
        if (type != UpgradeStatType.StatIncrease) effective *= 1f + GetStatIncreaseEffect();
        return new UpgradeStatSnapshot(type, value.accumulatedValue, effective, value.researchLevel, efficiency, effective * efficiency);
    }

    // 현재 초당 재화 생산량을 반환합니다.
    public float GetCurrencyPerSecond()
    {
        if (currencyUpgradeBalance == null) return 0f;
        CurrencyUpgradeDefinition definition = currencyUpgradeBalance.GetDefinition(CurrencyUpgradeType.CurrencyPerSecond); // 초당 재화 정의
        float added = definition == null ? 0f : definition.valuePerLevel * GetCurrencyUpgradeLevel(CurrencyUpgradeType.CurrencyPerSecond); // 강화 누적 증가량
        return Mathf.Max(0f, currencyUpgradeBalance.baseCurrencyPerSecond + added);
    }

    // 인간 사망 한 건에 해당하는 추가 재화를 한 번 지급합니다.
    public int GrantHumanKillBonus(UnitData defeatedUnit)
    {
        if (defeatedUnit == null || defeatedUnit.Team != UnitTeam.Human || currencyUpgradeBalance == null) return 0;
        CurrencyUpgradeDefinition definition = currencyUpgradeBalance.GetDefinition(CurrencyUpgradeType.HumanKillBonus); // 인간 처치 보너스 정의
        int bonus = definition == null ? 0 : Mathf.Max(0, Mathf.RoundToInt(definition.valuePerLevel * GetCurrencyUpgradeLevel(CurrencyUpgradeType.HumanKillBonus))); // 지급할 고정 보너스
        if (bonus > 0) AddCurrency(bonus);
        return bonus;
    }

    // 지정한 재화 강화의 계산 완료 상태를 반환합니다.
    public CurrencyUpgradeSnapshot GetCurrencyUpgradeSnapshot(CurrencyUpgradeType type)
    {
        CurrencyUpgradeDefinition definition = currencyUpgradeBalance == null ? null : currencyUpgradeBalance.GetDefinition(type); // 요청한 강화 정의
        if (definition == null) return new CurrencyUpgradeSnapshot(type, type.ToString(), string.Empty, 0, 0, 0, 0f, 0f);
        int level = Mathf.Clamp(GetCurrencyUpgradeLevel(type), 0, definition.maxLevel); // 현재 유효 레벨
        float currentEffect = GetDisplayedCurrencyEffect(type, level); // 기본값과 상한까지 반영한 현재 효과
        float nextEffect = GetDisplayedCurrencyEffect(type, Mathf.Min(level + 1, definition.maxLevel)); // 기본값과 상한까지 반영한 다음 효과
        int cost = level >= definition.maxLevel ? 0 : CalculateCurrencyUpgradeCost(definition, level); // 다음 강화 비용
        return new CurrencyUpgradeSnapshot(type, definition.displayName, definition.description, level,
            definition.maxLevel, cost, currentEffect, nextEffect);
    }

    // 비용과 최대 레벨을 확인한 뒤 재화 강화를 한 단계 올립니다.
    public bool TryUpgradeCurrency(CurrencyUpgradeType type)
    {
        CurrencyUpgradeDefinition definition = currencyUpgradeBalance == null ? null : currencyUpgradeBalance.GetDefinition(type); // 강화할 정의
        if (definition == null) return false;
        int level = GetCurrencyUpgradeLevel(type); // 강화 전 레벨
        if (level >= definition.maxLevel) return false;
        int cost = CalculateCurrencyUpgradeCost(definition, level); // 정확히 한 번 차감할 비용
        if (_state == null || _state.currency < cost) return false;
        _state.currency -= cost;
        SetCurrencyUpgradeLevel(type, level + 1);
        SaveAndNotify(true);
        return true;
    }

    // 대기 중인 오프라인 보상 결과를 UI가 한 번만 가져가게 합니다.
    public bool TryConsumeOfflineReward(out OfflineCurrencyReward reward)
    {
        reward = _pendingOfflineReward;
        if (!_hasPendingOfflineReward) return false;
        _hasPendingOfflineReward = false;
        return reward.EarnedCurrency > 0;
    }

    // 입력한 경과 초에 현재 상한과 효율을 적용한 오프라인 보상을 계산합니다.
    public OfflineCurrencyReward CalculateOfflineReward(double actualSeconds)
    {
        double safeActualSeconds = double.IsNaN(actualSeconds) || double.IsInfinity(actualSeconds) || actualSeconds <= 0d ? 0d : actualSeconds; // 검증된 실제 경과 초
        double appliedSeconds = Math.Min(safeActualSeconds, GetOfflineMaxSeconds()); // 적립 상한 적용 초
        float efficiency = GetOfflineEfficiency(); // 현재 오프라인 효율
        double rawReward = GetCurrencyPerSecond() * appliedSeconds * efficiency; // 반올림 전 보상
        int earnedCurrency = rawReward <= 0d ? 0 : (int)Math.Min(int.MaxValue, Math.Floor(rawReward)); // 지급할 정수 보상
        return new OfflineCurrencyReward(safeActualSeconds, appliedSeconds, efficiency, earnedCurrency);
    }

    // 현재 가챠 1회를 실행하고 저장 전 결과를 만듭니다.
    private GachaDrawResult ExecuteOneDraw()
    {
        int cost = GetCurrentDrawCost(); // 이번 뽑기 비용
        _state.currency -= cost;
        List<UpgradeStatType> pool = GetUnlockedStats(); // 현재 해금된 뽑기 풀
        UpgradeStatType type = pool[UnityEngine.Random.Range(0, pool.Count)]; // 당첨 스탯
        int amount = UnityEngine.Random.Range(1, 11); // 당첨 수치
        UpgradeStatValue value = GetValue(type); // 당첨 스탯 저장값
        value.accumulatedValue += amount;
        bool increased = AdvanceGachaLevel(); // 레벨 상승 여부
        return new GachaDrawResult(type, amount, value.accumulatedValue, increased, _state.gachaLevel);
    }

    // 현재 진행도를 올리고 조건을 만족하면 가챠 레벨을 상승시킵니다.
    private bool AdvanceGachaLevel()
    {
        GachaLevelDefinition definition = balanceSettings.GetGachaLevel(_state.gachaLevel); // 현재 가챠 정의
        if (definition == null || definition.drawsToNextLevel <= 0) return false;
        _state.drawsAtCurrentLevel++;
        if (_state.drawsAtCurrentLevel < definition.drawsToNextLevel || balanceSettings.GetGachaLevel(_state.gachaLevel + 1) == null) return false;
        _state.gachaLevel++;
        _state.drawsAtCurrentLevel = 0;
        return true;
    }

    // 자기 자신을 제외한 다른 스탯에 적용할 전역 증폭값을 반환합니다.
    private float GetStatIncreaseEffect()
    {
        UpgradeStatSnapshot increase = GetStatSnapshotWithoutAmplifier(UpgradeStatType.StatIncrease); // 전역 증폭 자체 스냅샷
        return increase.FinalBonus;
    }

    // 전역 증폭을 재귀 적용하지 않은 스탯 스냅샷을 반환합니다.
    private UpgradeStatSnapshot GetStatSnapshotWithoutAmplifier(UpgradeStatType type)
    {
        UpgradeStatDefinition definition = balanceSettings == null ? null : balanceSettings.GetStat(type); // 요청 스탯 정의
        UpgradeStatValue value = GetValue(type); // 요청 스탯 저장값
        if (definition == null)
            return new UpgradeStatSnapshot(type, value.accumulatedValue, value.accumulatedValue, value.researchLevel, 0f, 0f);
        float efficiency = definition.baseCoefficient * GetResearchMultiplier(definition, value.researchLevel); // 연구 적용 효율
        return new UpgradeStatSnapshot(type, value.accumulatedValue, value.accumulatedValue, value.researchLevel,
            efficiency, value.accumulatedValue * efficiency);
    }

    // 연구 레벨에 따른 완만한 효율 배율을 계산합니다.
    private static float GetResearchMultiplier(UpgradeStatDefinition definition, int level)
    {
        return 1f + definition.researchMaxMultiplierBonus * (1f - Mathf.Exp(-definition.researchCurveRate * level));
    }

    // 저장 상태에서 지정한 스탯 값을 찾거나 기본값으로 추가합니다.
    private UpgradeStatValue GetValue(UpgradeStatType type)
    {
        if (_state == null) _state = CreateInitialState();
        if (_state.stats == null) _state.stats = new List<UpgradeStatValue>();
        string targetStatId = GetStableStatId(type); // 찾을 스탯의 안정적인 저장 ID
        foreach (UpgradeStatValue item in _state.stats) // 저장된 스탯 항목
        {
            if (item == null || (!string.Equals(item.statId, targetStatId, StringComparison.Ordinal) &&
                !(string.IsNullOrWhiteSpace(item.statId) && item.statType == type))) continue;
            item.statId = targetStatId;
            item.statType = type;
            return item;
        }

        UpgradeStatValue added = new() { statId = targetStatId, statType = type }; // 새로 추가할 스탯 값
        _state.stats.Add(added);
        return added;
    }

    // 가챠 밸런스와 런타임 상태가 사용 가능한지 확인합니다.
    private bool CanUseBalance()
    {
        return _state != null && balanceSettings != null && balanceSettings.GetGachaLevel(1) != null;
    }

    // 현재 해금된 가챠 결과가 하나 이상인지 확인합니다.
    private bool HasUnlockedDrawPool()
    {
        return GetUnlockedStats().Count > 0;
    }

    // 통합 저장과 Legacy PlayerPrefs 중 안전한 초기 Provider 경로를 선택합니다.
    private void InitializeSaveProvider()
    {
        SaveManager saveManager = SaveManager.EnsureInstance(); // 업그레이드 Provider를 등록할 전역 저장 매니저
        bool hasUnifiedData = saveManager.HasProviderData(ProviderKey); // 디스크에서 읽은 통합 Upgrade Section 존재 여부
        if (hasUnifiedData)
        {
            _state = CreateInitialState();
            bool restoredFromSave; // 통합 Upgrade Section의 실제 복원 성공 여부
            bool unifiedProviderRegistered = saveManager.RegisterProvider(this, true, out restoredFromSave); // 통합 Section 경로의 Provider 등록 성공 여부
            if (!unifiedProviderRegistered)
            {
                Debug.LogError("[Save] Upgrade Provider 등록에 실패했습니다.");
                return;
            }

            if (restoredFromSave)
            {
                _legacyMigrationPending = false;
                DeleteLegacySave();
                return;
            }

            if (PlayerPrefs.HasKey(LegacyPlayerPrefsKey))
            {
                _legacyMigrationPending = true;
                if (!TryImportLegacyState())
                {
                    _state = CreateInitialState();
                    saveManager.UnregisterProvider(this);
                    Debug.LogError("[Save] 손상된 통합 데이터와 Legacy Migration 실패가 함께 발생하여 기존 PlayerPrefs를 유지하고 Provider 등록을 중단했습니다.");
                    return;
                }

                CompleteLegacyMigration(saveManager);
                return;
            }

            _legacyMigrationPending = false;
            ResetSaveData();
            saveManager.MarkDirty();
            if (!saveManager.SaveGame()) Debug.LogWarning("[Save] 손상된 Upgrade Section의 기본값 저장에 실패했습니다. 다음 저장에서 다시 시도합니다.");
            return;
        }

        if (!PlayerPrefs.HasKey(LegacyPlayerPrefsKey))
        {
            saveManager.RegisterProvider(this);
            return;
        }

        _legacyMigrationPending = true;
        if (!TryImportLegacyState())
        {
            _state = CreateInitialState();
            Debug.LogError("[Save] Legacy Migration에 실패하여 기존 PlayerPrefs를 유지하고 Upgrade Provider 등록을 중단했습니다.");
            return;
        }

        bool registered = saveManager.RegisterProvider(this, true); // 가져온 Legacy 원본을 유지한 Provider 등록 성공 여부
        if (!registered)
        {
            Debug.LogError("[Save] Legacy Upgrade Provider 등록에 실패했습니다. 기존 PlayerPrefs를 유지합니다.");
            return;
        }

        CompleteLegacyMigration(saveManager);
    }

    // 가져온 Legacy 원본을 먼저 통합 파일에 기록한 뒤에만 PlayerPrefs를 삭제합니다.
    private void CompleteLegacyMigration(SaveManager saveManager)
    {
        saveManager.MarkDirty();
        bool unifiedSaved = saveManager.SaveGame(); // Legacy 원본의 통합 파일 기록 성공 여부
        if (!unifiedSaved)
        {
            Debug.LogError("[Save] Legacy Migration 파일 저장에 실패했습니다. 기존 PlayerPrefs를 유지합니다.");
            return;
        }

        _legacyMigrationPending = false;
        DeleteLegacySave();
        Debug.Log("[Save] Legacy Upgrade 데이터를 통합 저장으로 안전하게 이전했습니다.");
        ProcessOfflineReward();
        saveManager.MarkDirty();
        bool rewardSaved = saveManager.SaveGame(); // 이전 직후 오프라인 보상과 처리 시각 저장 성공 여부
        if (!rewardSaved) Debug.LogWarning("[Save] Legacy 이전 후 오프라인 보상 저장에 실패했습니다. 다음 저장에서 다시 시도합니다.");
        stateChanged?.Invoke();
    }

    // 현재 통합 저장 파일을 다시 읽어 업그레이드 상태에 적용합니다.
    private void LoadState()
    {
        SaveManager saveManager = SaveManager.EnsureInstance(); // 수동 재로드를 처리할 통합 저장 매니저
        saveManager.LoadGame();
    }

    // 현재 업그레이드 영구 원본 DTO를 반환합니다.
    public object CaptureSaveData()
    {
        NormalizeState();
        return _state;
    }

    // 통합 저장에서 읽은 업그레이드 DTO를 런타임 원본에 적용합니다.
    public void RestoreSaveData(object data)
    {
        UpgradeState restoredState = data as UpgradeState; // 통합 저장에서 역직렬화한 Upgrade Provider DTO
        if (!TryMigrateUpgradeState(restoredState)) throw new InvalidOperationException("지원하지 않는 Upgrade Provider 저장 버전입니다.");
        _state = restoredState;
        _legacyMigrationPending = false;
        _pendingOfflineReward = default;
        _hasPendingOfflineReward = false;
        _isInBackground = false;
        _lastResumeProcessFrame = -1;
        NormalizeState();
        ProcessOfflineReward();
        SaveManager.Instance.MarkDirty();
        stateChanged?.Invoke();
    }

    // 업그레이드 영구 원본을 새 게임 기본값으로 되돌립니다.
    public void ResetSaveData()
    {
        _state = CreateInitialState();
        _legacyMigrationPending = false;
        _currencyProductionSeconds = 0f;
        _currencyProductionRemainder = 0f;
        _pendingOfflineReward = default;
        _hasPendingOfflineReward = false;
        _isInBackground = false;
        _lastResumeProcessFrame = -1;
        stateChanged?.Invoke();
    }

    // 모든 디스크 저장 직전에 마지막 활동 UTC를 현재 시각으로 갱신합니다.
    public bool PrepareSaveData()
    {
        if (_state == null || _legacyMigrationPending) return false;
        string currentUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture); // 이번 저장에 기록할 현재 UTC
        if (string.Equals(_state.lastActivityUtc, currentUtc, StringComparison.Ordinal)) return false;
        _state.lastActivityUtc = currentUtc;
        return true;
    }

    // 전체 Reset에서 재이전을 막기 위해 Legacy PlayerPrefs를 삭제합니다.
    public void ClearLegacySaveData()
    {
        _legacyMigrationPending = false;
        DeleteLegacySave();
    }

    // 기존 PlayerPrefs JSON을 통합 저장으로 한 번만 가져옵니다.
    private bool TryImportLegacyState()
    {
        if (!PlayerPrefs.HasKey(LegacyPlayerPrefsKey)) return false;

        try
        {
            UpgradeState legacyState = JsonUtility.FromJson<UpgradeState>(PlayerPrefs.GetString(LegacyPlayerPrefsKey)); // 기존 PlayerPrefs에서 읽은 업그레이드 DTO
            if (!TryMigrateUpgradeState(legacyState)) return false;
            _state = legacyState;
            NormalizeState();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Save] 기존 PlayerPrefs 업그레이드 데이터 이전 실패: {exception.Message}");
            return false;
        }
    }

    // 이전이 끝난 기존 PlayerPrefs 저장 키를 삭제합니다.
    private static void DeleteLegacySave()
    {
        if (!PlayerPrefs.HasKey(LegacyPlayerPrefsKey)) return;
        PlayerPrefs.DeleteKey(LegacyPlayerPrefsKey);
        PlayerPrefs.Save();
    }

    // 누락되거나 손상된 업그레이드 원본 값을 안전 범위로 보정합니다.
    private void NormalizeState()
    {
        if (_state == null) _state = CreateInitialState();
        _state.version = CurrentUpgradeSaveVersion;
        _state.currency = Mathf.Max(0, _state.currency);
        _state.gachaLevel = Mathf.Clamp(_state.gachaLevel, 1, GetMaximumGachaLevel());
        _state.drawsAtCurrentLevel = Mathf.Max(0, _state.drawsAtCurrentLevel);
        GachaLevelDefinition currentGacha = balanceSettings == null ? null : balanceSettings.GetGachaLevel(_state.gachaLevel); // 진행도를 검증할 현재 가챠 정의
        if (currentGacha != null && currentGacha.drawsToNextLevel > 0)
            _state.drawsAtCurrentLevel = Mathf.Min(_state.drawsAtCurrentLevel, currentGacha.drawsToNextLevel - 1);
        _state.currencyPerSecondLevel = ClampCurrencyUpgradeLevel(CurrencyUpgradeType.CurrencyPerSecond, _state.currencyPerSecondLevel);
        _state.humanKillBonusLevel = ClampCurrencyUpgradeLevel(CurrencyUpgradeType.HumanKillBonus, _state.humanKillBonusLevel);
        _state.offlineMaxTimeLevel = ClampCurrencyUpgradeLevel(CurrencyUpgradeType.OfflineMaxTime, _state.offlineMaxTimeLevel);
        _state.offlineEfficiencyLevel = ClampCurrencyUpgradeLevel(CurrencyUpgradeType.OfflineEfficiency, _state.offlineEfficiencyLevel);
        if (_state.stats == null) _state.stats = new List<UpgradeStatValue>();

        foreach (UpgradeStatValue value in _state.stats) // 보정할 저장 스탯 원본
        {
            if (value == null) continue;
            if (string.IsNullOrWhiteSpace(value.statId)) value.statId = GetStableStatId(value.statType);
            else if (TryGetStatType(value.statId, out UpgradeStatType restoredType)) value.statType = restoredType;
            value.accumulatedValue = Mathf.Max(0, value.accumulatedValue);
            value.researchLevel = Mathf.Max(0, value.researchLevel);
        }
    }

    // Upgrade Provider 버전을 현재 내부 형식으로 올릴 수 있는지 확인합니다.
    private static bool TryMigrateUpgradeState(UpgradeState state)
    {
        if (state == null) return false;
        if (state.version > CurrentUpgradeSaveVersion)
        {
            Debug.LogError($"[Save] 지원하지 않는 Upgrade Provider 버전입니다: {state.version}");
            return false;
        }

        if (state.version <= 0) state.version = 1;
        if (state.version == 1) state.version = 2;
        return state.version == CurrentUpgradeSaveVersion;
    }

    // 현재 Balance에 존재하는 가장 높은 가챠 레벨을 반환합니다.
    private int GetMaximumGachaLevel()
    {
        if (balanceSettings == null || balanceSettings.GachaLevels == null || balanceSettings.GachaLevels.Count == 0) return int.MaxValue;
        int maximumLevel = 1; // 손상된 큰 레벨을 제한할 현재 최대 가챠 레벨
        foreach (GachaLevelDefinition definition in balanceSettings.GachaLevels) // 최대 레벨을 확인할 가챠 정의
        {
            if (definition != null) maximumLevel = Mathf.Max(maximumLevel, definition.level);
        }

        return maximumLevel;
    }

    // 재화 강화 레벨을 0과 Balance 최대 레벨 사이로 제한합니다.
    private int ClampCurrencyUpgradeLevel(CurrencyUpgradeType type, int level)
    {
        CurrencyUpgradeDefinition definition = currencyUpgradeBalance == null ? null : currencyUpgradeBalance.GetDefinition(type); // 최대 레벨을 제공할 재화 강화 정의
        int maximumLevel = definition == null ? int.MaxValue : Mathf.Max(0, definition.maxLevel); // 적용 가능한 재화 강화 최대 레벨
        return Mathf.Clamp(level, 0, maximumLevel);
    }

    // 스탯 enum과 무관하게 유지되는 저장 ID를 반환합니다.
    private static string GetStableStatId(UpgradeStatType type)
    {
        return type switch
        {
            UpgradeStatType.Health => "stat_health",
            UpgradeStatType.Defense => "stat_defense",
            UpgradeStatType.Attack => "stat_attack",
            UpgradeStatType.InfectionCount => "stat_infection_count",
            UpgradeStatType.MoveSpeed => "stat_move_speed",
            UpgradeStatType.AttackSpeed => "stat_attack_speed",
            UpgradeStatType.ZombieCount => "stat_zombie_count",
            UpgradeStatType.StatIncrease => "stat_global_increase",
            UpgradeStatType.CriticalChance => "stat_critical_chance",
            UpgradeStatType.CriticalDamage => "stat_critical_damage",
            _ => "stat_unknown"
        };
    }

    // 저장 ID를 현재 스탯 enum으로 안전하게 변환합니다.
    private static bool TryGetStatType(string statId, out UpgradeStatType type)
    {
        switch (statId)
        {
            case "stat_health": type = UpgradeStatType.Health; return true;
            case "stat_defense": type = UpgradeStatType.Defense; return true;
            case "stat_attack": type = UpgradeStatType.Attack; return true;
            case "stat_infection_count": type = UpgradeStatType.InfectionCount; return true;
            case "stat_move_speed": type = UpgradeStatType.MoveSpeed; return true;
            case "stat_attack_speed": type = UpgradeStatType.AttackSpeed; return true;
            case "stat_zombie_count": type = UpgradeStatType.ZombieCount; return true;
            case "stat_global_increase": type = UpgradeStatType.StatIncrease; return true;
            case "stat_critical_chance": type = UpgradeStatType.CriticalChance; return true;
            case "stat_critical_damage": type = UpgradeStatType.CriticalDamage; return true;
            default: type = default; return false;
        }
    }

    // 이전 저장과 호환되는 초기 상태를 생성합니다.
    private UpgradeState CreateInitialState()
    {
        return new UpgradeState { currency = startingCurrency };
    }

    // 마지막 UTC부터 현재까지의 오프라인 보상을 처리하고 실제 지급 여부를 반환합니다.
    private bool ProcessOfflineReward()
    {
        string previousUtc = _state.lastActivityUtc; // 저장된 마지막 활동 시각
        DateTime nowUtc = DateTime.UtcNow; // 이번 처리 기준 UTC
        _state.lastActivityUtc = nowUtc.ToString("O", CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(previousUtc) || currencyUpgradeBalance == null) return false;

        bool parsed = DateTime.TryParseExact(previousUtc, "O", CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out DateTime savedUtc); // UTC 파싱 성공 여부
        if (!parsed) return false;
        OfflineCurrencyReward reward = CalculateOfflineReward((nowUtc - savedUtc.ToUniversalTime()).TotalSeconds); // 이번 접속 보상
        if (reward.EarnedCurrency <= 0) return false;
        _state.currency = (int)Math.Min(int.MaxValue, (long)_state.currency + reward.EarnedCurrency);
        _pendingOfflineReward = reward;
        _hasPendingOfflineReward = true;
        offlineRewardGranted?.Invoke(reward);
        return true;
    }

    // 현재 강화 종류의 저장 레벨을 반환합니다.
    private int GetCurrencyUpgradeLevel(CurrencyUpgradeType type)
    {
        if (_state == null) return 0;
        return type switch
        {
            CurrencyUpgradeType.CurrencyPerSecond => _state.currencyPerSecondLevel,
            CurrencyUpgradeType.HumanKillBonus => _state.humanKillBonusLevel,
            CurrencyUpgradeType.OfflineMaxTime => _state.offlineMaxTimeLevel,
            CurrencyUpgradeType.OfflineEfficiency => _state.offlineEfficiencyLevel,
            _ => 0
        };
    }

    // 현재 강화 종류의 저장 레벨을 변경합니다.
    private void SetCurrencyUpgradeLevel(CurrencyUpgradeType type, int level)
    {
        int safeLevel = Mathf.Max(0, level); // 저장할 음수 아닌 레벨
        switch (type)
        {
            case CurrencyUpgradeType.CurrencyPerSecond: _state.currencyPerSecondLevel = safeLevel; break;
            case CurrencyUpgradeType.HumanKillBonus: _state.humanKillBonusLevel = safeLevel; break;
            case CurrencyUpgradeType.OfflineMaxTime: _state.offlineMaxTimeLevel = safeLevel; break;
            case CurrencyUpgradeType.OfflineEfficiency: _state.offlineEfficiencyLevel = safeLevel; break;
        }
    }

    // 지정 레벨의 누적 강화 효과를 반환합니다.
    private float GetCurrencyUpgradeTotalEffect(CurrencyUpgradeType type, int level)
    {
        CurrencyUpgradeDefinition definition = currencyUpgradeBalance == null ? null : currencyUpgradeBalance.GetDefinition(type); // 강화 효과 정의
        return definition == null ? 0f : Mathf.Max(0, level) * definition.valuePerLevel;
    }

    // UI에 표시할 기본값과 상한이 반영된 최종 효과를 계산합니다.
    private float GetDisplayedCurrencyEffect(CurrencyUpgradeType type, int level)
    {
        if (currencyUpgradeBalance == null) return 0f;
        float added = GetCurrencyUpgradeTotalEffect(type, level); // 지정 레벨의 강화 증가량
        return type switch
        {
            CurrencyUpgradeType.CurrencyPerSecond => currencyUpgradeBalance.baseCurrencyPerSecond + added,
            CurrencyUpgradeType.HumanKillBonus => added,
            CurrencyUpgradeType.OfflineMaxTime => currencyUpgradeBalance.baseOfflineMaxHours + added,
            CurrencyUpgradeType.OfflineEfficiency => Mathf.Min(currencyUpgradeBalance.maximumOfflineEfficiency,
                currencyUpgradeBalance.baseOfflineEfficiency + added),
            _ => added
        };
    }

    // 지수 비용을 올림하고 int 범위로 보호합니다.
    private static int CalculateCurrencyUpgradeCost(CurrencyUpgradeDefinition definition, int currentLevel)
    {
        double cost = definition.baseCost * Math.Pow(definition.costGrowth, Math.Max(0, currentLevel)); // 올림 전 지수 비용
        if (double.IsNaN(cost) || double.IsInfinity(cost) || cost >= int.MaxValue) return int.MaxValue;
        return Math.Max(0, (int)Math.Ceiling(cost));
    }

    // 현재 오프라인 최대 적립 초를 반환합니다.
    private float GetOfflineMaxSeconds()
    {
        if (currencyUpgradeBalance == null) return 0f;
        float totalHours = currencyUpgradeBalance.baseOfflineMaxHours +
            GetCurrencyUpgradeTotalEffect(CurrencyUpgradeType.OfflineMaxTime, GetCurrencyUpgradeLevel(CurrencyUpgradeType.OfflineMaxTime)); // 데이터의 시간 단위 최대 적립량
        return Mathf.Max(0f, totalHours * 3600f);
    }

    // 상한이 적용된 현재 오프라인 적립 효율을 반환합니다.
    private float GetOfflineEfficiency()
    {
        if (currencyUpgradeBalance == null) return 0f;
        float efficiency = currencyUpgradeBalance.baseOfflineEfficiency +
            GetCurrencyUpgradeTotalEffect(CurrencyUpgradeType.OfflineEfficiency, GetCurrencyUpgradeLevel(CurrencyUpgradeType.OfflineEfficiency)); // 상한 적용 전 효율
        return Mathf.Clamp(efficiency, 0f, currencyUpgradeBalance.maximumOfflineEfficiency);
    }

    // 현재 상태를 Dirty 처리하고 필요하면 UI에 알립니다.
    private void SaveState(bool notify)
    {
        if (_state == null) return;
        SaveManager.Instance.MarkDirty();
        if (notify) stateChanged?.Invoke();
    }

    // 상태 변경을 알리고 중요한 진행이면 즉시 파일에 저장합니다.
    private void SaveAndNotify(bool saveImmediately)
    {
        SaveState(true);
        if (saveImmediately) SaveManager.Instance.SaveGame();
    }

    // 파괴되는 인스턴스의 Provider 등록을 해제합니다.
    protected override void OnDestroy()
    {
        if (SaveManager.HasInstance) SaveManager.Instance.UnregisterProvider(this);
        base.OnDestroy();
    }
}
