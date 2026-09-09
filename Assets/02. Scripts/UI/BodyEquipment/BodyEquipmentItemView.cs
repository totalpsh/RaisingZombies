using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 뽑기 결과와 인벤토리에서 함께 재사용하는 장비 카드입니다.
public sealed class BodyEquipmentItemView : MonoBehaviour
{
    [SerializeField] private Image rarityFrame; // 레어도 색과 선택적 프레임을 표시한다
    [SerializeField] private Image equipmentIcon; // 장비 원형의 아이콘을 표시한다
    [SerializeField] private TMP_Text rarityText; // 레어도 한국어 이름을 표시한다
    [SerializeField] private TMP_Text nameText; // 장비 이름을 표시한다
    [SerializeField] private TMP_Text slotText; // 장착 신체 부위를 표시한다
    [SerializeField] private TMP_Text mainStatText; // 주 스탯을 표시한다
    [SerializeField] private TMP_Text subStatText; // 보조 스탯 목록을 표시한다
    [SerializeField] private TMP_Text stateText; // 장착과 잠금 상태를 표시한다
    [SerializeField] private TMP_Text comparisonText; // 현재 장착 장비와의 간단한 비교를 표시한다
    [SerializeField] private TMP_Text lockButtonText; // 잠금 버튼의 현재 동작을 표시한다
    [SerializeField] private Button equipButton; // 현재 장비를 슬롯에 장착한다
    [SerializeField] private Button dismantleButton; // 부모 팝업에 파기 확인을 요청한다
    [SerializeField] private Button lockButton; // 장비 잠금을 전환한다
    private BodyEquipmentManager _manager; // 장비 동작의 실제 원본
    private string _instanceId = string.Empty; // 현재 카드에 바인딩된 장비 ID
    private Action<string> _dismantleRequested; // 부모 팝업의 파기 확인 Callback

    // 버튼 Listener를 한 번만 연결합니다.
    private void Awake()
    {
        AddButtonListener(equipButton, HandleEquip);
        AddButtonListener(dismantleButton, HandleDismantle);
        AddButtonListener(lockButton, HandleLock);
    }

    // 프리팹이 제거될 때 등록한 Listener를 정리합니다.
    private void OnDestroy()
    {
        RemoveButtonListener(equipButton, HandleEquip);
        RemoveButtonListener(dismantleButton, HandleDismantle);
        RemoveButtonListener(lockButton, HandleLock);
        _dismantleRequested = null;
    }

    // 장비 ID와 동작 Callback을 현재 카드에 연결합니다.
    public void Bind(BodyEquipmentManager manager, string instanceId, Action<string> dismantleRequested)
    {
        _manager = manager;
        _instanceId = instanceId ?? string.Empty;
        _dismantleRequested = dismantleRequested;
        Refresh();
    }

    // 현재 인벤토리 원본으로 카드 전체를 갱신합니다.
    public void Refresh()
    {
        BodyEquipmentInstance equipment = _manager == null ? null : _manager.GetEquipment(_instanceId); // 카드에 표시할 실제 장비
        if (!BodyEquipmentUIFormatter.TryGetDefinitions(_manager, equipment, out BodyEquipmentDefinitionSO definition, out BodyRarityDefinitionSO rarity))
        {
            gameObject.SetActive(false);
            return;
        }
        bool equipped = _manager.IsEquipped(_instanceId); // 현재 슬롯에 장착 중인지 여부
        SetText(rarityText, rarity.DisplayName);
        SetText(nameText, definition.DisplayName);
        SetText(slotText, definition.SlotDisplayName);
        SetText(mainStatText, BodyEquipmentUIFormatter.FormatStat(_manager, equipment.mainStat));
        SetText(subStatText, BodyEquipmentUIFormatter.FormatSubStats(_manager, equipment));
        SetText(stateText, equipped ? "장착 중" : equipment.isLocked ? "잠금" : "보관 중");
        SetText(comparisonText, BodyEquipmentUIFormatter.FormatComparison(_manager, equipment));
        SetText(lockButtonText, equipment.isLocked ? "잠금 해제" : "잠금");
        if (rarityFrame != null)
        {
            rarityFrame.color = rarity.UiColor;
            if (rarity.FrameSprite != null) rarityFrame.sprite = rarity.FrameSprite;
        }
        if (equipmentIcon != null)
        {
            equipmentIcon.sprite = definition.Icon;
            equipmentIcon.enabled = definition.Icon != null;
        }
        if (equipButton != null) equipButton.interactable = !equipped;
        if (dismantleButton != null) dismantleButton.interactable = !equipped && !equipment.isLocked;
        gameObject.SetActive(true);
    }

    // 현재 장비를 같은 슬롯의 기존 장비와 교체 장착합니다.
    private void HandleEquip()
    {
        if (_manager != null) _manager.TryEquip(_instanceId, out _);
    }

    // 파기 여부를 부모 팝업의 확인 UI에 전달합니다.
    private void HandleDismantle()
    {
        if (!string.IsNullOrWhiteSpace(_instanceId)) _dismantleRequested?.Invoke(_instanceId);
    }

    // 현재 장비의 실수 파기 보호 상태를 반전합니다.
    private void HandleLock()
    {
        BodyEquipmentInstance equipment = _manager == null ? null : _manager.GetEquipment(_instanceId); // 잠금을 바꿀 실제 장비
        if (equipment != null) _manager.TrySetLocked(_instanceId, !equipment.isLocked);
    }

    // 버튼에 중복 없이 Listener를 연결합니다.
    private static void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // 등록한 버튼 Listener를 제거합니다.
    private static void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction action) { if (button != null) button.onClick.RemoveListener(action); }

    // Text 참조가 있을 때만 문자열을 반영합니다.
    private static void SetText(TMP_Text text, string value) { if (text != null) text.text = value ?? string.Empty; }
}
