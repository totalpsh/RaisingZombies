using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 게임의 최상위 메인 UI 탭입니다.
public enum MainUITab
{
    Info,
    Upgrade,
    Battle,
    Challenge,
    Shop
}

// 프리팹의 메인 Root와 버튼을 사용해 최상위 탭 전환을 관리합니다.
public sealed class MainNavigationController : MonoBehaviour
{
    [Header("메인 화면 Root")]
    [SerializeField] private TopUIController topUIController; // 탭 전환과 무관하게 유지할 공통 상단 UI
    [SerializeField] private GameObject contentRoot; // 모든 메인 탭 Root를 담는 영역
    [SerializeField] private GameObject infoRoot; // 정보 탭의 Placeholder Root
    [SerializeField] private GameObject upgradeRoot; // 기존 업그레이드 UI가 들어가는 Root
    [SerializeField] private GameObject battleRoot; // 전투 탭의 Placeholder Root
    [SerializeField] private GameObject challengeRoot; // 도전 탭의 Placeholder Root
    [SerializeField] private GameObject shopRoot; // 상점 탭의 Placeholder Root

    [Header("하단 네비게이션 버튼")]
    [SerializeField] private Button[] tabButtons; // Info부터 Shop까지 순서대로 연결된 버튼 목록
    [SerializeField] private GameObject[] selectedObjects; // 선택된 탭에서만 켜질 버튼별 Focus 오브젝트

    [Header("ContentBox 상점 이동 버튼")]
    [SerializeField] private Button contentGemBoxButton; // ContentBox의 Button_GemBox 상점 이동 버튼
    [SerializeField] private Button contentAddButton; // ContentBox의 Button_Add 상점 이동 버튼

    private MainUITab currentTab; // 현재 선택된 메인 UI 탭
    private bool isInitialized; // 중복 초기화 방지 상태
    private bool listenersRegistered; // 버튼 Listener 중복 등록 방지 상태

    public MainUITab CurrentTab => currentTab; // 외부 시스템이 확인할 현재 메인 탭

    // 프리팹 참조를 확인하고 기존 Upgrade UI를 Upgrade Root에 연결합니다.
    public async Task InitializeAsync(UIManager uiManager)
    {
        if (isInitialized)
        {
            ShowTab(currentTab);
            return;
        }

        if (!HasRequiredReferences())
        {
            Debug.LogError("[MainNavigationController] 프리팹의 Root 또는 Button 참조가 누락되었습니다.", this);
            return;
        }

        RegisterButtonListeners();
        topUIController.Initialize(UpgradeManager.Instance);

        UpgradeMenuController upgradeMenu = upgradeRoot.GetComponentInChildren<UpgradeMenuController>(true); // 네비게이션 프리팹에 이미 포함된 업그레이드 UI
        if (upgradeMenu == null)
        {
            upgradeMenu = await uiManager.OpenUI<UpgradeMenuController>(null, UILayer.Main); // 프리팹에 없을 때만 생성하는 기존 Addressables UI
        }

        if (upgradeMenu == null)
        {
            Debug.LogError("[MainNavigationController] UpgradeMenuController를 만들지 못했습니다.", this);
            return;
        }

        upgradeMenu.transform.SetParent(upgradeRoot.transform, false);
        SetFullStretch(upgradeMenu.GetComponent<RectTransform>());

        isInitialized = true;
        ShowTab(MainUITab.Upgrade);
    }

    // 등록한 버튼 Listener를 제거합니다.
    private void OnDestroy()
    {
        UnregisterButtonListeners();
    }

    // 선택한 메인 탭 Root 하나만 활성화하고 버튼 상태를 갱신합니다.
    public void ShowTab(MainUITab tab)
    {
        if (!isInitialized)
        {
            return;
        }

        currentTab = tab;
        SetActive(infoRoot, tab == MainUITab.Info);
        SetActive(upgradeRoot, tab == MainUITab.Upgrade);
        SetActive(battleRoot, tab == MainUITab.Battle);
        SetActive(challengeRoot, tab == MainUITab.Challenge);
        SetActive(shopRoot, tab == MainUITab.Shop);
        UpdateSelectionObjects();
    }

