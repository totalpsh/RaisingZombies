using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 강화 메뉴에서 열 수 있는 화면 상태입니다.
public enum UpgradeMenuState
{
    CategorySelection = 1,
    StatUpgrade = 2,
    ProductionUpgrade = 4,

    // 기존 CurrencyUpgrade의 직렬화 값을 유지합니다.
    CurrencyUpgrade = 3
}

// 스탯, 생산, 재화 강화 화면 전환을 관리합니다.
public sealed class UpgradeMenuController : BaseUI
{
    [Header("화면")]

    [SerializeField]
    private GameObject categorySelectionRoot; // 강화 종류 선택 화면

    [FormerlySerializedAs("zombieUpgradeRoot")]
    [SerializeField]
    private GameObject statUpgradeRoot; // 기존 좀비 스탯 강화 화면

    [SerializeField]
    private GameObject productionUpgradeRoot; // 생산 강화 화면

    [SerializeField]
    private GameObject currencyUpgradeRoot; // 재화 강화 화면

    [Header("선택 버튼")]

    [FormerlySerializedAs("zombieUpgradeButton")]
    [SerializeField]
    private Button statUpgradeButton; // 스탯 강화 선택 버튼

    [SerializeField]
    private Button productionUpgradeButton; // 생산 강화 선택 버튼

    [SerializeField]
    private Button currencyUpgradeButton; // 재화 강화 선택 버튼

    [Header("뒤로가기 버튼")]

    [FormerlySerializedAs("zombieBackButton")]
    [SerializeField]
    private Button statBackButton; // 스탯 강화에서 종류 선택으로 이동

    [SerializeField]
    private Button productionBackButton; // 생산 강화에서 종류 선택으로 이동

    [SerializeField]
    private Button currencyBackButton; // 재화 강화에서 종류 선택으로 이동

    private UpgradeMenuState _currentState; // 현재 화면 상태
    [SerializeField] private GameObject currencyLockObject; // 선택적으로 표시할 기존 재화 잠금 표시
    [SerializeField] private GameObject productionLockObject; // 선택적으로 표시할 기존 생산 잠금 표시
    private QuestManager _quests; // 저장된 해금을 제공할 퀘스트 원본

    public UpgradeMenuState CurrentState => _currentState;

    // 버튼 이벤트를 연결합니다.
    private void OnEnable()
    {
        _quests = QuestManager.HasInstance ? QuestManager.Instance : null;
        if (_quests != null) { _quests.Changed -= RefreshUnlocks; _quests.Changed += RefreshUnlocks; }
        RefreshUnlocks();
        AddButtonListener(statUpgradeButton, ShowStatUpgrade);
        AddButtonListener(productionUpgradeButton, ShowProductionUpgrade);
        AddButtonListener(currencyUpgradeButton, ShowCurrencyUpgrade);

        AddButtonListener(statBackButton, ShowCategorySelection);
        AddButtonListener(productionBackButton, ShowCategorySelection);
        AddButtonListener(currencyBackButton, ShowCategorySelection);

        ShowCategorySelection();
    }

    // 버튼 이벤트 중복을 막기 위해 해제합니다.
    private void OnDisable()
    {
        if (_quests != null) _quests.Changed -= RefreshUnlocks;
        _quests = null;
        RemoveButtonListener(statUpgradeButton, ShowStatUpgrade);
        RemoveButtonListener(productionUpgradeButton, ShowProductionUpgrade);
        RemoveButtonListener(currencyUpgradeButton, ShowCurrencyUpgrade);

        RemoveButtonListener(statBackButton, ShowCategorySelection);
        RemoveButtonListener(productionBackButton, ShowCategorySelection);
        RemoveButtonListener(currencyBackButton, ShowCategorySelection);
    }

    // 세 가지 강화 종류 선택 화면을 표시합니다.
    public void ShowCategorySelection()
    {
        SetState(UpgradeMenuState.CategorySelection);
    }

    // 기존 좀비 스탯 강화 화면을 표시합니다.
    public void ShowStatUpgrade()
    {
        SetState(UpgradeMenuState.StatUpgrade);
    }

    // 생산 강화 화면을 표시합니다.
    public void ShowProductionUpgrade()
    {
        SetState(UpgradeMenuState.ProductionUpgrade);
    }

    // 재화 강화 화면을 표시합니다.
    public void ShowCurrencyUpgrade()
    {
        SetState(UpgradeMenuState.CurrencyUpgrade);
    }

    // 기존 코드에서 호출할 가능성을 위한 호환 함수입니다.
    public void ShowZombieUpgrade()
    {
        ShowStatUpgrade();
    }

    // 현재 상태에 해당하는 화면 하나만 활성화합니다.
    public void SetState(UpgradeMenuState state)
    {
        if ((state == UpgradeMenuState.CurrencyUpgrade && (_quests == null || !_quests.CurrencyUpgradeUnlocked)) ||
            (state == UpgradeMenuState.ProductionUpgrade && (_quests == null || !_quests.ProductionUpgradeUnlocked)))
            state = UpgradeMenuState.CategorySelection;
        _currentState = state;

        SetActive(categorySelectionRoot, state == UpgradeMenuState.CategorySelection);
        SetActive(statUpgradeRoot, state == UpgradeMenuState.StatUpgrade);
        SetActive(productionUpgradeRoot, state == UpgradeMenuState.ProductionUpgrade);
        SetActive(currencyUpgradeRoot, state == UpgradeMenuState.CurrencyUpgrade);
    }

    // 버튼과 직접 화면 진입 모두 동일한 저장 해금 상태를 적용합니다.
    private void RefreshUnlocks()
    {
        bool currencyOpen = _quests != null && _quests.CurrencyUpgradeUnlocked; // 재화 강화 접근 가능 여부
        bool productionOpen = _quests != null && _quests.ProductionUpgradeUnlocked; // 생산 강화 접근 가능 여부
        if (currencyUpgradeButton != null) currencyUpgradeButton.interactable = currencyOpen;
        if (productionUpgradeButton != null) productionUpgradeButton.interactable = productionOpen;
        SetActive(currencyLockObject, !currencyOpen);
        SetActive(productionLockObject, !productionOpen);
        if ((_currentState == UpgradeMenuState.CurrencyUpgrade && !currencyOpen) ||
            (_currentState == UpgradeMenuState.ProductionUpgrade && !productionOpen)) ShowCategorySelection();
    }

    // 버튼이 존재할 때 이벤트를 연결합니다.
    private static void AddButtonListener(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // 버튼이 존재할 때 이벤트를 해제합니다.
    private static void RemoveButtonListener(
        Button button,
        UnityEngine.Events.UnityAction action)
    {
        if (button != null)
            button.onClick.RemoveListener(action);
    }

    // 오브젝트가 존재할 때 활성 상태를 변경합니다.
    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}
