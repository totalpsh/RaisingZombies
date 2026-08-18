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
    [SerializeField] private GameObject contentRoot; // 모든 메인 탭 Root를 담는 영역
    [SerializeField] private GameObject infoRoot; // 정보 탭의 Placeholder Root
    [SerializeField] private GameObject upgradeRoot; // 기존 업그레이드 UI가 들어가는 Root
    [SerializeField] private GameObject battleRoot; // 전투 탭의 Placeholder Root
    [SerializeField] private GameObject challengeRoot; // 도전 탭의 Placeholder Root
    [SerializeField] private GameObject shopRoot; // 상점 탭의 Placeholder Root

    [Header("하단 네비게이션 버튼")]
    [SerializeField] private Button[] tabButtons; // Info부터 Shop까지 순서대로 연결된 버튼 목록
    [SerializeField] private Color selectedButtonColor = new(0.28f, 0.55f, 0.9f, 1f); // 선택된 버튼의 배경색
    [SerializeField] private Color normalButtonColor = new(0.2f, 0.2f, 0.24f, 1f); // 선택되지 않은 버튼의 배경색

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

        UpgradeMenuController upgradeMenu = await uiManager.OpenUI<UpgradeMenuController>(null, UILayer.Main); // 기존 Addressables 업그레이드 UI 인스턴스
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
        UpdateButtonStates();
    }

    // 프리팹에 메인 Root 5개와 버튼 5개가 모두 연결되었는지 확인합니다.
    private bool HasRequiredReferences()
    {
        if (contentRoot == null || infoRoot == null || upgradeRoot == null || battleRoot == null ||
            challengeRoot == null || shopRoot == null || tabButtons == null || tabButtons.Length != 5)
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

    // 현재 탭에 따라 선택 버튼만 구분되는 색상을 적용합니다.
    private void UpdateButtonStates()
    {
        for (int index = 0; index < tabButtons.Length; index++)
        {
            Image image = tabButtons[index].targetGraphic as Image; // 프리팹 버튼에 연결된 배경 이미지
            if (image == null)
            {
                continue;
            }

            bool selected = index == (int)currentTab; // 현재 버튼의 선택 여부
            image.color = selected ? selectedButtonColor : normalButtonColor;
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
