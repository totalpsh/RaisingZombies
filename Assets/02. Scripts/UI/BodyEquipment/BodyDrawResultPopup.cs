using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 한 번의 뽑기에서 얻은 최대 10개 장비를 인벤토리 원본으로 표시합니다.
public sealed class BodyDrawResultPopup : BaseUI
{
    [SerializeField] private Transform itemsRoot; // 결과 장비 카드의 부모
    [SerializeField] private BodyEquipmentItemView itemPrefab; // 최대 10개까지 재사용할 카드 원형
    [SerializeField] private TMP_Text titleText; // 실제 획득 장비 개수를 표시한다
    [SerializeField] private TMP_Text messageText; // 파기 결과와 오류를 표시한다
    [SerializeField] private Button inventoryButton; // 보관된 전체 장비 인벤토리를 연다
    [SerializeField] private Button closeButton; // 결과 팝업을 닫는다
    [SerializeField] private GameObject confirmationRoot; // 되돌릴 수 없는 파기 확인 영역
    [SerializeField] private TMP_Text confirmationText; // 파기 대상과 포인트를 표시한다
    [SerializeField] private Button confirmDismantleButton; // 확인한 결과 장비를 파기한다
    [SerializeField] private Button cancelDismantleButton; // 파기를 취소한다
    private readonly List<BodyEquipmentItemView> _items = new(10); // 결과가 바뀌어도 재사용할 카드
    private readonly List<string> _resultIds = new(10); // Draw 순간 Inventory에 저장된 장비 ID
    private BodyEquipmentManager _manager; // 결과 장비의 실제 Inventory 원본
    private string _pendingDismantleId = string.Empty; // 확인을 기다리는 결과 장비 ID
    private static Task<BodyDrawResultPopup> _openingTask; // 중복 비동기 생성 방지 작업

    // Draw 결과를 기존 UIManager 팝업으로 엽니다.
    public static async Task<BodyDrawResultPopup> ShowAsync(IReadOnlyList<BodyDrawResult> results)
    {
        if (!UIManager.HasInstance) return null;
        if (_openingTask != null) return await _openingTask;
        try
        {
            _openingTask = UIManager.Instance.OpenUI<BodyDrawResultPopup>(results, UILayer.PopUp);
            return await _openingTask;
        }
        finally { _openingTask = null; }
    }

    // 도메인 재로드 없는 플레이에서도 이전 비동기 작업을 지웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpeningTask() { _openingTask = null; }

    // Manager 상태와 버튼을 중복 없이 연결합니다.
    private void OnEnable()
    {
        _manager = BodyEquipmentManager.EnsureInstance();
        _manager.StateChanged -= Refresh;
        _manager.StateChanged += Refresh;
        AddButtonListener(inventoryButton, OpenInventory);
        AddButtonListener(closeButton, Close);
        AddButtonListener(confirmDismantleButton, ConfirmDismantle);
        AddButtonListener(cancelDismantleButton, CancelDismantle);
        SetActive(confirmationRoot, false);
    }

    // 숨겨진 결과 팝업의 Listener를 정리합니다.
    private void OnDisable()
    {
        if (_manager != null) _manager.StateChanged -= Refresh;
        RemoveButtonListener(inventoryButton, OpenInventory);
        RemoveButtonListener(closeButton, Close);
        RemoveButtonListener(confirmDismantleButton, ConfirmDismantle);
        RemoveButtonListener(cancelDismantleButton, CancelDismantle);
        _pendingDismantleId = string.Empty;
    }

    // 전달받은 복제 결과에서 Inventory의 안정적인 ID만 보관합니다.
    public override void Init(object param = null)
    {
        _resultIds.Clear();
        if (param is IReadOnlyList<BodyDrawResult> results)
        {
            for (int index = 0; index < results.Count && index < 10; index++)
            {
                BodyEquipmentInstance equipment = results[index].Equipment; // 이번 Draw 결과 장비
                if (equipment != null && !string.IsNullOrWhiteSpace(equipment.uniqueId)) _resultIds.Add(equipment.uniqueId);
            }
        }
        Refresh();
    }