    // 프리팹에 메인 Root 5개와 버튼 5개가 모두 연결되었는지 확인합니다.
    private bool HasRequiredReferences()
    {
        if (topUIController == null || contentRoot == null || infoRoot == null || upgradeRoot == null || battleRoot == null ||
            challengeRoot == null || shopRoot == null || tabButtons == null || tabButtons.Length != 5 ||
            selectedObjects == null || selectedObjects.Length != 5 || contentGemBoxButton == null || contentAddButton == null)
        {
            return false;
        }

        foreach (Button button in tabButtons)
        {
            if (button == null)
            {
                return false;
            }
        }

        foreach (GameObject selectedObject in selectedObjects) // 프리팹에 연결된 탭별 Focus 오브젝트
        {
            if (selectedObject == null)
            {
                return false;
            }
        }

        return true;
    }

    // 하단 버튼에 탭별 클릭 Listener를 한 번만 등록합니다.
    private void RegisterButtonListeners()
    {
        if (listenersRegistered)
        {
            return;
        }

        AddButtonListener(tabButtons[0], ShowInfoTab);
        AddButtonListener(tabButtons[1], ShowUpgradeTab);
        AddButtonListener(tabButtons[2], ShowBattleTab);
        AddButtonListener(tabButtons[3], ShowChallengeTab);
        AddButtonListener(tabButtons[4], ShowShopTab);
        AddButtonListener(contentGemBoxButton, ShowShopTab);
        AddButtonListener(contentAddButton, ShowShopTab);
        listenersRegistered = true;
    }

    // 등록된 탭별 클릭 Listener를 모두 제거합니다.
    private void UnregisterButtonListeners()
    {
        if (!listenersRegistered || tabButtons == null || tabButtons.Length != 5)
        {
            return;
        }

        RemoveButtonListener(tabButtons[0], ShowInfoTab);
        RemoveButtonListener(tabButtons[1], ShowUpgradeTab);
        RemoveButtonListener(tabButtons[2], ShowBattleTab);
        RemoveButtonListener(tabButtons[3], ShowChallengeTab);
        RemoveButtonListener(tabButtons[4], ShowShopTab);
        RemoveButtonListener(contentGemBoxButton, ShowShopTab);
        RemoveButtonListener(contentAddButton, ShowShopTab);
        listenersRegistered = false;
    }

    // 정보 탭을 표시합니다.
    private void ShowInfoTab()
    {
        ShowTab(MainUITab.Info);
    }

    // 업그레이드 탭을 표시합니다.
    private void ShowUpgradeTab()
    {
        ShowTab(MainUITab.Upgrade);
    }

    // 전투 탭을 표시합니다.
    private void ShowBattleTab()
    {
        ShowTab(MainUITab.Battle);
    }

    // 도전 탭을 표시합니다.
    private void ShowChallengeTab()
    {
        ShowTab(MainUITab.Challenge);
    }

    // 상점 탭을 표시합니다.
    private void ShowShopTab()
    {
        ShowTab(MainUITab.Shop);
    }

    // 현재 탭의 Focus 오브젝트만 켜고 나머지 선택 표시는 끕니다.
    private void UpdateSelectionObjects()
    {
        for (int index = 0; index < selectedObjects.Length; index++) // 선택 표시를 갱신할 탭 순번
        {
            bool selected = index == (int)currentTab; // 현재 버튼의 선택 여부
            selectedObjects[index].SetActive(selected);
        }
    }

    // 버튼이 존재할 때 클릭 Listener를 등록합니다.
    private static void AddButtonListener(Button button, UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }
    }

    // 버튼이 존재할 때 클릭 Listener를 제거합니다.
    private static void RemoveButtonListener(Button button, UnityAction action)
    {
        if (button != null)
        {
            button.onClick.RemoveListener(action);
        }
    }

    // RectTransform을 부모 크기에 맞게 전체 확장합니다.
    private static void SetFullStretch(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    // Root가 존재할 때만 활성 상태를 변경합니다.
    private static void SetActive(GameObject root, bool active)
    {
        if (root != null)
        {
            root.SetActive(active);
            
        }
    }
}
