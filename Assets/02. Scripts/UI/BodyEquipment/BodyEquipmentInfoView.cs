using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 장비 한 개의 공통 아이콘, 레어도와 실제 스탯을 표시한다.
public sealed class BodyEquipmentInfoView : MonoBehaviour
{
    [SerializeField] private Image equipmentIcon; // 원형 장비 아이콘
    [SerializeField] private Image slotIcon; // 장착 슬롯 표시 이미지
    [SerializeField] private Image rarityFrame; // 레어도 정의의 색과 프레임
    [SerializeField] private bool applyRarityFrameSprite = true; // 기존 디자인 Sprite를 레어도 프레임으로 교체할지 여부
    [SerializeField] private TextMeshProUGUI powerValueText; // 좀비 파워수치
    [SerializeField] private TMP_Text equipmentNameText; // 장비 이름과 부위
    [SerializeField] private TMP_Text rarityText; // 레어도 표시명
    [SerializeField] private TMP_Text mainStatText; // 주 스탯 한 줄
    [SerializeField] private TMP_Text subStatsText; // 실제로 뽑힌 보조 스탯만 표시
    [SerializeField] private TMP_Text emptyText; // 장착 장비가 없는 상태 안내
    [SerializeField] private BodyEquipmentStatRowView[] statRows; // ItemDetail에 미리 배치된 재사용 스탯 행
    private BodyRarityVisual _rarityVisual; // 같은 오브젝트에 선택적으로 추가한 레어도 색상 컴포넌트
    private bool _rarityVisualResolved; // 컴포넌트 검색을 한 번만 수행했는지 여부
    private readonly List<EquipmentStatRoll> _displayRolls = new(5); // Main과 Sub를 실제 표시 순서로 재사용한다
    private readonly HashSet<EquipmentStatType> _displayedTypes = new(); // 같은 StatType의 중복 행을 막는다

    // 같은 Formatter와 Definition으로 장비 정보를 갱신한다.
    public void Bind(BodyEquipmentManager manager, BodyEquipmentInstance equipment)
    {
        Bind(manager, equipment, null, false);
    }

    // 현재 장비와 비교 대상의 실제 Roll을 고정 Stat Object에 반영한다.
    public void Bind(BodyEquipmentManager manager, BodyEquipmentInstance equipment, BodyEquipmentInstance comparisonTarget, bool showComparison)
    {
        if (!_rarityVisualResolved)
        {
            _rarityVisual = GetComponent<BodyRarityVisual>();
            _rarityVisualResolved = true;
        }
        bool valid = BodyEquipmentUIFormatter.TryGetDefinitions(manager, equipment, out BodyEquipmentDefinitionSO definition, out BodyRarityDefinitionSO rarity); // 표시 가능한 장비인지 여부
        if (equipmentIcon != null)
        {
            equipmentIcon.sprite = valid ? definition.Icon : null;
            equipmentIcon.enabled = valid && definition.Icon != null;
        }
        if (slotIcon != null)
        {
            slotIcon.sprite = valid ? definition.EquipmentSprite : null;
            slotIcon.enabled = valid && definition.EquipmentSprite != null;
        }
        if (rarityFrame != null)
        {
            if (_rarityVisual == null) rarityFrame.color = valid ? rarity.UiColor : Color.clear;
            if (valid && applyRarityFrameSprite && rarity.FrameSprite != null) rarityFrame.sprite = rarity.FrameSprite;
            rarityFrame.enabled = valid;
        }
        if (_rarityVisual != null) _rarityVisual.SetRarity(valid ? rarity : null);
        SetText(equipmentNameText, valid ? $"{definition.DisplayName} · {definition.SlotDisplayName}" : string.Empty);
        SetText(rarityText, valid ? rarity.DisplayName : string.Empty);
        SetText(mainStatText, valid ? BodyEquipmentUIFormatter.FormatStat(manager, equipment.mainStat) : string.Empty);
        SetText(subStatsText, valid ? BodyEquipmentUIFormatter.FormatSubStats(manager, equipment) : string.Empty);
        SetText(powerValueText, string.Empty);
        if (emptyText != null) emptyText.gameObject.SetActive(!valid);
        RefreshStatRows(manager, valid ? equipment : null, comparisonTarget, showComparison);
    }

