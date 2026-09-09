using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 소유 장비를 최대 50개씩 재사용 카드로 보여주는 인벤토리 팝업입니다.
public sealed class BodyInventoryPopup : BaseUI
{
    public const int PageSize = 50; // 한 페이지에서 생성하고 재사용할 최대 카드 수
    [SerializeField] private Transform itemsRoot; // 재사용 장비 카드의 부모
    [SerializeField] private BodyEquipmentItemView itemPrefab; // 반복 생성 후 재사용할 장비 카드 원형
    [SerializeField] private TMP_Text ticketText; // 현재 신체 뽑기권을 표시한다
    [SerializeField] private TMP_Text inventoryCountText; // 보유 개수와 용량을 표시한다
    [SerializeField] private TMP_Text pageText; // 현재 페이지와 전체 페이지를 표시한다
    [SerializeField] private TMP_Text messageText; // 실패와 파기 결과를 표시한다
    [SerializeField] private Button previousButton; // 이전 50개 장비를 표시한다
    [SerializeField] private Button nextButton; // 다음 50개 장비를 표시한다
    [SerializeField] private Button closeButton; // 팝업을 닫는다
    [SerializeField] private GameObject confirmationRoot; // 되돌릴 수 없는 파기 확인 영역
    [SerializeField] private TMP_Text confirmationText; // 파기할 장비와 보상을 안내한다
    [SerializeField] private Button confirmDismantleButton; // 확인한 장비를 파기한다
    [SerializeField] private Button cancelDismantleButton; // 파기 요청을 취소한다
    private readonly List<BodyEquipmentItemView> _items = new(PageSize); // 최초 생성 후 계속 재사용할 카드
    private BodyEquipmentManager _manager; // 인벤토리의 실제 원본
    private CurrencyWalletManager _wallet; // 뽑기권 표시 원본
    private int _pageIndex; // 현재 표시 중인 0 기준 페이지
    private string _pendingDismantleId = string.Empty; // 확인을 기다리는 장비 ID
    private static Task<BodyInventoryPopup> _openingTask; // 중복 비동기 생성 방지 작업

    // 기존 팝업이 있으면 재사용하고 현재 인벤토리로 엽니다.
    public static async Task<BodyInventoryPopup> ShowAsync()
    {
        if (!UIManager.HasInstance) return null;
        BodyInventoryPopup existing = UIManager.Instance.GetUI<BodyInventoryPopup>(); // 이미 열린 인벤토리
        if (existing != null) { existing.Refresh(); return existing; }
        if (_openingTask != null) return await _openingTask;
        try
        {
            _openingTask = UIManager.Instance.OpenUI<BodyInventoryPopup>(null, UILayer.PopUp);
            return await _openingTask;
        }
        finally { _openingTask = null; }
    }

    // 도메인 재로드 없는 플레이에서도 이전 비동기 작업을 지웁니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOpeningTask() { _openingTask = null; }

    // Manager 이벤트와 버튼을 중복 없이 연결합니다.
    private void OnEnable()
    {
        _manager = BodyEquipmentManager.EnsureInstance();
        _wallet = CurrencyWalletManager.EnsureInstance();
        _manager.StateChanged -= Refresh;
        _manager.StateChanged += Refresh;
        _wallet.CurrencyChanged -= HandleCurrencyChanged;
        _wallet.CurrencyChanged += HandleCurrencyChanged;
        AddButtonListener(previousButton, ShowPreviousPage);
        AddButtonListener(nextButton, ShowNextPage);
        AddButtonListener(closeButton, Close);
        AddButtonListener(confirmDismantleButton, ConfirmDismantle);
        AddButtonListener(cancelDismantleButton, CancelDismantle);
        SetActive(confirmationRoot, false);
        Refresh();
    }

    // 숨겨진 팝업의 이벤트와 버튼 Listener를 정리합니다.
    private void OnDisable()
    {
        if (_manager != null) _manager.StateChanged -= Refresh;
        if (_wallet != null) _wallet.CurrencyChanged -= HandleCurrencyChanged;
        RemoveButtonListener(previousButton, ShowPreviousPage);
        RemoveButtonListener(nextButton, ShowNextPage);
        RemoveButtonListener(closeButton, Close);
        RemoveButtonListener(confirmDismantleButton, ConfirmDismantle);
        RemoveButtonListener(cancelDismantleButton, CancelDismantle);
        _pendingDismantleId = string.Empty;
    }

    // 풀에서 다시 열릴 때 첫 페이지와 최신 데이터를 표시합니다.
    public override void Init(object param = null)
    {
        _pageIndex = 0;
        Refresh();
    }

