using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 장비 한 개의 공통 아이콘, 레어도와 실제 스탯을 표시한다.
public sealed class BodyEquipmentInfoView : MonoBehaviour
{
    [SerializeField] private Image equipmentIcon; // 원형 장비 아이콘
    [SerializeField] private Image rarityFrame; // 레어도 정의의 색과 프레임
    [SerializeField] private TMP_Text equipmentNameText; // 장비 이름과 부위
    [SerializeField] private TMP_Text rarityText; // 레어도 표시명
    [SerializeField] private TMP_Text mainStatText; // 주 스탯 한 줄
    [SerializeField] private TMP_Text subStatsText; // 실제로 뽑힌 보조 스탯만 표시
    [SerializeField] private TMP_Text emptyText; // 장착 장비가 없는 상태 안내

    // 같은 Formatter와 Definition으로 장비 정보를 갱신한다.
    public void Bind(BodyEquipmentManager manager, BodyEquipmentInstance equipment)
    {
        bool valid = BodyEquipmentUIFormatter.TryGetDefinitions(manager, equipment, out BodyEquipmentDefinitionSO definition, out BodyRarityDefinitionSO rarity); // 표시 가능한 장비인지 여부
        if (equipmentIcon != null)
        {
            equipmentIcon.sprite = valid ? definition.Icon : null;
            equipmentIcon.enabled = valid && definition.Icon != null;
        }
        if (rarityFrame != null)
        {
            rarityFrame.color = valid ? rarity.UiColor : Color.clear;
            if (valid && rarity.FrameSprite != null) rarityFrame.sprite = rarity.FrameSprite;
            rarityFrame.enabled = valid;
        }
        SetText(equipmentNameText, valid ? $"{definition.DisplayName} · {definition.SlotDisplayName}" : string.Empty);
        SetText(rarityText, valid ? rarity.DisplayName : string.Empty);
        SetText(mainStatText, valid ? BodyEquipmentUIFormatter.FormatStat(manager, equipment.mainStat) : string.Empty);
        SetText(subStatsText, valid ? BodyEquipmentUIFormatter.FormatSubStats(manager, equipment) : string.Empty);
        if (emptyText != null) emptyText.gameObject.SetActive(!valid);
    }

    // 참조가 연결된 Text에만 문자열을 반영한다.
    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
