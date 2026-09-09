using System;
using System.Collections.Generic;
using UnityEngine;

// 한 레어도의 연구 레벨별 등장 가중치를 저장합니다.
[Serializable]
public sealed class BodyRarityWeight
{
    [Range(1, 12)] public int rarityTier = 1; // 이 Weight가 가리키는 레어도 Tier
    [Min(0f)] public float weight; // 전체 합에서 정규화할 실제 등장 가중치
}

// 한 뽑기 연구 레벨의 비용, 시간과 12개 확률 Weight를 저장합니다.
[Serializable]
public sealed class BodyDrawResearchLevelDefinition
{
    [Min(1)] public int level = 1; // 현재 적용되는 연구 레벨
    [Min(0)] public int requiredResearchPoints; // 다음 레벨 연구 시작에 소비할 포인트
    [Min(0f)] public double durationSeconds; // 다음 레벨 연구에 필요한 실제 시간
    public BodyRarityWeight[] rarityWeights = Array.Empty<BodyRarityWeight>(); // Tier 1~12 실제 뽑기 가중치

    // 한 연구 레벨에서 지정 Tier의 Weight를 반환합니다.
    public float GetWeight(int rarityTier)
    {
        if (rarityWeights == null) return 0f;
        foreach (BodyRarityWeight item in rarityWeights) // 일치하는 Tier 가중치
            if (item != null && item.rarityTier == rarityTier) return Mathf.Max(0f, item.weight);
        return 0f;
    }

    // 이 레벨의 음수가 아닌 전체 Weight를 합산합니다.
    public double GetTotalWeight()
    {
        double total = 0d; // 오버플로에 더 안전한 전체 가중치
        if (rarityWeights == null) return total;
        foreach (BodyRarityWeight item in rarityWeights) // 합산할 레어도 가중치
            if (item != null && item.weight > 0f) total += item.weight;
        return total;
    }
}

// 신체 뽑기 비용, 인벤토리 용량과 시간 연구 확률표를 관리합니다.
[CreateAssetMenu(fileName = "BodyDrawResearchSettings", menuName = "Raising Zombies/Body Equipment/Draw Research Settings")]
public sealed class BodyDrawResearchSettingsSO : ScriptableObject
{
    [SerializeField, Min(0)] private int inventoryCapacity = 500; // 0이면 무제한인 소유 장비 최대 개수
    [SerializeField, Min(1)] private int singleDrawTicketCost = 1; // 1회 뽑기에 소비할 신체 뽑기권
    [SerializeField, Min(1)] private int tenDrawTicketCost = 10; // 10회 뽑기에 소비할 신체 뽑기권
    [SerializeField, Min(1)] private int initialResearchLevel = 1; // 새 저장이 시작할 연구 레벨
    [SerializeField, Min(1f)] private float defenseReductionConstant = 100f; // 방어력 피해 감소식의 기준 상수
    [SerializeField, Min(1f)] private float baseCriticalDamageMultiplier = 1.5f; // 치명타 피해 옵션이 더해질 기본 배율
    [SerializeField] private BodyDrawResearchLevelDefinition[] levels = Array.Empty<BodyDrawResearchLevelDefinition>(); // 레벨 순서대로 배치할 연구 데이터

    public int InventoryCapacity => inventoryCapacity; // 0이면 무제한인 인벤토리 용량
    public int SingleDrawTicketCost => Mathf.Max(1, singleDrawTicketCost); // 안전한 1회 비용
    public int TenDrawTicketCost => Mathf.Max(1, tenDrawTicketCost); // 안전한 10회 비용
    public int InitialResearchLevel => Mathf.Max(1, initialResearchLevel); // 안전한 초기 연구 레벨
    public float DefenseReductionConstant => Mathf.Max(1f, defenseReductionConstant); // 0으로 나누지 않는 방어력 기준 상수
    public float BaseCriticalDamageMultiplier => Mathf.Max(1f, baseCriticalDamageMultiplier); // 1보다 작지 않은 기본 치명타 배율
    public IReadOnlyList<BodyDrawResearchLevelDefinition> Levels => levels; // UI와 Manager가 읽는 전체 연구 레벨
    public int MaximumResearchLevel => levels == null || levels.Length == 0 ? InitialResearchLevel : levels.Length; // 정의된 최고 연구 레벨

    // 지정 연구 레벨의 데이터 원형을 반환합니다.
    public BodyDrawResearchLevelDefinition GetLevel(int level)
    {
        if (levels == null) return null;
        foreach (BodyDrawResearchLevelDefinition item in levels) // 실제 번호와 일치하는 레벨
            if (item != null && item.level == level) return item;
        return null;
    }

    // 현재 연구 레벨의 Weight에서 한 Tier의 실제 확률을 계산합니다.
    public float GetProbability(int level, int rarityTier)
    {
        BodyDrawResearchLevelDefinition definition = GetLevel(level); // 확률 원본 연구 레벨
        double total = definition == null ? 0d : definition.GetTotalWeight(); // 정규화 분모
        if (total <= 0d) return 0f;
        return (float)(definition.GetWeight(rarityTier) / total);
    }

    // 레벨 순서, 12개 Tier, 비용, 시간과 Weight를 검증합니다.
    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (singleDrawTicketCost < 1 || tenDrawTicketCost < 1) { error = "Draw Ticket 비용은 1 이상이어야 합니다."; return false; }
        if (inventoryCapacity < 0) { error = "Inventory Capacity가 음수입니다."; return false; }
        if (defenseReductionConstant <= 0f || baseCriticalDamageMultiplier < 1f) { error = "전투 방어 상수 또는 기본 치명타 배율이 잘못됐습니다."; return false; }
        if (levels == null || levels.Length == 0) { error = "Research Level 데이터가 비어 있습니다."; return false; }
        HashSet<int> levelNumbers = new(); // 중복 연구 레벨 검사용 집합
        for (int index = 0; index < levels.Length; index++)
        {
            BodyDrawResearchLevelDefinition level = levels[index]; // 순서와 Weight를 확인할 레벨
            if (level == null || level.level != index + 1 || !levelNumbers.Add(level.level)) { error = $"Research Level {index + 1} 순서 또는 중복을 확인하세요."; return false; }
            if (level.requiredResearchPoints < 0 || level.durationSeconds < 0d) { error = $"Research Lv.{level.level} 비용 또는 시간이 음수입니다."; return false; }
            if (level.rarityWeights == null || level.rarityWeights.Length != 12) { error = $"Research Lv.{level.level}에는 12개 Rarity Weight가 필요합니다."; return false; }
            HashSet<int> tiers = new(); // 현재 레벨 안의 중복 Tier 검사용 집합
            foreach (BodyRarityWeight weight in level.rarityWeights) // 현재 레벨의 12개 가중치
            {
                if (weight == null || weight.rarityTier < 1 || weight.rarityTier > 12 || weight.weight < 0f || !tiers.Add(weight.rarityTier))
                { error = $"Research Lv.{level.level} Rarity Tier 또는 Weight가 잘못됐습니다."; return false; }
            }
            if (level.GetTotalWeight() <= 0d) { error = $"Research Lv.{level.level} Weight 총합이 0입니다."; return false; }
        }
        if (GetLevel(initialResearchLevel) == null) { error = "Initial Research Level 정의가 없습니다."; return false; }
        return true;
    }

    // Inspector에서 잘못된 연구 확률표를 즉시 알립니다.
    private void OnValidate()
    {
        if (!TryValidate(out string error)) Debug.LogWarning($"[BodyDrawResearchSettings] {error}", this);
    }
}
