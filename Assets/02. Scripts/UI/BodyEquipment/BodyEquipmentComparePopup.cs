using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 현재 장착 장비와 새 획득 장비를 나란히 비교하고 새 장비 동작만 요청한다.
public sealed class BodyEquipmentComparePopup : MonoBehaviour
{
    [SerializeField] private BodyEquipmentInfoView currentView; // 같은 부위의 현재 장착 장비 정보
    [SerializeField] private BodyEquipmentInfoView newView; // 이번 뽑기로 획득한 새 장비 정보
    [SerializeField] private TMP_Text differenceText; // 같은 주 스탯일 때의 차이 안내
    [SerializeField] private TMP_Text messageText; // 장착 또는 파기 실패 안내
    [SerializeField] private Button equipButton; // 새 장비를 매니저 API로 장착한다
    [SerializeField] private Button dismantleButton; // 새 장비의 파기 확인을 연다
    [SerializeField] private Button closeButton; // 새 장비를 보관한 채 창을 닫는다
    [SerializeField] private GameObject confirmationRoot; // 파기 전 확인 영역
    [SerializeField] private TMP_Text confirmationText; // 파기 장비와 포인트 안내
    [SerializeField] private Button confirmDismantleButton; // 확인 후 새 장비를 파기한다
    [SerializeField] private Button cancelDismantleButton; // 파기 확인을 취소한다
    private BodyEquipmentManager _manager; // Inventory와 장착의 실제 원본
    private string _newEquipmentId = string.Empty; // 선택한 새 장비의 안정적인 Instance ID

    // 획득한 ID의 현재 Inventory 상태로 비교 화면을 연다.
    public void Open(BodyEquipmentManager manager, string instanceId)
    {
        _manager = manager;
        BodyEquipmentInstance equipment = manager == null ? null : manager.GetEquipment(instanceId); // 이미 저장된 Draw 결과 장비
        if (!BodyEquipmentUIFormatter.TryGetDefinitions(manager, equipment, out BodyEquipmentDefinitionSO definition, out _)) return;
        _newEquipmentId = instanceId;
        BodyEquipmentInstance current = manager.GetEquipped(definition.Slot); // 같은 부위의 현재 장착 장비
        currentView?.Bind(manager, current, null, false);
        newView?.Bind(manager, equipment, current, true);
        if (differenceText != null) differenceText.text = BodyEquipmentUIFormatter.FormatComparison(manager, equipment);
        if (messageText != null) messageText.text = string.Empty;
        if (equipButton != null) equipButton.interactable = !manager.IsEquipped(instanceId);
        if (dismantleButton != null) dismantleButton.interactable = !manager.IsEquipped(instanceId) && !equipment.isLocked;
        if (confirmationRoot != null) confirmationRoot.SetActive(false);
        gameObject.SetActive(true);
    }

    // 모든 동작 버튼을 중복 없이 연결한다.
    private void OnEnable()
    {
        AddListener(equipButton, Equip);
        AddListener(dismantleButton, RequestDismantle);
        AddListener(closeButton, Close);
        AddListener(confirmDismantleButton, ConfirmDismantle);
        AddListener(cancelDismantleButton, CancelDismantle);
    }

    // 비활성 상태의 버튼 Listener를 정리한다.
    private void OnDisable()
    {
        RemoveListener(equipButton, Equip);
        RemoveListener(dismantleButton, RequestDismantle);
        RemoveListener(closeButton, Close);
        RemoveListener(confirmDismantleButton, ConfirmDismantle);
        RemoveListener(cancelDismantleButton, CancelDismantle);
    }

    // 실제 매니저의 교체 장착을 사용하고 성공했을 때만 닫는다.
    private void Equip()
    {
        if (_manager != null && _manager.TryEquip(_newEquipmentId, out _)) Close();
        else if (messageText != null) messageText.text = "장비를 장착할 수 없습니다.";
    }

    // 새 장비가 잠기거나 장착되었는지 확인하고 파기 확인을 보여준다.
    private void RequestDismantle()
    {
        BodyEquipmentInstance equipment = _manager == null ? null : _manager.GetEquipment(_newEquipmentId); // 파기 요청한 새 장비
        if (!BodyEquipmentUIFormatter.TryGetDefinitions(_manager, equipment, out BodyEquipmentDefinitionSO definition, out BodyRarityDefinitionSO rarity) || equipment.isLocked || _manager.IsEquipped(_newEquipmentId)) return;
        if (confirmationRoot == null)
        {
            if (_manager.TryDismantle(_newEquipmentId, out _)) Close();
            else if (messageText != null) messageText.text = "장착 중이거나 잠긴 장비는 파기할 수 없습니다.";
            return;
        }
        if (confirmationText != null) confirmationText.text = $"{rarity.DisplayName} {definition.DisplayName}을 파기하고\n연구 포인트 {rarity.DismantleResearchPoint}을 받겠습니까?";
        if (confirmationRoot != null) confirmationRoot.SetActive(true);
    }

    // 확인한 새 장비만 매니저의 안전 조건으로 파기한다.
    private void ConfirmDismantle()
    {
        if (_manager != null && _manager.TryDismantle(_newEquipmentId, out _)) Close();
        else if (messageText != null) messageText.text = "장착 중이거나 잠긴 장비는 파기할 수 없습니다.";
        CancelDismantle();
    }

    // 파기 확인 영역만 닫는다.
    private void CancelDismantle()
    {
        if (confirmationRoot != null) confirmationRoot.SetActive(false);
    }

    // Inventory에 보관된 새 장비를 유지하고 비교 화면만 닫는다.
    public void Close()
    {
        _newEquipmentId = string.Empty;
        gameObject.SetActive(false);
    }

    // 버튼 Listener를 중복 없이 연결한다.
    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // 버튼 Listener를 제거한다.
    private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.RemoveListener(action);
    }
}