    // 아직 Inventory에 존재하는 결과만 최대 10개 카드에 다시 표시합니다.
    private void Refresh()
    {
        if (_manager == null || itemsRoot == null || itemPrefab == null) return;
        EnsureItems(_resultIds.Count);
        int visibleCount = 0; // 파기되지 않아 실제 표시할 결과 개수
        for (int index = 0; index < _resultIds.Count; index++)
        {
            if (_manager.GetEquipment(_resultIds[index]) == null) continue;
            _items[visibleCount].Bind(_manager, _resultIds[index], RequestDismantle);
            visibleCount++;
        }
        for (int index = visibleCount; index < _items.Count; index++) _items[index].gameObject.SetActive(false);
        SetText(titleText, $"획득 장비 {visibleCount}개");
    }

    // 결과 개수까지만 카드를 만들고 다음 Draw에서 재사용합니다.
    private void EnsureItems(int required)
    {
        int target = Mathf.Clamp(required, 0, 10); // 10회 Draw를 넘지 않는 카드 목표 개수
        while (_items.Count < target) _items.Add(Instantiate(itemPrefab, itemsRoot));
    }

    // 결과 장비 파기 전 확인 내용을 표시합니다.
    private void RequestDismantle(string instanceId)
    {
        BodyEquipmentInstance equipment = _manager == null ? null : _manager.GetEquipment(instanceId); // 파기를 요청한 결과 장비
        if (!BodyEquipmentUIFormatter.TryGetDefinitions(_manager, equipment, out BodyEquipmentDefinitionSO definition, out BodyRarityDefinitionSO rarity)) return;
        _pendingDismantleId = instanceId;
        SetText(confirmationText, $"{rarity.DisplayName} {definition.DisplayName}을 파기하고\n연구 포인트 {rarity.DismantleResearchPoint}을 받겠습니까?");
        SetActive(confirmationRoot, true);
    }

    // 확인한 결과 장비를 Manager의 안전 조건으로 파기합니다.
    private void ConfirmDismantle()
    {
        string instanceId = _pendingDismantleId; // 상태 이벤트 전에 보관할 파기 ID
        CancelDismantle();
        int point = 0; // 성공 시 Manager가 반환할 연구 포인트
        bool success = _manager != null && _manager.TryDismantle(instanceId, out point); // 실제 파기 결과
        SetText(messageText, success ? $"연구 포인트 +{point}" : "장착 중이거나 잠긴 장비는 파기할 수 없습니다.");
    }

    // 대기 중인 파기 요청을 취소합니다.
    private void CancelDismantle()
    {
        _pendingDismantleId = string.Empty;
        SetActive(confirmationRoot, false);
    }

    // 현재 결과를 유지한 채 전체 인벤토리 팝업을 엽니다.
    private async void OpenInventory()
    {
        try { await BodyInventoryPopup.ShowAsync(); }
        catch (System.Exception exception) { Debug.LogException(exception, this); }
    }

    // UIManager의 기존 풀과 모달 구조를 통해 닫습니다.
    public void Close() { if (UIManager.HasInstance) UIManager.Instance.CloseUI(this); }

    // 버튼에 중복 없이 Listener를 연결합니다.
    private static void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // 등록한 버튼 Listener를 제거합니다.
    private static void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction action) { if (button != null) button.onClick.RemoveListener(action); }

    // 오브젝트 참조가 있을 때만 활성 상태를 변경합니다.
    private static void SetActive(GameObject target, bool active) { if (target != null) target.SetActive(active); }

    // Text 참조가 있을 때만 문자열을 반영합니다.
    private static void SetText(TMP_Text text, string value) { if (text != null) text.text = value ?? string.Empty; }
}
