using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 현재 장착 슬롯을 클릭했을 때 장비 한 개의 상세 정보만 보여준다.
public sealed class BodyEquipmentDetailPopup : MonoBehaviour
{
    [SerializeField] private BodyEquipmentInfoView equipmentView; // 상세 정보를 표시하는 공통 View
    [SerializeField] private TMP_Text stateText; // 현재 장착 중인 장비임을 알린다
    [SerializeField] private Button closeButton; // 상세 창을 닫는다

    // Inventory의 ID로 클릭한 장착 장비를 다시 조회해 연다.
    public void Open(BodyEquipmentManager manager, string instanceId)
    {
        BodyEquipmentInstance equipment = manager == null ? null : manager.GetEquipment(instanceId); // 열 때의 실제 소유 장비
        if (equipment == null) return;
        equipmentView?.Bind(manager, equipment);
        if (stateText != null) stateText.text = manager.IsEquipped(instanceId) ? "장착 중" : "보관 중";
        gameObject.SetActive(true);
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
