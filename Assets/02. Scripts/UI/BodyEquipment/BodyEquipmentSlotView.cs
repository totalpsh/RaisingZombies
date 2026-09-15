using System;
using UnityEngine;
using UnityEngine.UI;

// 기존 신체 장착 슬롯 하나를 실제 장착 데이터에 연결한다.
public sealed class BodyEquipmentSlotView : MonoBehaviour
{
    [SerializeField] private BodyEquipmentSlot slotType; // 이 View가 담당하는 실제 장착 부위
    [SerializeField] private Image equipmentIcon; // 현재 장착 장비 아이콘
    [SerializeField] private Image rarityFrame; // 장착 장비의 레어도 프레임
    [SerializeField] private GameObject emptyObject; // 기존 슬롯의 빈 장비 표시
    [SerializeField] private Button slotButton; // 장착 장비 상세 팝업을 여는 버튼
    private BodyEquipmentManager _manager; // 장착 상태의 단일 원본
    private Action<string> _detailRequested; // 패널에 상세 정보 표시를 요청한다

    public BodyEquipmentSlot SlotType => slotType; // 순서에 의존하지 않는 슬롯 식별값

    // 패널의 매니저와 상세 팝업 요청을 받는다.
    public void Bind(BodyEquipmentManager manager, Action<string> detailRequested)
    {
        _manager = manager;
        _detailRequested = detailRequested;
        Refresh();
    }

    // 저장된 현재 장착 장비와 빈 슬롯을 화면에 반영한다.
    public void Refresh()
    {
        BodyEquipmentInstance equipment = _manager == null ? null : _manager.GetEquipped(slotType); // 해당 부위에 실제 장착된 장비
        bool valid = BodyEquipmentUIFormatter.TryGetDefinitions(_manager, equipment, out BodyEquipmentDefinitionSO definition, out BodyRarityDefinitionSO rarity); // 원형과 레어도 조회 결과
        if (emptyObject != null) emptyObject.SetActive(!valid);
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
        if (slotButton != null) slotButton.interactable = valid;
    }

    // 버튼 Listener를 활성화 기간 동안 한 번만 등록한다.
    private void OnEnable()
    {
        if (slotButton == null) return;
        slotButton.onClick.RemoveListener(OpenDetail);
        slotButton.onClick.AddListener(OpenDetail);
    }

    // 비활성화할 때 Listener와 패널 요청을 정리한다.
    private void OnDisable()
    {
        if (slotButton != null) slotButton.onClick.RemoveListener(OpenDetail);
    }

    // 장착된 장비 ID만 상세 팝업으로 전달한다.
    private void OpenDetail()
    {
        BodyEquipmentInstance equipment = _manager == null ? null : _manager.GetEquipped(slotType); // 클릭 시점의 실제 장착 장비
        if (equipment != null) _detailRequested?.Invoke(equipment.uniqueId);
    }
}
