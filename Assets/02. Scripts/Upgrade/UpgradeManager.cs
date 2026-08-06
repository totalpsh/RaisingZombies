using System;
using System.Collections.Generic;
using UnityEngine;

//가챠, 연구, 저장과 최종 스탯 조회를 담당하는 독립 서비스입니다.
public sealed class UpgradeManager : Singleton<UpgradeManager>
{
    private const string SaveKey = "RaisingZombies.Upgrade.State";
    [SerializeField] private UpgradeBalanceSettings balanceSettings; // 전체 업그레이드 밸런스 에셋
    [SerializeField, Min(0)] private int startingCurrency = 10000; // 최초 저장 파일 생성 시 지급할 테스트 재화
    private UpgradeState _state;
    public event Action stateChanged;
    public event Action<IReadOnlyList<GachaDrawResult>> drawCompleted;
    public int Currency => _state == null ? 0 : _state.currency;
    public int GachaLevel => _state == null ? 1 : _state.gachaLevel;
    public int DrawsAtCurrentLevel => _state == null ? 0 : _state.drawsAtCurrentLevel;
    public UpgradeBalanceSettings BalanceSettings => balanceSettings;

    private void Awake()
    {
        LoadState();
    }

    private void OnValidate()
    {
        if (startingCurrency < 0) startingCurrency = 0;
    }

    public void AddCurrency(int amount)
    {
        if (amount <= 0) return;
        _state.currency += amount;
        SaveAndNotify();
    }

    [ContextMenu("테스트 재화 지급")]
    private void GrantTestCurrency()
    {
        AddCurrency(startingCurrency);
    }

