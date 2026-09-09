using System;
using System.Collections.Generic;
using UnityEngine;

// 재화 강화의 고정된 네 가지 종류입니다.
public enum CurrencyUpgradeType
{
    CurrencyPerSecond,
    HumanKillBonus,
    OfflineMaxTime,
    OfflineEfficiency
}

// 재화 강화 한 종류의 비용과 레벨당 효과를 정의합니다.
[Serializable]
public sealed class CurrencyUpgradeDefinition
{
    public CurrencyUpgradeType type; // 강화 종류
    public string id; // 저장 및 검증에 사용하는 고유 ID
    public string displayName; // UI에 표시할 한국어 이름
    [TextArea] public string description; // UI에 표시할 설명
    public Sprite icon; // 공용 Row에 표시할 이 강화 전용 아이콘
    [Tooltip("켜면 Max Level을 적용하지 않습니다. 기존 재화 강화도 기본적으로 무제한입니다.")]
    public bool unlimited = true; // 재화 강화의 레벨 제한을 사용하지 않을지 여부
    [Min(1)] public int maxLevel = 10; // Unlimited를 끈 경우에만 적용할 최대 레벨
    [Min(0)] public int baseCost = 100; // 0레벨에서 다음 레벨로 올릴 때의 비용
    [Min(1f)] public float costGrowth = 1.5f; // 레벨마다 곱하는 비용 배율
    [Min(0f)] public float valuePerLevel = 1f; // 레벨당 효과이며 오프라인 시간 종류만 시간 단위

    // 유한 레벨 정책을 사용하는 정의가 최대 레벨에 도달했는지 확인합니다.
    public bool IsMaxLevel(int level)
    {
        return !unlimited && level >= maxLevel;
    }
}

// 재화 생산과 오프라인 보상의 기본값 및 네 강화 정의를 보관합니다.
[CreateAssetMenu(fileName = "CurrencyUpgradeBalanceSettings", menuName = "Raising Zombies/Currency Upgrade Balance Settings")]
public sealed class CurrencyUpgradeBalanceSettings : ScriptableObject
{
    [Min(0f)] public float baseCurrencyPerSecond = 1f; // 강화 전 초당 기본 재화
    [Min(0f)] public float baseOfflineMaxHours = 2f; // Inspector에서 설정하는 강화 전 최대 적립 시간
    [Range(0f, 1f)] public float baseOfflineEfficiency = 0.5f; // 강화 전 오프라인 적립 효율
    [Range(0f, 1f)] public float maximumOfflineEfficiency = 1f; // 오프라인 적립 효율 상한
    [SerializeField] private CurrencyUpgradeDefinition[] definitions = Array.Empty<CurrencyUpgradeDefinition>(); // 네 종류의 재화 강화 정의

    public IReadOnlyList<CurrencyUpgradeDefinition> Definitions => definitions;

    // 지정한 종류의 재화 강화 정의를 반환합니다.
    public CurrencyUpgradeDefinition GetDefinition(CurrencyUpgradeType type)
    {
        if (definitions == null) return null;
        foreach (CurrencyUpgradeDefinition definition in definitions) // 검사할 강화 정의
        {
            if (definition != null && definition.type == type) return definition;
        }

        return null;
    }

    // 누락, 중복, 잘못된 비용 및 효율 설정을 검사합니다.
    public void CollectValidationErrors(List<string> errors)
    {
        if (errors == null) throw new ArgumentNullException(nameof(errors));
        errors.Clear();
        HashSet<CurrencyUpgradeType> types = new(); // 중복 종류 검사 집합
        HashSet<string> ids = new(StringComparer.Ordinal); // 중복 ID 검사 집합

        if (definitions != null)
        {
            foreach (CurrencyUpgradeDefinition definition in definitions) // 검사할 강화 정의
            {
                if (definition == null)
                {
                    errors.Add("비어 있는 재화 강화 정의가 있습니다.");
                    continue;
                }

                if (!types.Add(definition.type)) errors.Add($"중복 강화 종류: {definition.type}");
                if (string.IsNullOrWhiteSpace(definition.id)) errors.Add($"{definition.type}의 ID가 비어 있습니다.");
                else if (!ids.Add(definition.id)) errors.Add($"중복 강화 ID: {definition.id}");
                if (!definition.unlimited && definition.maxLevel <= 0) errors.Add($"{definition.type}의 유한 최대 레벨은 1 이상이어야 합니다.");
                if (definition.baseCost < 0) errors.Add($"{definition.type}의 기본 비용이 음수입니다.");
                if (!IsFinite(definition.costGrowth) || definition.costGrowth < 1f) errors.Add($"{definition.type}의 비용 증가 배율은 유한한 1 이상이어야 합니다.");
                if (!IsFinite(definition.valuePerLevel) || definition.valuePerLevel <= 0f) errors.Add($"{definition.type}의 레벨당 증가 수치는 유한한 양수여야 합니다.");
            }
        }

        foreach (CurrencyUpgradeType type in Enum.GetValues(typeof(CurrencyUpgradeType))) // 필수 강화 종류
        {
            if (!types.Contains(type)) errors.Add($"재화 강화 정의 누락: {type}");
        }

        if (!IsFinite(baseCurrencyPerSecond) || baseCurrencyPerSecond < 0f) errors.Add("기본 초당 재화는 유한한 0 이상이어야 합니다.");
        if (!IsFinite(baseOfflineMaxHours) || baseOfflineMaxHours < 0f) errors.Add("기본 오프라인 시간은 유한한 0 이상이어야 합니다.");
        if (!IsFinite(baseOfflineEfficiency) || baseOfflineEfficiency < 0f || baseOfflineEfficiency > 1f)
            errors.Add("기본 오프라인 효율은 0~1 범위여야 합니다.");
        if (!IsFinite(maximumOfflineEfficiency) || maximumOfflineEfficiency < 0f || maximumOfflineEfficiency > 1f)
            errors.Add("오프라인 효율 상한은 0~1 범위여야 합니다.");
        if (maximumOfflineEfficiency < baseOfflineEfficiency)
            errors.Add("오프라인 효율 상한이 기본 효율보다 작습니다.");
        CurrencyUpgradeDefinition efficiency = GetDefinition(CurrencyUpgradeType.OfflineEfficiency); // 효율 상한을 검사할 정의
        if (efficiency != null && !efficiency.unlimited && baseOfflineEfficiency + (double)efficiency.valuePerLevel * efficiency.maxLevel > maximumOfflineEfficiency + 0.0001d)
            errors.Add("최대 레벨의 오프라인 효율이 설정된 효율 상한을 초과합니다.");
    }

