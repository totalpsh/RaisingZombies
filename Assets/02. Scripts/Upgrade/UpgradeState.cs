using System;
using System.Collections.Generic;

/// PlayerPrefs JSON으로 저장하는 업그레이드 런타임 상태입니다.
[Serializable]
public sealed class UpgradeState
{
    public int version = 2;
    public int currency;
    public int gachaLevel = 1;
    public int drawsAtCurrentLevel;
    public List<UpgradeStatValue> stats = new List<UpgradeStatValue>();
    public int currencyPerSecondLevel; // 초당 재화 강화 레벨
    public int humanKillBonusLevel; // 인간 처치 추가 재화 강화 레벨
    public int offlineMaxTimeLevel; // 오프라인 최대 적립 시간 강화 레벨
    public int offlineEfficiencyLevel; // 오프라인 적립 효율 강화 레벨
    public string lastActivityUtc = string.Empty; // 마지막 정상 저장 시각을 기록한 UTC 문자열
}

[Serializable]
public sealed class UpgradeStatValue
{
    public UpgradeStatType statType;
    public int accumulatedValue;
    public int researchLevel;
}

/// 다른 게임 시스템이 읽기만 하는 계산 완료 스탯입니다.
public readonly struct UpgradeStatSnapshot
{
    public readonly UpgradeStatType StatType;
    public readonly int RawAccumulatedValue;
    public readonly float EffectiveAccumulatedValue;
    public readonly int ResearchLevel;
    public readonly float PerPointEfficiency;
    public readonly float FinalBonus;

    public UpgradeStatSnapshot(UpgradeStatType type, int raw, float effective, int research, float efficiency, float bonus)
    {
        StatType = type;
        RawAccumulatedValue = raw;
        EffectiveAccumulatedValue = effective;
        ResearchLevel = research;
        PerPointEfficiency = efficiency;
        FinalBonus = bonus;
    }
}

public readonly struct GachaDrawResult
{
    public readonly UpgradeStatType StatType;
    public readonly int Value;
    public readonly int Total;
    public readonly bool LevelIncreased;
    public readonly int NewLevel;

    public GachaDrawResult(UpgradeStatType type, int value, int total, bool levelIncreased, int newLevel)
    {
        StatType = type;
        Value = value;
        Total = total;
        LevelIncreased = levelIncreased;
        NewLevel = newLevel;
    }
}