    [ContextMenu("업그레이드 저장 초기화")]
    private void ResetSavedState()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        _state = CreateInitialState();
        SaveAndNotify();
    }

    //현재 가챠 풀에서 1회 뽑고 결과를 영구 누적합니다.
    public bool TryDrawOne(out GachaDrawResult result)
    {
        result = default;
        if (!CanUseBalance() || !HasUnlockedDrawPool() || _state.currency < GetCurrentDrawCost()) return false;
        result = ExecuteOneDraw();
        SaveAndNotify();
        drawCompleted?.Invoke(new[] { result });
        return true;
    }

    //레벨 상승과 비용 변경까지 사전 계산한 뒤 10회를 순차 실행합니다.
    public bool TryDrawTen(out IReadOnlyList<GachaDrawResult> results)
    {
        results = null;
        if (!CanUseBalance() || !HasUnlockedDrawPool() || _state.currency < GetDrawCostForCount(10)) return false;
        var values = new List<GachaDrawResult>(10);
        for (var i = 0; i < 10; i++) values.Add(ExecuteOneDraw());
        SaveAndNotify();
        results = values;
        drawCompleted?.Invoke(values);
        return true;
    }

    //원본 뽑기 수치는 유지한 채 해당 스탯의 연구 레벨만 올립니다.
    public bool TryUpgradeResearch(UpgradeStatType type)
    {
        var definition = balanceSettings == null ? null : balanceSettings.GetStat(type);
        if (definition == null || !IsUnlocked(type)) return false;
        var value = GetValue(type);
        var cost = GetResearchCost(type);
        if (_state.currency < cost) return false;
        _state.currency -= cost;
        value.researchLevel++;
        SaveAndNotify();
        return true;
    }

    public int GetCurrentDrawCost()
    {
        var level = balanceSettings == null || _state == null ? null : balanceSettings.GetGachaLevel(_state.gachaLevel);
        return level == null ? 0 : level.drawCost;
    }

    public int GetDrawCostForCount(int count)
    {
        if (count <= 0 || !CanUseBalance()) return 0;
        var level = _state.gachaLevel;
        var progress = _state.drawsAtCurrentLevel;
        var total = 0;
        for (var i = 0; i < count; i++)
        {
            var definition = balanceSettings.GetGachaLevel(level);
            if (definition == null) return total;
            total += definition.drawCost;
            progress++;
            if (definition.drawsToNextLevel > 0 && progress >= definition.drawsToNextLevel &&
                balanceSettings.GetGachaLevel(level + 1) != null)
            {
                level++;
                progress = 0;
            }
        }

        return total;
    }

    public int GetResearchCost(UpgradeStatType type)
    {
        var definition = balanceSettings == null ? null : balanceSettings.GetStat(type);
        return definition == null
            ? 0
            : Mathf.CeilToInt(definition.researchBaseCost *
                              Mathf.Pow(definition.researchCostGrowth, GetValue(type).researchLevel));
    }

    public bool IsUnlocked(UpgradeStatType type)
    {
        return CanUseBalance() && GetUnlockedStats().Contains(type);
    }

    public List<UpgradeStatType> GetUnlockedStats()
    {
        var result = new List<UpgradeStatType>();
        if (!CanUseBalance()) return result;
        for (var level = 1; level <= _state.gachaLevel; level++)
        {
            var definition = balanceSettings.GetGachaLevel(level);
            if (definition?.newlyUnlockedStats == null) continue;
            foreach (var stat in definition.newlyUnlockedStats)
                if (!result.Contains(stat))
                    result.Add(stat);
        }

        return result;
    }

    // 전투 등 외부 시스템이 최종 보너스를 읽는 API입니다.
    public UpgradeStatSnapshot GetStatSnapshot(UpgradeStatType type)
    {
        var definition = balanceSettings == null ? null : balanceSettings.GetStat(type);
        var value = GetValue(type);
        if (definition == null)
            return new UpgradeStatSnapshot(type, value.accumulatedValue, value.accumulatedValue, value.researchLevel,
                0f, 0f);
        var efficiency = definition.baseCoefficient * GetResearchMultiplier(definition, value.researchLevel);
        float effective = value.accumulatedValue;
        if (type != UpgradeStatType.StatIncrease) effective *= 1f + GetStatIncreaseEffect();
        return new UpgradeStatSnapshot(type, value.accumulatedValue, effective, value.researchLevel, efficiency,
            effective * efficiency);
    }

    private GachaDrawResult ExecuteOneDraw()
    {
        var cost = GetCurrentDrawCost();
        _state.currency -= cost;
        var pool = GetUnlockedStats();
        var type = pool[UnityEngine.Random.Range(0, pool.Count)];
        var amount = UnityEngine.Random.Range(1, 11);
        var value = GetValue(type);
        value.accumulatedValue += amount;
        var increased = AdvanceGachaLevel();
        return new GachaDrawResult(type, amount, value.accumulatedValue, increased, _state.gachaLevel);
    }

    private bool AdvanceGachaLevel()
    {
        var definition = balanceSettings.GetGachaLevel(_state.gachaLevel);
        if (definition == null || definition.drawsToNextLevel <= 0) return false;
        _state.drawsAtCurrentLevel++;
        if (_state.drawsAtCurrentLevel < definition.drawsToNextLevel ||
            balanceSettings.GetGachaLevel(_state.gachaLevel + 1) == null) return false;
        _state.gachaLevel++;
        _state.drawsAtCurrentLevel = 0;
        return true;
    }

    private float GetStatIncreaseEffect()
    {
        var increase = GetStatSnapshotWithoutAmplifier(UpgradeStatType.StatIncrease);
        return increase.FinalBonus;
    }

    private UpgradeStatSnapshot GetStatSnapshotWithoutAmplifier(UpgradeStatType type)
    {
        var definition = balanceSettings == null ? null : balanceSettings.GetStat(type);
        var value = GetValue(type);
        if (definition == null)
            return new UpgradeStatSnapshot(type, value.accumulatedValue, value.accumulatedValue, value.researchLevel,
                0f, 0f);
        var efficiency = definition.baseCoefficient * GetResearchMultiplier(definition, value.researchLevel);
        return new UpgradeStatSnapshot(type, value.accumulatedValue, value.accumulatedValue, value.researchLevel,
            efficiency, value.accumulatedValue * efficiency);
    }

    private float GetResearchMultiplier(UpgradeStatDefinition definition, int level)
    {
        return 1f + definition.researchMaxMultiplierBonus * (1f - Mathf.Exp(-definition.researchCurveRate * level));
    }

    private UpgradeStatValue GetValue(UpgradeStatType type)
    {
        if (_state == null) _state = CreateInitialState();
        if (_state.stats == null) _state.stats = new List<UpgradeStatValue>();
        foreach (var item in _state.stats)
            if (item != null && item.statType == type)
                return item;
        var added = new UpgradeStatValue { statType = type };
        _state.stats.Add(added);
        return added;
    }

    private bool CanUseBalance()
    {
        return _state != null && balanceSettings != null && balanceSettings.GetGachaLevel(1) != null;
    }

    private bool HasUnlockedDrawPool()
    {
        return GetUnlockedStats().Count > 0;
    }

    private void LoadState()
    {
        _state = PlayerPrefs.HasKey(SaveKey)
            ? JsonUtility.FromJson<UpgradeState>(PlayerPrefs.GetString(SaveKey))
            : CreateInitialState();
        if (_state == null) _state = CreateInitialState();
        if (_state.stats == null) _state.stats = new List<UpgradeStatValue>();
        SaveAndNotify();
    }

    private UpgradeState CreateInitialState()
    {
        return new UpgradeState { currency = startingCurrency };
    }

    private void SaveAndNotify()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(_state));
        PlayerPrefs.Save();
        stateChanged?.Invoke();
    }
}
