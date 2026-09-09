using System;
using System.Collections.Generic;
using UnityEngine;

// 모든 장비, 레어도와 스탯 정의를 한 번 캐시해 Runtime 조회를 제공합니다.
[CreateAssetMenu(fileName = "BodyEquipmentDatabase", menuName = "Raising Zombies/Body Equipment/Database")]
public sealed class BodyEquipmentDatabaseSO : ScriptableObject
{
    [SerializeField] private BodyEquipmentDefinitionSO[] equipmentDefinitions = Array.Empty<BodyEquipmentDefinitionSO>(); // 뽑을 수 있는 전체 장비 원형
    [SerializeField] private BodyRarityDefinitionSO[] rarityDefinitions = Array.Empty<BodyRarityDefinitionSO>(); // Tier 1~12 레어도 성능 원형
    [SerializeField] private EquipmentStatDefinitionSO[] statDefinitions = Array.Empty<EquipmentStatDefinitionSO>(); // 장비에 붙을 수 있는 전체 스탯 원형
    private readonly Dictionary<string, BodyEquipmentDefinitionSO> _equipmentById = new(StringComparer.Ordinal); // Definition ID 기반 장비 캐시
    private readonly Dictionary<int, BodyRarityDefinitionSO> _rarityByTier = new(); // Tier 기반 레어도 캐시
    private readonly Dictionary<EquipmentStatType, EquipmentStatDefinitionSO> _statByType = new(); // Stat Type 기반 스탯 캐시
    private readonly List<BodyEquipmentDefinitionSO> _drawableEquipment = new(); // 양수 Weight를 가진 장비 선택 캐시
    private readonly List<double> _cumulativeEquipmentWeights = new(); // 이진 탐색에 사용할 장비 누적 Weight
    private double _totalEquipmentWeight; // 캐시된 전체 장비 Draw Weight
    private bool _cacheReady; // 현재 배열 내용으로 캐시를 만든 상태

    public IReadOnlyList<BodyEquipmentDefinitionSO> EquipmentDefinitions => equipmentDefinitions; // 전체 장비 원형 읽기 전용 목록
    public IReadOnlyList<BodyRarityDefinitionSO> RarityDefinitions => rarityDefinitions; // 전체 레어도 원형 읽기 전용 목록
    public IReadOnlyList<EquipmentStatDefinitionSO> StatDefinitions => statDefinitions; // 전체 스탯 원형 읽기 전용 목록

    // 현재 데이터가 유효하면 ID Dictionary를 한 번 구성합니다.
    public bool TryInitialize(out string error)
    {
        _cacheReady = false;
        _equipmentById.Clear();
        _rarityByTier.Clear();
        _statByType.Clear();
        _drawableEquipment.Clear();
        _cumulativeEquipmentWeights.Clear();
        _totalEquipmentWeight = 0d;
        if (!TryValidate(out error)) return false;
        foreach (BodyEquipmentDefinitionSO definition in equipmentDefinitions)
        {
            _equipmentById.Add(definition.DefinitionId, definition);
            if (definition.DrawWeight <= 0f) continue;
            _totalEquipmentWeight += definition.DrawWeight;
            _drawableEquipment.Add(definition);
            _cumulativeEquipmentWeights.Add(_totalEquipmentWeight);
        }
        foreach (BodyRarityDefinitionSO rarity in rarityDefinitions) _rarityByTier.Add(rarity.Tier, rarity);
        foreach (EquipmentStatDefinitionSO stat in statDefinitions) _statByType.Add(stat.StatType, stat);
        _cacheReady = true;
        return true;
    }

    // 저장된 ID에 해당하는 장비 원형을 캐시에서 조회합니다.
    public bool TryGetEquipment(string definitionId, out BodyEquipmentDefinitionSO definition)
    {
        EnsureCache();
        definition = null;
        return !string.IsNullOrWhiteSpace(definitionId) && _equipmentById.TryGetValue(definitionId, out definition);
    }

    // 지정 Tier의 레어도 성능 원형을 캐시에서 조회합니다.
    public bool TryGetRarity(int tier, out BodyRarityDefinitionSO rarity)
    {
        EnsureCache();
        return _rarityByTier.TryGetValue(tier, out rarity);
    }

    // 지정 종류의 장비 스탯 원형을 캐시에서 조회합니다.
    public bool TryGetStat(EquipmentStatType statType, out EquipmentStatDefinitionSO stat)
    {
        EnsureCache();
        return _statByType.TryGetValue(statType, out stat);
    }

