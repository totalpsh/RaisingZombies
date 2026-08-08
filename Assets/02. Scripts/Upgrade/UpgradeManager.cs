using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 가챠, 연구, 재화 강화, 저장과 최종 스탯 조회를 담당합니다.
public sealed class UpgradeManager : Singleton<UpgradeManager>
{
    private const string SaveKey = "RaisingZombies.Upgrade.State"; // 기존 PlayerPrefs 저장 키
    [SerializeField] private UpgradeBalanceSettings balanceSettings; // 좀비 가챠 및 연구 밸런스
    [SerializeField] private CurrencyUpgradeBalanceSettings currencyUpgradeBalance; // 재화 강화 및 오프라인 보상 밸런스
    [SerializeField, Min(0)] private int startingCurrency = 10000; // 최초 저장 생성 시 지급할 테스트 재화
    private UpgradeState _state; // 현재 저장 상태
    private float _currencyProductionSeconds; // 다음 정수 초 지급까지 누적한 게임 시간
    private float _currencyProductionRemainder; // 정수로 지급하지 못한 재화 소수 잔여량
    private OfflineCurrencyReward _pendingOfflineReward; // 아직 UI가 소비하지 않은 오프라인 보상 결과
    private bool _hasPendingOfflineReward; // 오프라인 결과가 표시 대기 중인지 여부
    private int _lastActivitySaveFrame = -1; // 같은 프레임의 Pause와 Focus 중복 저장 방지

    public event Action stateChanged; // 저장 상태 변경 이벤트
    public event Action<IReadOnlyList<GachaDrawResult>> drawCompleted; // 가챠 완료 이벤트
    public event Action<OfflineCurrencyReward> offlineRewardGranted; // 오프라인 보상 실제 지급 이벤트
    public int Currency => _state == null ? 0 : _state.currency;
    public int GachaLevel => _state == null ? 1 : _state.gachaLevel;
    public int DrawsAtCurrentLevel => _state == null ? 0 : _state.drawsAtCurrentLevel;
    public UpgradeBalanceSettings BalanceSettings => balanceSettings;
    public CurrencyUpgradeBalanceSettings CurrencyUpgradeBalance => currencyUpgradeBalance;

    // 싱글턴을 등록하고 기존 저장 및 오프라인 보상을 불러옵니다.
    protected override void Awake()
    {
        base.Awake();
        if (currencyUpgradeBalance == null)
            Debug.LogError("[UpgradeManager] CurrencyUpgradeBalanceSettings 참조가 없습니다. 재화 생산과 오프라인 보상이 비활성화됩니다.", this);
        LoadState();
    }

    // Time.timeScale 영향을 받는 초 단위 재화를 지급합니다.
    private void Update()
    {
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

    // 앱이 백그라운드로 갈 때 마지막 활동 UTC를 저장합니다.
    private void OnApplicationPause(bool paused)
    {
        if (paused) SaveLastActivityUtc();
    }

    // 앱 포커스를 잃을 때 마지막 활동 UTC를 저장합니다.
    private void OnApplicationFocus(bool focused)
    {
        if (!focused) SaveLastActivityUtc();
    }

    // 앱 종료 시 마지막 활동 UTC를 저장합니다.
    private void OnApplicationQuit()
    {
        SaveLastActivityUtc();
    }

    // 양수 재화를 안전하게 더하고 즉시 저장합니다.
    public void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        if (_state == null) _state = CreateInitialState();
        _state.currency = (int)Math.Min(int.MaxValue, (long)_state.currency + amount);
        SaveAndNotify();
    }

    // 현재 재화에서 요청 금액을 한 번만 차감합니다.
    public bool TrySpendCurrency(int amount)
    {
        if (_state == null || amount < 0 || _state.currency < amount) return false;
        _state.currency -= amount;
        SaveAndNotify();
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
        PlayerPrefs.DeleteKey(SaveKey);
        _state = CreateInitialState();
        SaveAndNotify();
    }

    // 현재 가챠 표에서 1회 뽑고 결과를 영구 저장합니다.
    public bool TryDrawOne(out GachaDrawResult result)
    {
        result = default;
        if (!CanUseBalance() || !HasUnlockedDrawPool() || _state.currency < GetCurrentDrawCost()) return false;
        result = ExecuteOneDraw();
        SaveAndNotify();
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
        SaveAndNotify();
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
        SaveAndNotify();
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
        SaveAndNotify();
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
        foreach (UpgradeStatValue item in _state.stats) // 저장된 스탯 항목
            if (item != null && item.statType == type) return item;
        UpgradeStatValue added = new() { statType = type }; // 새로 추가할 스탯 값
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

    // 기존 키에서 상태를 불러오고 오프라인 보상을 정확히 한 번 처리합니다.
    private void LoadState()
    {
        _state = PlayerPrefs.HasKey(SaveKey) ? JsonUtility.FromJson<UpgradeState>(PlayerPrefs.GetString(SaveKey)) : CreateInitialState();
        if (_state == null) _state = CreateInitialState();
        if (_state.stats == null) _state.stats = new List<UpgradeStatValue>();
        ProcessOfflineReward();
        _state.version = 2;
        SaveAndNotify();
    }

    // 이전 저장과 호환되는 초기 상태를 생성합니다.
    private UpgradeState CreateInitialState()
    {
        return new UpgradeState { currency = startingCurrency };
    }

    // 마지막 UTC부터 현재까지의 오프라인 보상을 계산하고 즉시 처리 시각을 갱신합니다.
    private void ProcessOfflineReward()
    {
        string previousUtc = _state.lastActivityUtc; // 저장된 마지막 활동 시각
        DateTime nowUtc = DateTime.UtcNow; // 이번 처리 기준 UTC
        _state.lastActivityUtc = nowUtc.ToString("O", CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(previousUtc) || currencyUpgradeBalance == null) return;

        bool parsed = DateTime.TryParseExact(previousUtc, "O", CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out DateTime savedUtc); // UTC 파싱 성공 여부
        if (!parsed) return;
        OfflineCurrencyReward reward = CalculateOfflineReward((nowUtc - savedUtc.ToUniversalTime()).TotalSeconds); // 이번 접속 보상
        if (reward.EarnedCurrency <= 0) return;
        _state.currency = (int)Math.Min(int.MaxValue, (long)_state.currency + reward.EarnedCurrency);
        _pendingOfflineReward = reward;
        _hasPendingOfflineReward = true;
        offlineRewardGranted?.Invoke(reward);
    }

    // 마지막 활동 UTC를 한 경로에서 저장합니다.
    private void SaveLastActivityUtc()
    {
        if (_state == null || _lastActivitySaveFrame == Time.frameCount) return;
        _lastActivitySaveFrame = Time.frameCount;
        _state.lastActivityUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        SaveState(false);
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

    // 현재 상태를 기존 PlayerPrefs 키에 저장하고 필요하면 UI에 알립니다.
    private void SaveState(bool notify)
    {
        if (_state == null) return;
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_state));
        PlayerPrefs.Save();
        if (notify) stateChanged?.Invoke();
    }

    // 상태를 저장하고 구독 중인 UI를 갱신합니다.
    private void SaveAndNotify()
    {
        SaveState(true);
    }
}