    // 현재 페이지 범위만 최대 50개 카드에 바인딩합니다.
    public void Refresh()
    {
        if (_manager == null || itemsRoot == null || itemPrefab == null) return;
        int count = _manager.InventoryCount; // 현재 전체 장비 수
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(count / (float)PageSize)); // 빈 인벤토리도 1로 보일 페이지 수
        _pageIndex = Mathf.Clamp(_pageIndex, 0, pageCount - 1);
        int start = _pageIndex * PageSize; // 현재 페이지의 첫 장비 인덱스
        int visibleCount = Mathf.Min(PageSize, count - start); // 이번 페이지에 표시할 카드 수
        EnsureItems(visibleCount);
        for (int index = 0; index < _items.Count; index++)
        {
            bool visible = index < visibleCount; // 현재 페이지에 실제 데이터가 있는지 여부
            _items[index].gameObject.SetActive(visible);
            if (visible) _items[index].Bind(_manager, _manager.Inventory[start + index].uniqueId, RequestDismantle);
        }
        int capacity = _manager.ResearchSettings == null ? 0 : _manager.ResearchSettings.InventoryCapacity; // Settings의 실제 용량
        SetText(ticketText, _manager.BodyDrawTickets.ToString());
        SetText(inventoryCountText, capacity <= 0 ? count.ToString() : $"{count} / {capacity}");
        SetText(pageText, $"{_pageIndex + 1} / {pageCount}");
        if (previousButton != null) previousButton.interactable = _pageIndex > 0;
        if (nextButton != null) nextButton.interactable = _pageIndex + 1 < pageCount;
    }

    // 필요한 개수까지만 카드를 만들고 이후 페이지에서는 재사용합니다.
    private void EnsureItems(int required)
    {
        int target = Mathf.Clamp(required, 0, PageSize); // 성능 제한을 적용한 카드 목표 개수
        while (_items.Count < target) _items.Add(Instantiate(itemPrefab, itemsRoot));
    }

    // 이전 페이지를 같은 카드 목록으로 표시합니다.
    private void ShowPreviousPage()
    {
        if (_pageIndex <= 0) return;
        _pageIndex--;
        Refresh();
    }

    // 다음 페이지를 같은 카드 목록으로 표시합니다.
    private void ShowNextPage()
    {
        if (_manager == null || (_pageIndex + 1) * PageSize >= _manager.InventoryCount) return;
        _pageIndex++;
        Refresh();
    }

    // 파기 전 장비 상태와 획득 포인트를 확인 창에 표시합니다.
    private void RequestDismantle(string instanceId)
    {
        BodyEquipmentInstance equipment = _manager == null ? null : _manager.GetEquipment(instanceId); // 파기를 요청한 실제 장비
        if (!BodyEquipmentUIFormatter.TryGetDefinitions(_manager, equipment, out BodyEquipmentDefinitionSO definition, out BodyRarityDefinitionSO rarity)) return;
        _pendingDismantleId = instanceId;
        SetText(confirmationText, $"{rarity.DisplayName} {definition.DisplayName}을 파기하고\n연구 포인트 {rarity.DismantleResearchPoint}을 받겠습니까?");
        SetActive(confirmationRoot, true);
    }

    // 확인한 장비를 Manager의 안전 조건으로 파기합니다.
    private void ConfirmDismantle()
    {
        string instanceId = _pendingDismantleId; // Callback 중 상태 변경 전에 보관할 ID
        CancelDismantle();
        int point = 0; // 성공 시 Manager가 반환할 연구 포인트
        bool success = _manager != null && _manager.TryDismantle(instanceId, out point); // 실제 파기 결과
        SetText(messageText, success ? $"연구 포인트 +{point}" : "장착 중이거나 잠긴 장비는 파기할 수 없습니다.");
    }

    // 대기 중인 파기 요청을 지우고 확인 창을 닫습니다.
    private void CancelDismantle()
    {
        _pendingDismantleId = string.Empty;
        SetActive(confirmationRoot, false);
    }

    // 신체 뽑기권이 바뀔 때만 해당 Text를 갱신합니다.
    private void HandleCurrencyChanged(GameCurrencyType type, long amount)
    {
        if (type == GameCurrencyType.BodyDrawTicket) SetText(ticketText, amount.ToString());
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

    // 오브젝트 참조가 있을 때만 활성 상태를 바꿉니다.
    private static void SetActive(GameObject target, bool active) { if (target != null) target.SetActive(active); }

    // Text 참조가 있을 때만 문자열을 반영합니다.
    private static void SetText(TMP_Text text, string value) { if (text != null) text.text = value ?? string.Empty; }
}