    // 무제한 레벨과 별개로 효과가 멈추는 기존 효율 상한을 안내합니다.
    public void CollectValidationWarnings(List<string> warnings)
    {
        if (warnings == null) throw new ArgumentNullException(nameof(warnings));
        warnings.Clear();
        CurrencyUpgradeDefinition efficiency = GetDefinition(CurrencyUpgradeType.OfflineEfficiency); // 효과 상한을 안내할 강화 정의
        if (efficiency == null || !efficiency.unlimited || !IsFinite(efficiency.valuePerLevel) || efficiency.valuePerLevel <= 0f) return;
        double capLevel = Math.Ceiling(Math.Max(0d, (maximumOfflineEfficiency - (double)baseOfflineEfficiency) / efficiency.valuePerLevel)); // 현재 공식에서 효율 상한에 도달하는 레벨
        warnings.Add($"오프라인 효율은 Lv.{capLevel:0}부터 {maximumOfflineEfficiency * 100d:0.##}% 상한에 도달합니다. 이후에도 레벨은 오르지만 실제 보상 효율은 증가하지 않습니다.");
    }

    // Inspector 수치가 NaN이나 무한대가 아닌지 확인합니다.
    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    // 현재 밸런스 검증 결과를 콘솔에 출력합니다.
    [ContextMenu("재화 강화 밸런스 검증")]
    private void LogValidationResult()
    {
        List<string> errors = new(); // 발견한 검증 오류
        CollectValidationErrors(errors);
        if (errors.Count == 0)
        {
            Debug.Log("[CurrencyUpgradeBalanceSettings] 검증을 통과했습니다.", this);
        }

        foreach (string error in errors) Debug.LogError($"[CurrencyUpgradeBalanceSettings] {error}", this);
        CollectValidationWarnings(errors);
        foreach (string warning in errors) Debug.LogWarning($"[CurrencyUpgradeBalanceSettings] {warning}", this); // 레벨 제한과 다른 효과 상한 안내
    }
}

// UI와 외부 시스템이 읽는 계산 완료 재화 강화 상태입니다.
public readonly struct CurrencyUpgradeSnapshot
{
    public readonly CurrencyUpgradeType Type; // 강화 종류
    public readonly string DisplayName; // 표시 이름
    public readonly string Description; // 설명
    public readonly int CurrentLevel; // 현재 레벨
    public readonly int MaxLevel; // 최대 레벨
    public readonly int NextCost; // 다음 강화 비용
    public readonly float CurrentEffect; // 현재 총 효과
    public readonly float NextEffect; // 다음 레벨 총 효과
    public readonly bool IsMaxLevel; // 최대 레벨 여부
    public readonly bool IsUnlimited; // 정의의 무제한 레벨 정책 여부

    // 계산된 재화 강화 표시 데이터를 생성합니다.
    public CurrencyUpgradeSnapshot(CurrencyUpgradeType type, string name, string description, int level, int maxLevel,
        int nextCost, float currentEffect, float nextEffect, bool unlimited = false)
    {
        Type = type;
        DisplayName = name;
        Description = description;
        CurrentLevel = level;
        MaxLevel = maxLevel;
        NextCost = nextCost;
        CurrentEffect = currentEffect;
        NextEffect = nextEffect;
        IsUnlimited = unlimited;
        IsMaxLevel = !unlimited && level >= maxLevel;
    }
}

// 한 번만 표시할 오프라인 보상 계산 결과입니다.
public readonly struct OfflineCurrencyReward
{
    public readonly double ActualSeconds; // 실제 오프라인 경과 초
    public readonly double AppliedSeconds; // 상한 적용 후 경과 초
    public readonly float Efficiency; // 적용 효율
    public readonly int EarnedCurrency; // 최종 획득 재화

    // 계산된 오프라인 보상 결과를 생성합니다.
    public OfflineCurrencyReward(double actualSeconds, double appliedSeconds, float efficiency, int earnedCurrency)
    {
        ActualSeconds = actualSeconds;
        AppliedSeconds = appliedSeconds;
        Efficiency = efficiency;
        EarnedCurrency = earnedCurrency;
    }
}
