#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;

// 최소 신체 장비 UI Prefab을 만들고 기존 강화 메뉴와 Addressables에 연결합니다.
public static class BodyEquipmentUISetupTool
{
    private const string UpgradePrefabPath = "Assets/03. Prefabs/UI/Upgrade/UpgradeMenuController.prefab"; // 기존 강화 메뉴 프리팹 경로
    private const string OutputFolder = "Assets/03. Prefabs/UI/Upgrade/BodyEquipment"; // 새 장비 UI 프리팹 폴더
    private static TMP_FontAsset _font; // 기존 강화 메뉴에서 재사용할 TMP Font Asset

    // 없는 UI Prefab만 생성하고 기존 스탯 가챠 화면을 신체 장비 화면으로 전환합니다.
    [MenuItem("Tools/Raising Zombies/Body Equipment/Create Missing UI Prefabs And Connect")]
    public static void CreateMissingUIPrefabsAndConnect()
    {
        EnsureFolder(OutputFolder);
        LoadExistingFont();
        BodyEquipmentItemView itemPrefab = CreateItemPrefabIfMissing(); // 결과와 인벤토리가 공유할 카드 원형
        CreateResultPopupIfMissing(itemPrefab);
        CreateInventoryPopupIfMissing(itemPrefab);
        CreateProbabilityPopupIfMissing();
        CreateResearchPopupIfMissing();
        ConnectUpgradeMenu();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BodyEquipmentUISetup] 누락된 UI Prefab 생성, Addressables 등록과 UpgradeMenu 연결을 완료했습니다. 기존 생성 Prefab은 덮어쓰지 않았습니다.");
    }

    // 기존 강화 메뉴에서 현재 사용 중인 TMP Font Asset을 가져옵니다.
    private static void LoadExistingFont()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UpgradePrefabPath); // Font를 확인할 기존 프리팹
        TMP_Text[] texts = prefab == null ? null : prefab.GetComponentsInChildren<TMP_Text>(true); // 기존 전체 TMP Text
        _font = null;
        if (texts == null) return;
        for (int index = 0; index < texts.Length; index++)
        {
            if (texts[index] == null || texts[index].font == null) continue;
            _font = texts[index].font;
            return;
        }
    }

    // 결과와 인벤토리에서 재사용할 장비 카드 Prefab을 처음 한 번만 만듭니다.
    private static BodyEquipmentItemView CreateItemPrefabIfMissing()
    {
        string path = $"{OutputFolder}/BodyEquipmentItemView.prefab"; // 장비 카드 Prefab 경로
        BodyEquipmentItemView existing = AssetDatabase.LoadAssetAtPath<BodyEquipmentItemView>(path); // 사용자가 수정했을 수 있는 기존 카드
        if (existing != null)
        {
            RegisterAddressable(path, nameof(BodyEquipmentItemView));
            return existing;
        }

        GameObject root = CreateUIObject(nameof(BodyEquipmentItemView), typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement)); // 카드 루트
        Image background = root.GetComponent<Image>(); // 카드 배경 이미지
        background.color = new Color(0.13f, 0.14f, 0.16f, 1f);
        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>(); // 카드 내부 세로 배치
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 4f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        root.GetComponent<LayoutElement>().preferredHeight = 500f;
        Image frame = CreateImage(root.transform, "RarityFrame", Color.white); // 레어도 색 프레임
        frame.gameObject.AddComponent<LayoutElement>().preferredHeight = 8f;
        Image icon = CreateImage(root.transform, "EquipmentIcon", Color.white); // 장비 아이콘
        icon.preserveAspect = true;
        icon.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
        TMP_Text rarity = CreateText(root.transform, "RarityText", "레어도", 21f); // 레어도 Text
        TMP_Text name = CreateText(root.transform, "NameText", "장비 이름", 24f); // 장비명 Text
        TMP_Text slot = CreateText(root.transform, "SlotText", "부위", 18f); // 슬롯 Text
        TMP_Text main = CreateText(root.transform, "MainStatText", "주 스탯", 19f); // 주 스탯 Text
        TMP_Text sub = CreateText(root.transform, "SubStatText", "보조 스탯", 16f); // 보조 스탯 Text
        TMP_Text state = CreateText(root.transform, "StateText", "보관 중", 16f); // 상태 Text
        TMP_Text comparison = CreateText(root.transform, "ComparisonText", string.Empty, 15f); // 비교 Text
        Transform actions = CreateHorizontalGroup(root.transform, "Actions"); // 카드 동작 버튼 부모
        Button equip = CreateButton(actions, "EquipButton", "장착", out _); // 장착 버튼
        Button dismantle = CreateButton(actions, "DismantleButton", "파기", out _); // 파기 버튼
        Button lockButton = CreateButton(actions, "LockButton", "잠금", out TMP_Text lockText); // 잠금 버튼
        BodyEquipmentItemView view = root.AddComponent<BodyEquipmentItemView>(); // 카드 동작 컴포넌트
        SerializedObject serialized = new(view); // 카드 참조 연결용 SerializedObject
        Set(serialized, "rarityFrame", frame);
        Set(serialized, "equipmentIcon", icon);
        Set(serialized, "rarityText", rarity);
        Set(serialized, "nameText", name);
        Set(serialized, "slotText", slot);
        Set(serialized, "mainStatText", main);
        Set(serialized, "subStatText", sub);
        Set(serialized, "stateText", state);
        Set(serialized, "comparisonText", comparison);
        Set(serialized, "lockButtonText", lockText);
        Set(serialized, "equipButton", equip);
        Set(serialized, "dismantleButton", dismantle);
        Set(serialized, "lockButton", lockButton);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path); // 저장된 카드 Prefab
        Object.DestroyImmediate(root);
        RegisterAddressable(path, nameof(BodyEquipmentItemView));
        return saved.GetComponent<BodyEquipmentItemView>();
    }

    // 한 번과 10회 뽑기 결과 Popup을 처음 한 번만 만듭니다.
    private static void CreateResultPopupIfMissing(BodyEquipmentItemView itemPrefab)
    {
        string path = $"{OutputFolder}/BodyDrawResultPopup.prefab"; // 결과 Popup 경로
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) { RegisterAddressable(path, nameof(BodyDrawResultPopup)); return; }
        GameObject root = CreatePopupRoot(nameof(BodyDrawResultPopup), out Transform panel); // 결과 Popup 루트
        TMP_Text title = CreateText(panel, "TitleText", "획득 장비", 32f); // 제목 Text
        ScrollRect scroll = CreateScrollView(panel, "ResultScroll", out Transform content); // 결과 카드 Scroll
        scroll.gameObject.GetComponent<LayoutElement>().flexibleHeight = 1f;
        TMP_Text message = CreateText(panel, "MessageText", string.Empty, 18f); // 결과 안내 Text
        Transform actions = CreateHorizontalGroup(panel, "Footer"); // 하단 버튼 부모
        Button inventory = CreateButton(actions, "InventoryButton", "인벤토리", out _); // 인벤토리 버튼
        Button close = CreateButton(actions, "CloseButton", "닫기", out _); // 닫기 버튼
        GameObject confirmation = CreateConfirmation(panel, out TMP_Text confirmationText, out Button confirm, out Button cancel); // 파기 확인 영역
        BodyDrawResultPopup popup = root.AddComponent<BodyDrawResultPopup>(); // 결과 Popup 동작
        SerializedObject serialized = new(popup); // 결과 Popup 참조 연결
        Set(serialized, "itemsRoot", content);
        Set(serialized, "itemPrefab", itemPrefab);
        Set(serialized, "titleText", title);
        Set(serialized, "messageText", message);
        Set(serialized, "inventoryButton", inventory);
        Set(serialized, "closeButton", close);
        Set(serialized, "confirmationRoot", confirmation);
        Set(serialized, "confirmationText", confirmationText);
        Set(serialized, "confirmDismantleButton", confirm);
        Set(serialized, "cancelDismantleButton", cancel);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SaveAndRegister(root, path, nameof(BodyDrawResultPopup));
    }

    // 최대 50개 카드 재사용 인벤토리 Popup을 처음 한 번만 만듭니다.
    private static void CreateInventoryPopupIfMissing(BodyEquipmentItemView itemPrefab)
    {
        string path = $"{OutputFolder}/BodyInventoryPopup.prefab"; // 인벤토리 Popup 경로
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) { RegisterAddressable(path, nameof(BodyInventoryPopup)); return; }
        GameObject root = CreatePopupRoot(nameof(BodyInventoryPopup), out Transform panel); // 인벤토리 Popup 루트
        TMP_Text title = CreateText(panel, "TitleText", "신체 장비 인벤토리", 32f); // 제목 Text
        title.alignment = TextAlignmentOptions.Center;
        Transform summary = CreateHorizontalGroup(panel, "Summary"); // 상단 요약 부모
        CreateText(summary, "TicketLabel", "뽑기권", 18f);
        TMP_Text ticket = CreateText(summary, "TicketText", "0", 18f); // 뽑기권 Text
        CreateText(summary, "CountLabel", "보유", 18f);
        TMP_Text count = CreateText(summary, "InventoryCountText", "0 / 500", 18f); // 보유량 Text
        ScrollRect scroll = CreateScrollView(panel, "InventoryScroll", out Transform content); // 장비 목록 Scroll
        scroll.gameObject.GetComponent<LayoutElement>().flexibleHeight = 1f;
        TMP_Text message = CreateText(panel, "MessageText", string.Empty, 18f); // 파기 결과 Text
        Transform footer = CreateHorizontalGroup(panel, "Footer"); // 페이지 버튼 부모
        Button previous = CreateButton(footer, "PreviousButton", "이전", out _); // 이전 버튼
        TMP_Text page = CreateText(footer, "PageText", "1 / 1", 20f); // 페이지 Text
        Button next = CreateButton(footer, "NextButton", "다음", out _); // 다음 버튼
        Button close = CreateButton(footer, "CloseButton", "닫기", out _); // 닫기 버튼
        GameObject confirmation = CreateConfirmation(panel, out TMP_Text confirmationText, out Button confirm, out Button cancel); // 파기 확인 영역
        BodyInventoryPopup popup = root.AddComponent<BodyInventoryPopup>(); // 인벤토리 동작
        SerializedObject serialized = new(popup); // 인벤토리 참조 연결
        Set(serialized, "itemsRoot", content);
        Set(serialized, "itemPrefab", itemPrefab);
        Set(serialized, "ticketText", ticket);
        Set(serialized, "inventoryCountText", count);
        Set(serialized, "pageText", page);
        Set(serialized, "messageText", message);
        Set(serialized, "previousButton", previous);
        Set(serialized, "nextButton", next);
        Set(serialized, "closeButton", close);
        Set(serialized, "confirmationRoot", confirmation);
        Set(serialized, "confirmationText", confirmationText);
        Set(serialized, "confirmDismantleButton", confirm);
        Set(serialized, "cancelDismantleButton", cancel);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SaveAndRegister(root, path, nameof(BodyInventoryPopup));
    }

    // 실제 Weight 기반 12개 확률 Popup을 처음 한 번만 만듭니다.
    private static void CreateProbabilityPopupIfMissing()
    {
        string path = $"{OutputFolder}/BodyRarityProbabilityPopup.prefab"; // 확률 Popup 경로
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) { RegisterAddressable(path, nameof(BodyRarityProbabilityPopup)); return; }
        GameObject root = CreatePopupRoot(nameof(BodyRarityProbabilityPopup), out Transform panel); // 확률 Popup 루트
        TMP_Text title = CreateText(panel, "TitleText", "레어도 확률", 32f); // 제목 Text
        title.alignment = TextAlignmentOptions.Center;
        TMP_Text level = CreateText(panel, "ResearchLevelText", "뽑기 연구 Lv.1", 23f); // 연구 레벨 Text
        TMP_Text probability = CreateText(panel, "ProbabilityText", string.Empty, 19f); // 12개 실제 확률 Text
        probability.gameObject.GetComponent<LayoutElement>().flexibleHeight = 1f;
        Button close = CreateButton(panel, "CloseButton", "닫기", out _); // 닫기 버튼
        BodyRarityProbabilityPopup popup = root.AddComponent<BodyRarityProbabilityPopup>(); // 확률 Popup 동작
        SerializedObject serialized = new(popup); // 확률 Popup 참조 연결
        Set(serialized, "researchLevelText", level);
        Set(serialized, "probabilityText", probability);
        Set(serialized, "closeButton", close);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SaveAndRegister(root, path, nameof(BodyRarityProbabilityPopup));
    }

    // 실제 시간 연구와 현재·다음 확률 Popup을 처음 한 번만 만듭니다.
    private static void CreateResearchPopupIfMissing()
    {
        string path = $"{OutputFolder}/BodyResearchPopup.prefab"; // 연구 Popup 경로
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) { RegisterAddressable(path, nameof(BodyResearchPopup)); return; }
        GameObject root = CreatePopupRoot(nameof(BodyResearchPopup), out Transform panel); // 연구 Popup 루트
        TMP_Text title = CreateText(panel, "TitleText", "신체 뽑기 연구", 32f); // 제목 Text
        title.alignment = TextAlignmentOptions.Center;
        Transform summary = CreateHorizontalGroup(panel, "Summary"); // 연구 요약 부모
        TMP_Text level = CreateText(summary, "ResearchLevelText", "뽑기 연구 Lv.1", 21f); // 연구 레벨 Text
        TMP_Text point = CreateText(summary, "ResearchPointText", "0", 21f); // 연구 포인트 Text
        TMP_Text required = CreateText(summary, "RequiredPointText", "0", 21f); // 필요 포인트 Text
        TMP_Text duration = CreateText(summary, "DurationText", "00:00:00", 21f); // 연구 시간 Text
        Transform probabilities = CreateHorizontalGroup(panel, "Probabilities"); // 현재·다음 확률 부모
        probabilities.gameObject.GetComponent<LayoutElement>().flexibleHeight = 1f;
        Transform currentColumn = CreateVerticalGroup(probabilities, "CurrentColumn"); // 현재 확률 열
        CreateText(currentColumn, "CurrentLabel", "현재 확률", 20f);
        TMP_Text current = CreateText(currentColumn, "CurrentProbabilityText", string.Empty, 15f); // 현재 확률 Text
        Transform nextColumn = CreateVerticalGroup(probabilities, "NextColumn"); // 다음 확률 열
        CreateText(nextColumn, "NextLabel", "다음 단계 확률", 20f);
        TMP_Text next = CreateText(nextColumn, "NextProbabilityText", string.Empty, 15f); // 다음 확률 Text
        GameObject timerRoot = CreateUIObject("TimerRoot", typeof(VerticalLayoutGroup)); // 진행 중 Timer 부모
        timerRoot.transform.SetParent(panel, false);
        TMP_Text timer = CreateText(timerRoot.transform, "TimerText", string.Empty, 24f); // 남은 시간 Text
        TMP_Text message = CreateText(panel, "MessageText", string.Empty, 18f); // 연구 결과 Text
        Transform footer = CreateHorizontalGroup(panel, "Footer"); // 연구 버튼 부모
        Button start = CreateButton(footer, "StartButton", "연구 시작", out _); // 연구 시작 버튼
        Button close = CreateButton(footer, "CloseButton", "닫기", out _); // 닫기 버튼
        BodyResearchPopup popup = root.AddComponent<BodyResearchPopup>(); // 연구 Popup 동작
        SerializedObject serialized = new(popup); // 연구 Popup 참조 연결
        Set(serialized, "researchLevelText", level);
        Set(serialized, "researchPointText", point);
        Set(serialized, "requiredPointText", required);
        Set(serialized, "durationText", duration);
        Set(serialized, "currentProbabilityText", current);
        Set(serialized, "nextProbabilityText", next);
        Set(serialized, "timerText", timer);
        Set(serialized, "messageText", message);
        Set(serialized, "startButton", start);
        Set(serialized, "closeButton", close);
        Set(serialized, "timerRoot", timerRoot);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SaveAndRegister(root, path, nameof(BodyResearchPopup));
    }

    // 기존 UpgradeMenu의 Stat Root 안에 새 장비 Draw 화면을 추가하고 기존 가챠 화면을 숨깁니다.
    private static void ConnectUpgradeMenu()
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(UpgradePrefabPath); // 수정할 프리팹 임시 루트
        try
        {
            UpgradeMenuController controller = contents.GetComponent<UpgradeMenuController>(); // 기존 메뉴 상태 Controller
            if (controller == null) { Debug.LogError("[BodyEquipmentUISetup] UpgradeMenuController 컴포넌트를 찾을 수 없습니다."); return; }
            SerializedObject menuSerialized = new(controller); // 기존 직렬화 참조 조회
            GameObject statRoot = menuSerialized.FindProperty("statUpgradeRoot").objectReferenceValue as GameObject; // 기존 스탯 가챠 화면 Root
            if (statRoot == null) { Debug.LogError("[BodyEquipmentUISetup] statUpgradeRoot 참조가 없습니다."); return; }
            Transform existingPanel = GetDirectChild(statRoot.transform, "BodyEquipmentDrawRoot"); // 이미 연결된 새 Draw 화면
            if (existingPanel != null) return;
            for (int index = 0; index < statRoot.transform.childCount; index++) statRoot.transform.GetChild(index).gameObject.SetActive(false);
            CreateDrawPanel(statRoot.transform, controller);
            Button selector = menuSerialized.FindProperty("statUpgradeButton").objectReferenceValue as Button; // 종류 선택의 기존 스탯 버튼
            TMP_Text selectorText = selector == null ? null : selector.GetComponentInChildren<TMP_Text>(true); // 기존 선택 버튼 문구
            if (selectorText != null && (selectorText.text.Contains("스탯") || selectorText.text.Contains("좀비"))) selectorText.text = "신체 장비";
            PrefabUtility.SaveAsPrefabAsset(contents, UpgradePrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    // 강화 메뉴 내부에 Ticket, Draw와 세 개 Popup 버튼을 가진 최소 화면을 만듭니다.
    private static void CreateDrawPanel(Transform parent, UpgradeMenuController controller)
    {
        GameObject root = CreateUIObject("BodyEquipmentDrawRoot", typeof(Image), typeof(VerticalLayoutGroup)); // 새 장비 Draw 화면 Root
        root.transform.SetParent(parent, false);
        Stretch(root.GetComponent<RectTransform>());
        root.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.11f, 1f);
        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>(); // 화면 세로 배치
        layout.padding = new RectOffset(28, 28, 28, 28);
        layout.spacing = 14f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        TMP_Text title = CreateText(root.transform, "TitleText", "좀비 신체 장비 뽑기", 38f); // 화면 제목
        title.alignment = TextAlignmentOptions.Center;
        Transform summary = CreateHorizontalGroup(root.transform, "Summary"); // 상단 현재 상태 부모
        CreateText(summary, "TicketLabel", "신체 뽑기권", 21f);
        TMP_Text ticket = CreateText(summary, "TicketText", "0", 24f); // 뽑기권 Text
        CreateText(summary, "ResearchLabel", "뽑기 연구", 21f);
        TMP_Text research = CreateText(summary, "ResearchLevelText", "Lv.1", 24f); // 연구 레벨 Text
        CreateText(summary, "InventoryLabel", "인벤토리", 21f);
        TMP_Text inventory = CreateText(summary, "InventoryCountText", "0 / 500", 24f); // 인벤토리 Text
        Transform drawActions = CreateHorizontalGroup(root.transform, "DrawActions"); // Draw 버튼 부모
        Button drawOne = CreateButton(drawActions, "DrawOneButton", "1회 뽑기", out _); // 1회 Draw 버튼
        TMP_Text singleCost = CreateText(drawActions, "SingleCostText", "1", 22f); // 1회 비용 Text
        Button drawTen = CreateButton(drawActions, "DrawTenButton", "10회 뽑기", out _); // 10회 Draw 버튼
        TMP_Text tenCost = CreateText(drawActions, "TenCostText", "10", 22f); // 10회 비용 Text
        TMP_Text recent = CreateText(root.transform, "RecentResultText", "최근 획득 없음", 20f); // 최근 결과 Text
        TMP_Text message = CreateText(root.transform, "MessageText", string.Empty, 19f); // 실패 안내 Text
        message.color = new Color(1f, 0.55f, 0.5f, 1f);
        Transform navigation = CreateHorizontalGroup(root.transform, "Navigation"); // Popup 버튼 부모
        Button researchButton = CreateButton(navigation, "ResearchButton", "뽑기 연구", out _); // 연구 Popup 버튼
        Button probabilityButton = CreateButton(navigation, "ProbabilityButton", "레어도 확률", out _); // 확률 Popup 버튼
        Button inventoryButton = CreateButton(navigation, "InventoryButton", "인벤토리", out _); // 인벤토리 Popup 버튼
        Button back = CreateButton(root.transform, "BackButton", "뒤로", out _); // 메뉴 뒤로가기 버튼
        BodyDrawPanel panel = root.AddComponent<BodyDrawPanel>(); // 장비 Draw 동작
        SerializedObject serialized = new(panel); // Draw 화면 참조 연결
        Set(serialized, "menuController", controller);
        Set(serialized, "ticketText", ticket);
        Set(serialized, "singleCostText", singleCost);
        Set(serialized, "tenCostText", tenCost);
        Set(serialized, "researchLevelText", research);
        Set(serialized, "inventoryCountText", inventory);
        Set(serialized, "recentResultText", recent);
        Set(serialized, "messageText", message);
        Set(serialized, "drawOneButton", drawOne);
        Set(serialized, "drawTenButton", drawTen);
        Set(serialized, "researchButton", researchButton);
        Set(serialized, "probabilityButton", probabilityButton);
        Set(serialized, "inventoryButton", inventoryButton);
        Set(serialized, "backButton", back);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // 공통 화면 전체 Popup Root와 중앙 Panel을 만듭니다.
    private static GameObject CreatePopupRoot(string name, out Transform panel)
    {
        GameObject root = CreateUIObject(name, typeof(Image)); // 화면 전체 Popup Root
        Stretch(root.GetComponent<RectTransform>());
        root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);
        GameObject panelObject = CreateUIObject("Panel", typeof(Image), typeof(VerticalLayoutGroup)); // 중앙 내용 Panel
        panelObject.transform.SetParent(root.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>(); // Panel 배치 Rect
        panelRect.anchorMin = new Vector2(0.05f, 0.04f);
        panelRect.anchorMax = new Vector2(0.95f, 0.96f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelObject.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.13f, 1f);
        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>(); // Popup 내용 세로 배치
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 10f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        panel = panelObject.transform;
        return root;
    }

    // 재사용 카드 목록에 사용할 Scroll View를 만듭니다.
    private static ScrollRect CreateScrollView(Transform parent, string name, out Transform content)
    {
        GameObject root = CreateUIObject(name, typeof(Image), typeof(ScrollRect), typeof(LayoutElement)); // Scroll Root
        root.transform.SetParent(parent, false);
        root.GetComponent<Image>().color = new Color(0.04f, 0.045f, 0.055f, 1f);
        root.GetComponent<LayoutElement>().preferredHeight = 520f;
        GameObject viewport = CreateUIObject("Viewport", typeof(Image), typeof(Mask)); // Scroll 가림 영역
        viewport.transform.SetParent(root.transform, false);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        GameObject contentObject = CreateUIObject("Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)); // 카드 Content
        contentObject.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>(); // Content 배치 Rect
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = contentObject.GetComponent<VerticalLayoutGroup>(); // 카드 세로 배치
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = root.GetComponent<ScrollRect>(); // 실제 Scroll 동작
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        content = contentObject.transform;
        return scroll;
    }

    // 파기 확인 문구와 확인·취소 버튼 영역을 만듭니다.
    private static GameObject CreateConfirmation(Transform parent, out TMP_Text message, out Button confirm, out Button cancel)
    {
        GameObject root = CreateUIObject("DismantleConfirmation", typeof(Image), typeof(VerticalLayoutGroup)); // 파기 확인 Root
        root.transform.SetParent(parent, false);
        root.GetComponent<Image>().color = new Color(0.25f, 0.08f, 0.08f, 1f);
        message = CreateText(root.transform, "ConfirmationText", "장비를 파기합니까?", 19f);
        message.alignment = TextAlignmentOptions.Center;
        Transform actions = CreateHorizontalGroup(root.transform, "Actions"); // 확인 버튼 부모
        confirm = CreateButton(actions, "ConfirmButton", "파기", out _);
        cancel = CreateButton(actions, "CancelButton", "취소", out _);
        root.SetActive(false);
        return root;
    }

    // 기본 Image와 Button을 가진 UI 버튼을 만듭니다.
    private static Button CreateButton(Transform parent, string name, string label, out TMP_Text labelText)
    {
        GameObject root = CreateUIObject(name, typeof(Image), typeof(Button), typeof(LayoutElement)); // 버튼 Root
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>(); // 버튼 배경
        image.color = new Color(0.22f, 0.36f, 0.48f, 1f);
        Button button = root.GetComponent<Button>(); // 버튼 동작
        button.targetGraphic = image;
        LayoutElement element = root.GetComponent<LayoutElement>(); // 버튼 레이아웃 크기
        element.preferredHeight = 54f;
        element.flexibleWidth = 1f;
        labelText = CreateText(root.transform, "Label", label, 20f);
        Stretch(labelText.rectTransform);
        labelText.alignment = TextAlignmentOptions.Center;
        return button;
    }

    // 부모에 맞는 TMP Text를 만듭니다.
    private static TMP_Text CreateText(Transform parent, string name, string value, float size)
    {
        GameObject root = CreateUIObject(name, typeof(TextMeshProUGUI), typeof(LayoutElement)); // 새 Text 오브젝트
        root.transform.SetParent(parent, false);
        TMP_Text text = root.GetComponent<TMP_Text>(); // 생성한 TMP Text
        text.text = value;
        text.fontSize = size;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        if (_font != null) text.font = _font;
        LayoutElement element = root.GetComponent<LayoutElement>(); // Text 기본 높이
        element.minHeight = Mathf.Max(30f, size + 10f);
        return text;
    }

    // 단순 색 Image를 만듭니다.
    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject root = CreateUIObject(name, typeof(Image)); // 새 Image 오브젝트
        root.transform.SetParent(parent, false);
        Image image = root.GetComponent<Image>(); // 생성한 Image
        image.color = color;
        return image;
    }

    // 자식들을 가로로 배치하는 Layout Root를 만듭니다.
    private static Transform CreateHorizontalGroup(Transform parent, string name)
    {
        GameObject root = CreateUIObject(name, typeof(HorizontalLayoutGroup), typeof(LayoutElement)); // 가로 Layout Root
        root.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>(); // 가로 배치 설정
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        root.GetComponent<LayoutElement>().preferredHeight = 60f;
        return root.transform;
    }

    // 자식들을 세로로 배치하는 Layout Root를 만듭니다.
    private static Transform CreateVerticalGroup(Transform parent, string name)
    {
        GameObject root = CreateUIObject(name, typeof(VerticalLayoutGroup), typeof(LayoutElement)); // 세로 Layout Root
        root.transform.SetParent(parent, false);
        VerticalLayoutGroup layout = root.GetComponent<VerticalLayoutGroup>(); // 세로 배치 설정
        layout.spacing = 4f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        root.GetComponent<LayoutElement>().flexibleWidth = 1f;
        return root.transform;
    }

    // RectTransform과 지정 Component를 가진 UI GameObject를 만듭니다.
    private static GameObject CreateUIObject(string name, params System.Type[] componentTypes)
    {
        System.Type[] types = new System.Type[componentTypes.Length + 1]; // RectTransform을 앞에 넣을 Component 배열
        types[0] = typeof(RectTransform);
        for (int index = 0; index < componentTypes.Length; index++) types[index + 1] = componentTypes[index];
        return new GameObject(name, types);
    }

    // RectTransform을 부모 전체 영역에 맞춥니다.
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 직계 자식 이름만 검사해 기존 생성 UI를 보존합니다.
    private static Transform GetDirectChild(Transform parent, string name)
    {
        for (int index = 0; index < parent.childCount; index++)
            if (parent.GetChild(index).name == name) return parent.GetChild(index);
        return null;
    }

    // SerializedField 참조를 안전하게 연결합니다.
    private static void Set(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName); // 연결할 직렬화 필드
        if (property == null) { Debug.LogError($"[BodyEquipmentUISetup] 직렬화 필드를 찾을 수 없습니다: {propertyName}"); return; }
        property.objectReferenceValue = value;
    }

    // 새 프리팹을 저장하고 UI Addressables 그룹에 타입 이름으로 등록합니다.
    private static void SaveAndRegister(GameObject root, string path, string address)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        RegisterAddressable(path, address);
    }

    // UIManager가 타입 이름으로 생성할 수 있게 UI 그룹 Address를 등록합니다.
    private static void RegisterAddressable(string assetPath, string address)
    {
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(false); // 현재 프로젝트 Addressables 설정
        if (settings == null) { Debug.LogError("[BodyEquipmentUISetup] Addressables Settings가 없습니다."); return; }
        AddressableAssetGroup group = settings.FindGroup("UI") ?? settings.DefaultGroup; // 기존 UI Addressable 그룹
        string guid = AssetDatabase.AssetPathToGUID(assetPath); // 등록할 프리팹 GUID
        if (string.IsNullOrWhiteSpace(guid)) return;
        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group); // 기존 항목이 있으면 UI 그룹으로 이동
        entry.address = address;
        EditorUtility.SetDirty(settings);
    }

    // 요청 폴더가 없을 때만 부모부터 순서대로 만듭니다.
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/'); // 생성할 폴더의 부모 경로
        string name = Path.GetFileName(path); // 생성할 마지막 폴더 이름
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name)) return;
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