    // Main과 Sub Roll을 기존 행에 순서대로 표시하고 같은 StatType끼리 비교한다.
    private void RefreshStatRows(BodyEquipmentManager manager, BodyEquipmentInstance equipment, BodyEquipmentInstance comparisonTarget, bool showComparison)
    {
        if (statRows == null) return;
        bool hasTarget = comparisonTarget != null; // 비교할 현재 장비가 존재하는지 여부
        CollectDisplayRolls(equipment);
        if (showComparison && hasTarget) AppendMissingComparisonRolls(comparisonTarget);
        for (int index = 0; index < statRows.Length; index++)
        {
            BodyEquipmentStatRowView row = statRows[index]; // 현재 ItemDetail의 재사용 스탯 행
            if (row == null) continue;
            if (index >= _displayRolls.Count)
            {
                row.Clear();
                continue;
            }
            EquipmentStatType statType = _displayRolls[index].statType; // 이 행에 표시할 실제 Roll 종류
            TryGetStatValue(equipment, statType, out float value);
            bool targetHasValue = TryGetStatValue(comparisonTarget, statType, out float targetValue); // 현재 장비의 같은 Stat Roll
            row.Bind(manager, statType, value, showComparison, hasTarget, targetHasValue ? targetValue : 0f);
        }
    }

    // 장비의 Main과 Sub Roll을 중복 없이 실제 순서로 수집한다.
    private void CollectDisplayRolls(BodyEquipmentInstance equipment)
    {
        _displayRolls.Clear();
        _displayedTypes.Clear();
        AddDisplayRoll(equipment?.mainStat);
        if (equipment?.subStats == null) return;
        for (int index = 0; index < equipment.subStats.Count; index++) AddDisplayRoll(equipment.subStats[index]);
    }

    // 새 장비에 없는 현재 장비 옵션을 남은 행에 0 수치로 추가한다.
    private void AppendMissingComparisonRolls(BodyEquipmentInstance comparisonTarget)
    {
        AddDisplayRoll(comparisonTarget?.mainStat);
        if (comparisonTarget?.subStats == null) return;
        for (int index = 0; index < comparisonTarget.subStats.Count && _displayRolls.Count < statRows.Length; index++)
            AddDisplayRoll(comparisonTarget.subStats[index]);
    }

    // 유효하고 아직 표시하지 않은 Roll만 행 목록에 추가한다.
    private void AddDisplayRoll(EquipmentStatRoll roll)
    {
        if (roll == null || _displayRolls.Count >= statRows.Length || !_displayedTypes.Add(roll.statType)) return;
        _displayRolls.Add(roll);
    }

    // 장비 한 개에서 같은 StatType의 Main과 Sub 수치를 합산한다.
    private static bool TryGetStatValue(BodyEquipmentInstance equipment, EquipmentStatType statType, out float value)
    {
        value = 0f;
        if (equipment == null) return false;
        bool found = AddRoll(equipment.mainStat, statType, ref value); // 주 스탯 포함 여부
        if (equipment.subStats == null) return found;
        for (int index = 0; index < equipment.subStats.Count; index++)
            found |= AddRoll(equipment.subStats[index], statType, ref value);
        return found;
    }

    // 지정 StatType과 일치하는 유효한 Roll만 합산한다.
    private static bool AddRoll(EquipmentStatRoll roll, EquipmentStatType statType, ref float value)
    {
        if (roll == null || roll.statType != statType || float.IsNaN(roll.value) || float.IsInfinity(roll.value)) return false;
        value += Mathf.Max(0f, roll.value);
        return true;
    }

    // 참조가 연결된 Text에만 문자열을 반영한다.
    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
