using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 현재 장착 슬롯을 클릭했을 때 장비 한 개의 상세 정보만 보여준다.
public sealed class BodyEquipmentDetailPopup : MonoBehaviour
{
    [SerializeField] private BodyEquipmentInfoView equipmentView; // 상세 정보를 표시하는 공통 View
    [SerializeField] private TMP_Text stateText; // 현재 장착 중인 장비임을 알린다
    [SerializeField] private Button closeButton; // 상세 창을 닫는다
    private BodyEquipmentSlot _displayedSlot; // 열려 있는 장착 부위의 변경을 반영한다

    // 기존 ID 호출도 장착된 장비의 정보 표시로만 제한한다.
    public void Open(BodyEquipmentManager manager, string instanceId)
    {
        Open(manager, manager == null ? null : manager.GetEquipment(instanceId));
    }

    // 부모가 전달한 실제 장착 장비 하나를 같은 팝업에 표시한다.
    public void Open(BodyEquipmentManager manager, BodyEquipmentInstance equipment)
    {
        if (manager == null || equipment == null || !manager.IsEquipped(equipment.uniqueId) ||
            !manager.Database.TryGetEquipment(equipment.definitionId, out BodyEquipmentDefinitionSO definition))
        {
            Close();
            return;
        }
        _displayedSlot = definition.Slot;
        equipmentView?.Bind(manager, equipment);
        if (stateText != null) stateText.text = "장착 중";
        gameObject.SetActive(true);
    }

    // 부모의 StateChanged 갱신에서 현재 부위의 최신 장착 장비를 다시 표시한다.
    public void RefreshEquipped(BodyEquipmentManager manager)
    {
        Open(manager, manager == null ? null : manager.GetEquipped(_displayedSlot));
    }

    // 버튼 Listener를 중복 없이 연결한다.
    private void OnEnable()
    {
        if (closeButton == null) return;
        closeButton.onClick.RemoveListener(Close);
        closeButton.onClick.AddListener(Close);
    }

    // 닫힌 팝업의 Listener를 해제한다.
    private void OnDisable()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(Close);
    }

    // 장비 데이터는 유지하고 상세 화면만 닫는다.
    public void Close()
    {
        gameObject.SetActive(false);
    }
}
