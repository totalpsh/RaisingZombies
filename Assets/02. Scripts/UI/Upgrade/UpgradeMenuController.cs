using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 강화 메뉴에서 열 수 있는 화면 상태입니다.
public enum UpgradeMenuState
{
    None = 0,
    CategorySelection = 1,
    StatUpgrade = 2,

    // 기존 CurrencyUpgrade의 직렬화 값을 유지합니다.
    CurrencyUpgrade = 3,
    ProductionUpgrade = 4
}

// 스탯, 생산, 재화 강화 화면 전환을 관리합니다.
public sealed class UpgradeMenuController : MonoBehaviour
{
    [Header("화면")]

    [SerializeField]
    private GameObject mainMenuRoot; // 기존 메인 메뉴 루트

    [SerializeField]
    private GameObject categorySelectionRoot; // 강화 종류 선택 화면

    [FormerlySerializedAs("zombieUpgradeRoot")]
    [SerializeField]
    private GameObject statUpgradeRoot; // 기존 좀비 스탯 강화 화면

    [SerializeField]
    private GameObject productionUpgradeRoot; // 생산 강화 화면

    [SerializeField]
    private GameObject currencyUpgradeRoot; // 재화 강화 화면

    [Header("메인 및 선택 버튼")]

    [SerializeField]
    private Button openUpgradeButton; // 메인 화면의 강화 버튼

    [FormerlySerializedAs("zombieUpgradeButton")]
    [SerializeField]
    private Button statUpgradeButton; // 스탯 강화 선택 버튼

    [SerializeField]
    private Button productionUpgradeButton; // 생산 강화 선택 버튼

    [SerializeField]
    private Button currencyUpgradeButton; // 재화 강화 선택 버튼

    [Header("뒤로가기 버튼")]

    [SerializeField]
    private Button categoryBackButton; // 종류 선택에서 메인으로 이동

    [FormerlySerializedAs("zombieBackButton")]
    [SerializeField]
    private Button statBackButton; // 스탯 강화에서 종류 선택으로 이동

    [SerializeField]
    private Button productionBackButton; // 생산 강화에서 종류 선택으로 이동

    [SerializeField]
    private Button currencyBackButton; // 재화 강화에서 종류 선택으로 이동

    [Header("초기 상태")]

    [SerializeField]
    private UpgradeMenuState initialState = UpgradeMenuState.None; // 최초 화면 상태

    private UpgradeMenuState _currentState; // 현재 화면 상태

    public UpgradeMenuState CurrentState => _currentState;

    // 버튼 이벤트를 연결합니다.
    private void OnEnable()
    {
        AddButtonListener(openUpgradeButton, ShowCategorySelection);

        AddButtonListener(statUpgradeButton, ShowStatUpgrade);
        AddButtonListener(productionUpgradeButton, ShowProductionUpgrade);
        AddButtonListener(currencyUpgradeButton, ShowCurrencyUpgrade);

        AddButtonListener(categoryBackButton, ShowMainMenu);
        AddButtonListener(statBackButton, ShowCategorySelection);
        AddButtonListener(productionBackButton, ShowCategorySelection);
        AddButtonListener(currencyBackButton, ShowCategorySelection);
    }

    // 모든 Awake가 끝난 뒤 최초 화면을 적용합니다.
    private void Start()
    {
        SetState(initialState);
    }

    // 버튼 이벤트 중복을 막기 위해 해제합니다.
    private void OnDisable()
    {
        RemoveButtonListener(openUpgradeButton, ShowCategorySelection);

        RemoveButtonListener(statUpgradeButton, ShowStatUpgrade);
        RemoveButtonListener(productionUpgradeButton, ShowProductionUpgrade);
        RemoveButtonListener(currencyUpgradeButton, ShowCurrencyUpgrade);

        RemoveButtonListener(categoryBackButton, ShowMainMenu);
        RemoveButtonListener(statBackButton, ShowCategorySelection);
        RemoveButtonListener(productionBackButton, ShowCategorySelection);
        RemoveButtonListener(currencyBackButton, ShowCategorySelection);
    }

    // 기존 메인 메뉴를 표시합니다.
    public void ShowMainMenu()
    {
        SetState(UpgradeMenuState.None);
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
        _currentState = state;

        SetActive(mainMenuRoot, state == UpgradeMenuState.None);
        SetActive(categorySelectionRoot, state == UpgradeMenuState.CategorySelection);
        SetActive(statUpgradeRoot, state == UpgradeMenuState.StatUpgrade);
        SetActive(productionUpgradeRoot, state == UpgradeMenuState.ProductionUpgrade);
        SetActive(currencyUpgradeRoot, state == UpgradeMenuState.CurrencyUpgrade);
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