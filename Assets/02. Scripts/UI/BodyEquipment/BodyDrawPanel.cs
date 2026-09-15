using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 기존 스탯 가챠 화면을 대체해 신체 장비 뽑기 기능을 연결합니다.
public sealed class BodyDrawPanel : MonoBehaviour
{
    [SerializeField] private UpgradeMenuController menuController; // 기존 강화 메뉴의 화면 전환 원본
    [SerializeField] private TMP_Text ticketText; // 현재 신체 뽑기권을 표시한다
    [SerializeField] private TMP_Text singleCostText; // 1회 뽑기권 비용을 표시한다
    [SerializeField] private TMP_Text tenCostText; // 10회 뽑기권 비용을 표시한다
    [SerializeField] private TMP_Text researchLevelText; // 현재 뽑기 연구 레벨을 표시한다
    [SerializeField] private TMP_Text inventoryCountText; // 인벤토리 사용량을 표시한다
    [SerializeField] private TMP_Text recentResultText; // 가장 최근 획득 레어도와 장비를 표시한다
    [SerializeField] private TMP_Text messageText; // 뽑기 불가 이유를 표시한다
    [SerializeField] private Button drawOneButton; // 장비를 1회 뽑는다
    [SerializeField] private Button drawTenButton; // 장비를 10회 뽑는다
    [SerializeField] private Button researchButton; // 뽑기 연구 팝업을 연다
    [SerializeField] private Button probabilityButton; // 실제 레어도 확률 팝업을 연다
    [SerializeField] private Button inventoryButton; // 장비 인벤토리 팝업을 연다
    [SerializeField] private Button backButton; // 기존 강화 종류 선택 화면으로 돌아간다
    [SerializeField] private BodyEquipmentSlotView[] equippedSlots; // 기존 화면에 항상 보이는 8개 장착 슬롯
    [SerializeField] private BodyEquipmentInfoView centerResultView; // 중앙에 마지막 획득 장비를 간단히 보여준다
    [SerializeField] private GameObject centerResultRoot; // 뽑기 전에는 숨길 중앙 결과 영역
    [SerializeField] private BodyEquipmentComparePopup comparePopup; // 새 장비와 현재 장비를 비교하는 루트 내부 팝업
    [SerializeField] private BodyEquipmentDetailPopup detailPopup; // 장착 슬롯을 눌렀을 때 열 루트 내부 팝업
    private BodyEquipmentManager _manager; // 장비 Draw와 Inventory 실제 원본
    private CurrencyWalletManager _wallet; // 신체 뽑기권 실제 원본

    // Manager 이벤트와 화면 버튼을 중복 없이 연결합니다.
    private void OnEnable()
    {
        _manager = BodyEquipmentManager.EnsureInstance();
        _wallet = CurrencyWalletManager.EnsureInstance();
        _manager.StateChanged -= Refresh;
        _manager.StateChanged += Refresh;
        _manager.BodyDrawCompleted -= HandleDrawCompleted;
        _manager.BodyDrawCompleted += HandleDrawCompleted;
        _wallet.CurrencyChanged -= HandleCurrencyChanged;
        _wallet.CurrencyChanged += HandleCurrencyChanged;
        AddButtonListener(drawOneButton, DrawOne);
        AddButtonListener(drawTenButton, DrawTen);
        AddButtonListener(researchButton, OpenResearch);
        AddButtonListener(probabilityButton, OpenProbability);
        AddButtonListener(inventoryButton, OpenInventory);
        AddButtonListener(backButton, GoBack);
        if (equippedSlots != null)
            foreach (BodyEquipmentSlotView slot in equippedSlots) if (slot != null) slot.Bind(_manager, OpenDetail);
        if (centerResultRoot != null) centerResultRoot.SetActive(false);
        Refresh();
    }

    // 숨겨진 화면의 이벤트와 버튼 Listener를 정리합니다.
    private void OnDisable()
    {
        if (_manager != null)
        {
            _manager.StateChanged -= Refresh;
            _manager.BodyDrawCompleted -= HandleDrawCompleted;
        }
        if (_wallet != null) _wallet.CurrencyChanged -= HandleCurrencyChanged;
        RemoveButtonListener(drawOneButton, DrawOne);
        RemoveButtonListener(drawTenButton, DrawTen);
        RemoveButtonListener(researchButton, OpenResearch);
        RemoveButtonListener(probabilityButton, OpenProbability);
        RemoveButtonListener(inventoryButton, OpenInventory);
        RemoveButtonListener(backButton, GoBack);
        if (comparePopup != null) comparePopup.Close();
        if (detailPopup != null) detailPopup.Close();
    }