    // 초기화 때 만든 누적 Weight 캐시에서 장비 원형 하나를 이진 탐색으로 선택합니다.
    public BodyEquipmentDefinitionSO GetWeightedEquipment(float normalizedRoll)
    {
        EnsureCache();
        if (_drawableEquipment.Count == 0 || _totalEquipmentWeight <= 0d) return null;
        double target = Mathf.Clamp01(normalizedRoll) * _totalEquipmentWeight; // 전체 Weight 안의 선택 위치
        int low = 0; // 이진 탐색의 포함된 최소 인덱스
        int high = _cumulativeEquipmentWeights.Count - 1; // 이진 탐색의 포함된 최대 인덱스
        while (low < high)
        {
            int middle = low + (high - low) / 2; // 오버플로 없는 중간 인덱스
            if (target < _cumulativeEquipmentWeights[middle]) high = middle;
            else low = middle + 1;
        }
        return _drawableEquipment[low];
    }

    // 전체 Definition ID, 12개 Tier, 스탯 참조와 범위를 검증합니다.
    public bool TryValidate(out string error)
    {
        error = string.Empty;
        if (equipmentDefinitions == null || equipmentDefinitions.Length == 0) { error = "Equipment Definition 목록이 비어 있습니다."; return false; }
        if (rarityDefinitions == null || rarityDefinitions.Length != 12) { error = "Rarity Definition은 Tier 1~12가 모두 필요합니다."; return false; }
        if (statDefinitions == null || statDefinitions.Length == 0) { error = "Stat Definition 목록이 비어 있습니다."; return false; }

        HashSet<string> equipmentIds = new(StringComparer.Ordinal); // 중복 장비 ID 검사용 집합
        HashSet<int> rarityTiers = new(); // 중복 및 누락 Tier 검사용 집합
        HashSet<string> statIds = new(StringComparer.Ordinal); // 중복 스탯 ID 검사용 집합
        HashSet<EquipmentStatType> statTypes = new(); // 중복 스탯 종류 검사용 집합
        foreach (EquipmentStatDefinitionSO stat in statDefinitions) // 전체 스탯 원형 검사
        {
            if (stat == null || !stat.TryValidate(out error)) return false;
            if (!statIds.Add(stat.StatId) || !statTypes.Add(stat.StatType)) { error = $"중복 Stat 정의입니다: {stat.StatId}"; return false; }
        }
        foreach (BodyRarityDefinitionSO rarity in rarityDefinitions) // 전체 레어도 원형 검사
        {
            if (rarity == null || !rarity.TryValidate(out error)) return false;
            if (!rarityTiers.Add(rarity.Tier)) { error = $"중복 Rarity Tier입니다: {rarity.Tier}"; return false; }
        }
        for (int tier = 1; tier <= 12; tier++)
            if (!rarityTiers.Contains(tier)) { error = $"Rarity Tier {tier} 정의가 없습니다."; return false; }
        foreach (BodyEquipmentDefinitionSO equipment in equipmentDefinitions) // 전체 장비 원형과 Stat 참조 검사
        {
            if (equipment == null || !equipment.TryValidate(out error)) return false;
            if (!equipmentIds.Add(equipment.DefinitionId)) { error = $"중복 Equipment Definition ID입니다: {equipment.DefinitionId}"; return false; }
            if (!ContainsOnlyKnownStats(equipment.PossibleMainStats, statTypes) || !ContainsOnlyKnownStats(equipment.PossibleSubStats, statTypes))
            { error = $"{equipment.DefinitionId}에 Database에 없는 Stat Definition 참조가 있습니다."; return false; }
        }
        return true;
    }

    // 장비 Stat Pool이 Database에 등록된 정의만 참조하는지 확인합니다.
    private static bool ContainsOnlyKnownStats(IReadOnlyList<EquipmentStatDefinitionSO> stats, HashSet<EquipmentStatType> knownTypes)
    {
        if (stats == null) return false;
        for (int index = 0; index < stats.Count; index++)
            if (stats[index] == null || !knownTypes.Contains(stats[index].StatType)) return false;
        return true;
    }

    // 조회 전에 캐시가 없다면 현재 유효 데이터로 한 번 구성합니다.
    private void EnsureCache()
    {
        if (_cacheReady) return;
        if (!TryInitialize(out string error)) Debug.LogError($"[BodyEquipmentDatabase] {error}", this);
    }

    // Inspector 변경 후 이전 Runtime 캐시를 폐기하고 오류를 표시합니다.
    private void OnValidate()
    {
        _cacheReady = false;
        _drawableEquipment.Clear();
        _cumulativeEquipmentWeights.Clear();
        _totalEquipmentWeight = 0d;
        if (!TryValidate(out string error)) Debug.LogWarning($"[BodyEquipmentDatabase] {error}", this);
    }
}
