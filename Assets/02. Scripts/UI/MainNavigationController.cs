using System.Threading.Tasks;
using UnityEngine;
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

// UIManager가 만든 기존 업그레이드 UI와 새 메인 탭 루트를 함께 관리합니다.
public sealed class MainNavigationController : MonoBehaviour
{
    private const float BottomNavigationHeight = 120f; // 하단 고정 네비게이션의 높이

    private GameObject contentRoot; // 모든 메인 탭 Root를 담는 영역
    private GameObject infoRoot; // 정보 탭의 Placeholder Root
    private GameObject upgradeRoot; // 기존 업그레이드 UI가 들어가는 Root
    private GameObject battleRoot; // 전투 탭의 Placeholder Root
    private GameObject challengeRoot; // 도전 탭의 Placeholder Root
    private GameObject shopRoot; // 상점 탭의 Placeholder Root
    private Button[] tabButtons; // 탭 순서와 같은 하단 버튼 목록
    private MainUITab currentTab; // 현재 선택된 메인 UI 탭
    private bool isInitialized; // 중복 초기화 방지 상태

    public MainUITab CurrentTab => currentTab; // 외부 시스템이 확인할 현재 메인 탭

    // UIManager의 Main 레이어 안에 메인 UI 골격과 기존 Upgrade UI를 준비합니다.
    public async Task InitializeAsync(UIManager uiManager)
    {
        if (isInitialized)
        {
            ShowTab(currentTab);
            return;
        }

        Transform mainLayer = uiManager.GetLayer(UILayer.Main); // UIManager가 관리하는 Main 레이어
        CreateNavigationLayout(mainLayer);

        UpgradeMenuController upgradeMenu = await uiManager.OpenUI<UpgradeMenuController>(null, UILayer.Main); // 기존 Addressables 업그레이드 UI 인스턴스
        if (upgradeMenu == null)
        {
            Debug.LogError("[MainNavigationController] UpgradeMenuController를 만들지 못했습니다.");
            return;
        }

        upgradeMenu.transform.SetParent(upgradeRoot.transform, false);
        SetFullStretch(upgradeMenu.GetComponent<RectTransform>());

        isInitialized = true;
        ShowTab(MainUITab.Upgrade);
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

    // Content Root와 하단 고정 네비게이션을 런타임에 만듭니다.
    private void CreateNavigationLayout(Transform mainLayer)
    {
        GameObject navigationRoot = CreateRoot("MainNavigation", mainLayer); // 메인 네비게이션 전체 Root
        SetFullStretch(navigationRoot.GetComponent<RectTransform>());

        contentRoot = CreateRoot("ContentRoot", navigationRoot.transform);
        RectTransform contentRect = contentRoot.GetComponent<RectTransform>(); // 탭 콘텐츠가 표시될 영역
        SetFullStretch(contentRect);
        contentRect.offsetMin = new Vector2(0f, BottomNavigationHeight);

        infoRoot = CreatePlaceholderRoot("InfoRoot", "정보", contentRoot.transform);
        upgradeRoot = CreateRoot("UpgradeRoot", contentRoot.transform);
        SetFullStretch(upgradeRoot.GetComponent<RectTransform>());
        battleRoot = CreatePlaceholderRoot("BattleRoot", "전투", contentRoot.transform);
        challengeRoot = CreatePlaceholderRoot("ChallengeRoot", "도전", contentRoot.transform);
        shopRoot = CreatePlaceholderRoot("ShopRoot", "상점", contentRoot.transform);

        SetActive(infoRoot, false);
        SetActive(upgradeRoot, false);
        SetActive(battleRoot, false);
        SetActive(challengeRoot, false);
        SetActive(shopRoot, false);

        CreateBottomNavigation(navigationRoot.transform);
    }

    // 하단에 5개의 동일한 폭의 탭 버튼을 지정된 순서로 만듭니다.
    private void CreateBottomNavigation(Transform parent)
    {
        GameObject bottomNavigation = CreateRoot("BottomNavigation", parent); // 콘텐츠와 분리된 하단 네비게이션 Root
        RectTransform navigationRect = bottomNavigation.GetComponent<RectTransform>(); // 하단 네비게이션 위치 정보
        navigationRect.anchorMin = Vector2.zero;
        navigationRect.anchorMax = Vector2.right;
        navigationRect.pivot = new Vector2(0.5f, 0f);
        navigationRect.anchoredPosition = Vector2.zero;
        navigationRect.sizeDelta = new Vector2(0f, BottomNavigationHeight);

        Image navigationBackground = bottomNavigation.AddComponent<Image>(); // 하단 바 배경 그래픽
        navigationBackground.color = new Color(0.08f, 0.08f, 0.1f, 0.96f);

        string[] labels = { "정보", "업그레이드", "전투", "도전", "상점" }; // 요구된 하단 버튼 표시 순서
        tabButtons = new Button[labels.Length]; // 탭 선택 상태를 표시할 버튼 배열
        for (int index = 0; index < labels.Length; index++)
        {
            int tabIndex = index; // 클릭 람다가 참조할 고정 탭 인덱스
            tabButtons[index] = CreateTabButton(bottomNavigation.transform, labels[index], index, labels.Length);
            tabButtons[index].onClick.AddListener(() => ShowTab((MainUITab)tabIndex));
        }
    }

    // Placeholder Root에 화면 확인용 텍스트를 하나 추가합니다.
    private GameObject CreatePlaceholderRoot(string rootName, string label, Transform parent)
    {
        GameObject root = CreateRoot(rootName, parent); // 아직 기능이 없는 탭의 독립 Root
        SetFullStretch(root.GetComponent<RectTransform>());

        GameObject labelObject = CreateRoot("PlaceholderLabel", root.transform); // Placeholder 화면 이름 텍스트 오브젝트
        RectTransform labelRect = labelObject.GetComponent<RectTransform>(); // 중앙 텍스트 배치 정보
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(400f, 80f);

        Text labelText = labelObject.AddComponent<Text>(); // 기본 UI 텍스트 컴포넌트
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.text = label;
        labelText.fontSize = 36;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = Color.white;
        return root;
    }

    // 탭 이름과 클릭 영역을 가진 하단 버튼 하나를 만듭니다.
    private Button CreateTabButton(Transform parent, string label, int index, int count)
    {
        GameObject buttonObject = CreateRoot($"{label}Button", parent); // 탭 클릭을 처리하는 버튼 오브젝트
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>(); // 버튼의 균등 분할 위치 정보
        float width = 1f / count; // 버튼 하나가 차지할 가로 비율
        buttonRect.anchorMin = new Vector2(index * width, 0f);
        buttonRect.anchorMax = new Vector2((index + 1) * width, 1f);
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;

        Image buttonImage = buttonObject.AddComponent<Image>(); // 선택 상태 색을 표시할 버튼 배경
        buttonImage.color = new Color(0.2f, 0.2f, 0.24f, 1f);
        Button button = buttonObject.AddComponent<Button>(); // 탭 전환 입력 컴포넌트
        button.targetGraphic = buttonImage;

        GameObject textObject = CreateRoot("Label", buttonObject.transform); // 버튼 이름 텍스트 오브젝트
        SetFullStretch(textObject.GetComponent<RectTransform>());
        Text buttonText = textObject.AddComponent<Text>(); // 버튼 표시용 기본 UI 텍스트
        buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        buttonText.text = label;
        buttonText.fontSize = 24;
        buttonText.alignment = TextAnchor.MiddleCenter;
        buttonText.color = Color.white;
        buttonText.raycastTarget = false;
        return button;
    }

    // 현재 탭에 따라 선택 버튼만 구분되는 색상을 적용합니다.
    private void UpdateButtonStates()
    {
        for (int index = 0; index < tabButtons.Length; index++)
        {
            Image image = tabButtons[index].GetComponent<Image>(); // 버튼의 배경 이미지
            bool selected = index == (int)currentTab; // 현재 버튼의 선택 여부
            image.color = selected ? new Color(0.28f, 0.55f, 0.9f, 1f) : new Color(0.2f, 0.2f, 0.24f, 1f);
        }
    }

    // RectTransform을 부모 크기에 맞게 전체 확장합니다.
    private static void SetFullStretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    // 이름과 RectTransform만 가진 UI Root를 만듭니다.
    private static GameObject CreateRoot(string rootName, Transform parent)
    {
        GameObject root = new GameObject(rootName, typeof(RectTransform)); // UI 계층에 추가할 Root 오브젝트
        root.layer = parent.gameObject.layer;
        root.transform.SetParent(parent, false);
        return root;
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