    // Ticket, 연구 레벨, 인벤토리와 Draw 가능 상태를 이벤트 시점에만 갱신합니다.
    public void Refresh()
    {
        if (_manager == null) return;
        int singleCost = _manager.GetDrawTicketCost(1); // Settings의 실제 1회 비용
        int tenCost = _manager.GetDrawTicketCost(10); // Settings의 실제 10회 비용
        int capacity = _manager.ResearchSettings == null ? 0 : _manager.ResearchSettings.InventoryCapacity; // Settings의 실제 인벤토리 용량
        SetText(ticketText, _manager.BodyDrawTickets.ToString());
        SetText(singleCostText, singleCost.ToString());
        SetText(tenCostText, tenCost.ToString());
        SetText(researchLevelText, $"Lv.{_manager.ResearchLevel}");
        SetText(inventoryCountText, capacity <= 0 ? _manager.InventoryCount.ToString() : $"{_manager.InventoryCount} / {capacity}");
        if (drawOneButton != null) drawOneButton.interactable = _manager.CanDraw(1);
        if (drawTenButton != null) drawTenButton.interactable = _manager.CanDraw(10);
        if (equippedSlots != null)
            foreach (BodyEquipmentSlotView slot in equippedSlots) if (slot != null) slot.Refresh();
    }

    // 1회 뽑기를 실행합니다.
    private void DrawOne() { Draw(1); }

    // 10회 뽑기를 실행합니다.
    private void DrawTen() { Draw(10); }

    // 실제 Manager에서 Ticket을 소비하고 1회는 비교, 10회는 기존 결과 목록으로 연결합니다.
    private async void Draw(int count)
    {
        if (_manager == null || !_manager.TryDraw(count, out IReadOnlyList<BodyDrawResult> results))
        {
            SetText(messageText, BuildDrawFailureMessage(count));
            Refresh();
            return;
        }
        SetText(messageText, string.Empty);
        if (results == null || results.Count == 0) return;
        if (count == 1)
        {
            BodyEquipmentInstance equipment = results[0].Equipment; // 이미 Inventory에 저장된 이번 결과
            if (equipment != null) OpenComparison(equipment.uniqueId);
            return;
        }
        try { await BodyDrawResultPopup.ShowAsync(results, OpenComparison); }
        catch (System.Exception exception) { Debug.LogException(exception, this); }
    }

    // 현재 비용과 인벤토리 상태로 가장 직접적인 실패 이유를 만듭니다.
    private string BuildDrawFailureMessage(int count)
    {
        if (_manager == null) return "장비 시스템을 준비하지 못했습니다.";
        int cost = _manager.GetDrawTicketCost(count); // 요청 Draw의 실제 Ticket 비용
        if (_manager.BodyDrawTickets < cost) return "신체 뽑기권이 부족합니다.";
        int capacity = _manager.ResearchSettings == null ? 0 : _manager.ResearchSettings.InventoryCapacity; // 실제 인벤토리 용량
        if (capacity > 0 && _manager.InventoryCount + count > capacity) return "장비 인벤토리가 가득 찼습니다.";
        return "장비 뽑기를 실행할 수 없습니다.";
    }

    // 최근 Draw 결과를 이벤트에서 읽어 간단히 표시합니다.
    private void HandleDrawCompleted(IReadOnlyList<BodyDrawResult> results)
    {
        if (results == null || results.Count == 0 || results[results.Count - 1].Equipment == null) return;
        BodyEquipmentInstance equipment = results[results.Count - 1].Equipment; // 최근 결과의 마지막 장비
        if (!BodyEquipmentUIFormatter.TryGetDefinitions(_manager, equipment, out BodyEquipmentDefinitionSO definition, out BodyRarityDefinitionSO rarity)) return;
        SetText(recentResultText, $"최근 획득: {rarity.DisplayName} {definition.DisplayName}");
        if (centerResultRoot != null) centerResultRoot.SetActive(true);
        if (centerResultView != null) centerResultView.Bind(_manager, equipment);
    }

    // 현재 장착 슬롯을 누르면 그 장비 하나의 상세 팝업을 엽니다.
    private void OpenDetail(string instanceId)
    {
        if (comparePopup != null) comparePopup.Close();
        if (detailPopup != null) detailPopup.Open(_manager, instanceId);
    }

    // 결과 ID가 아직 Inventory에 있을 때만 같은 부위의 현재 장비와 비교합니다.
    private void OpenComparison(string instanceId)
    {
        if (detailPopup != null) detailPopup.Close();
        if (comparePopup != null) comparePopup.Open(_manager, instanceId);
    }

    // 신체 뽑기권이 바뀔 때만 화면을 갱신합니다.
    private void HandleCurrencyChanged(GameCurrencyType type, long amount)
    {
        if (type == GameCurrencyType.BodyDrawTicket) Refresh();
    }

    // 뽑기 연구 팝업을 엽니다.
    private async void OpenResearch()
    {
        try { await BodyResearchPopup.ShowAsync(); }
        catch (System.Exception exception) { Debug.LogException(exception, this); }
    }

    // 현재 연구 단계의 실제 레어도 확률 팝업을 엽니다.
    private async void OpenProbability()
    {
        try { await BodyRarityProbabilityPopup.ShowAsync(); }
        catch (System.Exception exception) { Debug.LogException(exception, this); }
    }

    // 전체 보관 장비 관리 팝업을 열고, 장착 슬롯은 이 화면에 계속 표시합니다.
    private async void OpenInventory()
    {
        try { await BodyInventoryPopup.ShowAsync(); }
        catch (System.Exception exception) { Debug.LogException(exception, this); }
    }

    // 기존 강화 종류 선택 화면으로 돌아갑니다.
    private void GoBack()
    {
        if (menuController != null) menuController.ShowCategorySelection();
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
