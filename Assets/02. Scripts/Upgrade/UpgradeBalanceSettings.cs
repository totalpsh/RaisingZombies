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

/// <summary>스탯 하나의 표시 방식과 연구 밸런스를 정의합니다.</summary>
[Serializable]
public sealed class UpgradeStatDefinition
{
    public UpgradeStatType statType;
    public string displayName;
    public UpgradeResultKind resultKind;
    [Min(0f)] public float baseCoefficient = 0.01f; // 누적 수치 1당 기본 효과
    [Min(0)] public int researchBaseCost = 50; // 연구 레벨 0에서의 강화 비용
    [Min(1f)] public float researchCostGrowth = 1.2f; // 연구 레벨별 비용 증가율
    [Min(0f)] public float researchMaxMultiplierBonus = 1f; // 연구로 추가 가능한 최대 효율 배율
    [Min(0f)] public float researchCurveRate = 0.2f; // 연구 효율 상승 곡선의 속도
}

/// <summary>가챠 레벨의 비용, 승급 조건, 새 해금 스탯을 정의합니다.</summary>
[Serializable]
public sealed class GachaLevelDefinition
{
    [Min(1)] public int level;
    [Min(0)] public int drawCost;
    [Min(0)] public int drawsToNextLevel; // 0이면 최고 레벨
    public UpgradeStatType[] newlyUnlockedStats = Array.Empty<UpgradeStatType>();
}

/// <summary>업그레이드 시스템이 참조하는 모든 조절 가능한 밸런스입니다.</summary>
[CreateAssetMenu(fileName = "UpgradeBalanceSettings", menuName = "Raising Zombies/Upgrade Balance Settings")]
public sealed class UpgradeBalanceSettings : ScriptableObject
{
    [SerializeField] private UpgradeStatDefinition[] statDefinitions = Array.Empty<UpgradeStatDefinition>(); // 전체 스탯 정의
    [SerializeField] private GachaLevelDefinition[] gachaLevels = Array.Empty<GachaLevelDefinition>(); // 레벨 순서대로 배치한 가챠 정의

    public IReadOnlyList<UpgradeStatDefinition> StatDefinitions => statDefinitions;
    public IReadOnlyList<GachaLevelDefinition> GachaLevels => gachaLevels;

    /// <summary>지정한 스탯 정의를 반환하며, 정의가 없으면 null을 반환합니다.</summary>
    public UpgradeStatDefinition GetStat(UpgradeStatType type)
    {
        if (statDefinitions == null)
        {
            return null;
        }

        foreach (UpgradeStatDefinition item in statDefinitions)
        {
            if (item != null && item.statType == type)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>지정한 가챠 레벨 정의를 반환하며, 정의가 없으면 null을 반환합니다.</summary>
    public GachaLevelDefinition GetGachaLevel(int level)
    {
        if (gachaLevels == null)
        {
            return null;
        }

        foreach (GachaLevelDefinition item in gachaLevels)
        {
            if (item != null && item.level == level)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Inspector와 설정 도구에서 표시할 밸런스 오류를 수집합니다.</summary>
    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null)
        {
            throw new ArgumentNullException(nameof(errors));
        }

        errors.Clear();
        ValidateStats(errors);
        ValidateGachaLevels(errors);
    }

    /// <summary>현재 밸런스 오류를 콘솔에 명확히 출력합니다.</summary>
    [ContextMenu("밸런스 검증")]
    private void LogValidationResult()
    {
        var errors = new List<string>();
        CollectValidationErrors(errors);

        if (errors.Count == 0)
        {
            Debug.Log("[UpgradeBalanceSettings] 밸런스 검증을 통과했습니다.", this);
            return;
        }

        foreach (string error in errors)
        {
            Debug.LogError($"[UpgradeBalanceSettings] {error}", this);
        }
    }

    private void ValidateStats(List<string> errors)
    {
        var definedStats = new HashSet<UpgradeStatType>();

        if (statDefinitions != null)
        {
            foreach (UpgradeStatDefinition definition in statDefinitions)
            {
                if (definition == null)
                {
                    errors.Add("비어 있는 스탯 정의가 있습니다.");
                    continue;
                }

                if (!definedStats.Add(definition.statType))
                {
                    errors.Add($"중복 스탯 정의: {definition.statType}");
                }

                if (string.IsNullOrWhiteSpace(definition.displayName))
                {
                    errors.Add($"{definition.statType}의 표시명이 비어 있습니다.");
                }
            }
        }

        foreach (UpgradeStatType statType in Enum.GetValues(typeof(UpgradeStatType)))
        {
            if (!definedStats.Contains(statType))
            {
                errors.Add($"스탯 정의 누락: {statType}");
            }
        }

        if (!definedStats.Contains(UpgradeStatType.StatIncrease))
        {
            errors.Add("StatIncrease 정의가 없습니다. 전역 수치 증가 계산을 사용할 수 없습니다.");
        }
    }

    private void ValidateGachaLevels(List<string> errors)
    {
        if (GetGachaLevel(1) == null)
        {
            errors.Add("Lv.1 가챠 정의가 없습니다.");
        }

        if (gachaLevels == null || gachaLevels.Length == 0)
        {
            errors.Add("가챠 레벨 정의가 비어 있습니다.");
            return;
        }

        var levels = new HashSet<int>();
        for (var index = 0; index < gachaLevels.Length; index++)
        {
            GachaLevelDefinition definition = gachaLevels[index];
            if (definition == null)
            {
                errors.Add($"가챠 레벨 배열의 {index + 1}번째 항목이 비어 있습니다.");
                continue;
            }

            int expectedLevel = index + 1;
            if (definition.level != expectedLevel)
            {
                errors.Add($"가챠 레벨 순서 이상: {index + 1}번째 항목은 Lv.{expectedLevel}이어야 하지만 Lv.{definition.level}입니다.");
            }

            if (!levels.Add(definition.level))
            {
                errors.Add($"중복 가챠 레벨 정의: Lv.{definition.level}");
            }
        }
    }
}
