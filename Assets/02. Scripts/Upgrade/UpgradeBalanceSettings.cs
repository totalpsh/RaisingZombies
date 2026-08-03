using System;
using System.Collections.Generic;
using UnityEngine;

public enum UpgradeStatType
{
    Health,
    Defense,
    Attack,
    InfectionCount,
    MoveSpeed,
    AttackSpeed,
    ZombieCount,
    StatIncrease,
    CriticalChance,
    CriticalDamage
}

public enum UpgradeResultKind
{
    Percent,
    Integer,
    GlobalAmplifier
}

/// <summary>스탯 하나의 표시 및 연구 밸런스 정의입니다.</summary>
[Serializable]
public sealed class UpgradeStatDefinition
{
    public UpgradeStatType statType;
    public string displayName;
    public UpgradeResultKind resultKind;
    [Min(0f)] public float baseCoefficient = 0.01f; // 원본 수치 1당 기본 효과
    [Min(0)] public int researchBaseCost = 50; // 연구 레벨 0에서의 비용
    [Min(1f)] public float researchCostGrowth = 1.2f; // 연구 레벨당 비용 증가율
    [Min(0f)] public float researchMaxMultiplierBonus = 1f; // 연구로 추가 가능한 최대 효율 배율
    [Min(0f)] public float researchCurveRate = 0.2f; // 연구 효율 상승 곡선의 속도
}

/// <summary>가챠 레벨의 비용, 승급 조건, 새 해금 스탯입니다.</summary>
[Serializable]
public sealed class GachaLevelDefinition
{
    [Min(1)] public int level;
    [Min(0)] public int drawCost;
    [Min(0)] public int drawsToNextLevel; // 0이면 최고 레벨
    public UpgradeStatType[] newlyUnlockedStats;
}

/// <summary>업그레이드 시스템에서 참조하는 모든 조절 가능한 밸런스입니다.</summary>
[CreateAssetMenu(fileName = "UpgradeBalanceSettings", menuName = "Raising Zombies/Upgrade Balance Settings")]
public sealed class UpgradeBalanceSettings : ScriptableObject
{
    [SerializeField] private UpgradeStatDefinition[] statDefinitions;
    [SerializeField] private GachaLevelDefinition[] gachaLevels;
    public IReadOnlyList<UpgradeStatDefinition> StatDefinitions => statDefinitions;
    public IReadOnlyList<GachaLevelDefinition> GachaLevels => gachaLevels;

    public UpgradeStatDefinition GetStat(UpgradeStatType type)
    {
        if (statDefinitions != null) foreach (var item in statDefinitions) if (item != null && item.statType == type) return item;
        return null;
    }

    public GachaLevelDefinition GetGachaLevel(int level)
    {
        if (gachaLevels != null) foreach (var item in gachaLevels) if (item != null && item.level == level) return item;
        return null;
    }
}
