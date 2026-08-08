#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 재화 강화 기본 에셋, UI 프리팹, 검증 메뉴를 제공합니다.
public static class CurrencyUpgradeSetupTool
{
    private const string BalancePath = "Assets/02. Scripts/Upgrade/CurrencyUpgradeBalanceSettings_Default.asset"; // 기본 밸런스 생성 경로
    private const string PrefabFolder = "Assets/03. Prefabs/UI/Upgrade/Currency"; // 재화 강화 프리팹 폴더
    private const string RowPath = PrefabFolder + "/CurrencyUpgradeRowView.prefab"; // 강화 행 프리팹 경로
    private const string PanelPath = PrefabFolder + "/CurrencyUpgradePanel.prefab"; // 재화 강화 패널 프리팹 경로
    private const string PopupPath = PrefabFolder + "/OfflineCurrencyRewardPopup.prefab"; // 오프라인 팝업 프리팹 경로
    private const string MenuPath = PrefabFolder + "/UpgradeCategoryMenu.prefab"; // 카테고리 메뉴 프리팹 경로

    // 기존 파일을 덮어쓰지 않고 기본 재화 강화 밸런스를 생성합니다.
    [MenuItem("Tools/Raising Zombies/Upgrade/Create Default Currency Upgrade Balance")]
    public static void CreateDefaultBalance()
    {
        CurrencyUpgradeBalanceSettings existing = AssetDatabase.LoadAssetAtPath<CurrencyUpgradeBalanceSettings>(BalancePath); // 기존 기본 밸런스
        if (existing != null)
        {
            Selection.activeObject = existing;
            Debug.Log($"[CurrencyUpgradeSetup] 기존 에셋을 유지했습니다: {BalancePath}");
            return;
        }

        EnsureFolder("Assets/03. Prefabs/UI/Upgrade", "Currency");
        CurrencyUpgradeBalanceSettings balance = ScriptableObject.CreateInstance<CurrencyUpgradeBalanceSettings>(); // 생성할 기본 밸런스
        SerializedObject serialized = new(balance); // private 배열을 설정할 직렬화 객체
        SerializedProperty definitions = serialized.FindProperty("definitions"); // 네 강화 정의 배열
        definitions.arraySize = 4;
        SetDefinition(definitions.GetArrayElementAtIndex(0), CurrencyUpgradeType.CurrencyPerSecond,
            "currency_per_second", "초당 재화", "매초 획득하는 재화를 고정 수치만큼 증가시킵니다.", 20, 50, 1.45f, 1f);
        SetDefinition(definitions.GetArrayElementAtIndex(1), CurrencyUpgradeType.HumanKillBonus,
            "human_kill_bonus", "인간 처치 보너스", "인간을 처치할 때 추가 재화를 획득합니다.", 20, 80, 1.5f, 1f);
        SetDefinition(definitions.GetArrayElementAtIndex(2), CurrencyUpgradeType.OfflineMaxTime,
            "offline_max_time", "오프라인 적립 시간", "오프라인 재화가 저장되는 최대 시간을 늘립니다.", 10, 200, 1.7f, 1f);
        SetDefinition(definitions.GetArrayElementAtIndex(3), CurrencyUpgradeType.OfflineEfficiency,
            "offline_efficiency", "오프라인 적립 효율", "오프라인 시간에 적용되는 재화 생산 효율을 높입니다.", 5, 300, 2f, 0.1f);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(balance, BalancePath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = balance;
        Debug.Log($"[CurrencyUpgradeSetup] 기본 밸런스를 생성했습니다: {BalancePath}");
    }

    // 기존 파일을 유지하면서 재화 강화 UI 프리팹 묶음을 생성합니다.
    [MenuItem("Tools/Raising Zombies/Upgrade/Create Currency Upgrade UI Prefabs")]
    public static void CreateCurrencyUpgradeUiPrefabs()
    {
        EnsureFolder("Assets/03. Prefabs/UI/Upgrade", "Currency");
        CurrencyUpgradeRowView row = LoadOrCreateRowPrefab(); // 패널에 연결할 강화 행
        CurrencyUpgradePanel panel = LoadOrCreatePanelPrefab(row); // 카테고리 메뉴에 연결할 재화 패널
        LoadOrCreatePopupPrefab();
        LoadOrCreateMenuPrefab(panel);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CurrencyUpgradeSetup] UI 프리팹을 확인했습니다: {PrefabFolder}");
    }

    // 카테고리 선택 UI 프리팹을 생성하거나 기존 프리팹을 선택합니다.
    [MenuItem("Tools/Raising Zombies/Upgrade/Setup Upgrade Category UI")]
    public static void SetupUpgradeCategoryUi()
    {
        CreateCurrencyUpgradeUiPrefabs();
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath); // 생성 또는 유지된 카테고리 메뉴
        Selection.activeObject = menu;
        EditorGUIUtility.PingObject(menu);
        Debug.Log("[CurrencyUpgradeSetup] UpgradeCategoryMenu를 씬 Canvas 아래에 배치하고 UpgradeManager와 메인 강화 버튼을 연결하세요.");
    }

    // 밸런스와 생성된 UI의 필수 참조를 검사합니다.
    [MenuItem("Tools/Raising Zombies/Upgrade/Validate Currency Upgrade Setup")]
    public static void ValidateSetup()
    {
        int errorCount = 0; // 발견한 전체 설정 오류 수
        CurrencyUpgradeBalanceSettings balance = AssetDatabase.LoadAssetAtPath<CurrencyUpgradeBalanceSettings>(BalancePath); // 검사할 기본 밸런스
        if (balance == null)
        {
            Debug.LogError($"[CurrencyUpgradeSetup] 밸런스 에셋이 없습니다: {BalancePath}");
            errorCount++;
        }
        else
        {
            System.Collections.Generic.List<string> errors = new(); // 밸런스 내부 오류
            balance.CollectValidationErrors(errors);
            foreach (string error in errors)
            {
                Debug.LogError($"[CurrencyUpgradeSetup] {error}", balance);
                errorCount++;
            }
        }

        errorCount += ValidatePrefab<CurrencyUpgradeRowView>(RowPath);
        errorCount += ValidatePrefab<CurrencyUpgradePanel>(PanelPath);
        errorCount += ValidatePrefab<OfflineCurrencyRewardPopup>(PopupPath);
        errorCount += ValidatePrefab<UpgradeMenuController>(MenuPath);
        errorCount += ValidateReferences<CurrencyUpgradeRowView>(RowPath, "nameText", "descriptionText", "levelText",
            "currentEffectText", "nextEffectText", "costText", "upgradeButton");
        errorCount += ValidateReferences<CurrencyUpgradePanel>(PanelPath, "currencyText", "productionText", "rowsRoot", "rowPrefab");
        errorCount += ValidateReferences<OfflineCurrencyRewardPopup>(PopupPath, "actualTimeText", "appliedTimeText",
            "efficiencyText", "rewardText", "confirmButton");
        errorCount += ValidateReferences<UpgradeMenuController>(MenuPath, "categorySelectionRoot", "zombieUpgradeRoot",
            "currencyUpgradeRoot", "zombieUpgradeButton", "currencyUpgradeButton", "categoryBackButton", "zombieBackButton", "currencyBackButton");
        errorCount += ValidateRuntimeIntegration();

        if (errorCount == 0) Debug.Log("[CurrencyUpgradeSetup] 밸런스 종류·ID·비용·효율 상한과 UI 프리팹 구성을 통과했습니다.");
        else Debug.LogError($"[CurrencyUpgradeSetup] 총 {errorCount}개의 설정 오류가 있습니다.");
    }

    // 직렬화 배열 한 칸에 기본 강화 정의를 기록합니다.
    private static void SetDefinition(SerializedProperty property, CurrencyUpgradeType type, string id, string displayName,
        string description, int maxLevel, int baseCost, float growth, float valuePerLevel)
    {
        property.FindPropertyRelative("type").enumValueIndex = (int)type;
        property.FindPropertyRelative("id").stringValue = id;
        property.FindPropertyRelative("displayName").stringValue = displayName;
        property.FindPropertyRelative("description").stringValue = description;
        property.FindPropertyRelative("maxLevel").intValue = maxLevel;
        property.FindPropertyRelative("baseCost").intValue = baseCost;
        property.FindPropertyRelative("costGrowth").floatValue = growth;
        property.FindPropertyRelative("valuePerLevel").floatValue = valuePerLevel;
    }

    // 강화 행 프리팹을 불러오거나 새로 생성합니다.
    private static CurrencyUpgradeRowView LoadOrCreateRowPrefab()
    {
        CurrencyUpgradeRowView existing = AssetDatabase.LoadAssetAtPath<CurrencyUpgradeRowView>(RowPath); // 기존 강화 행
        if (existing != null) return existing;
        GameObject root = CreateUiObject("CurrencyUpgradeRowView", new Vector2(820f, 190f)); // 행 루트
        VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>(); // 행 내부 세로 배치
        layout.padding = new RectOffset(18, 18, 10, 10);
        layout.spacing = 4f;
        TMP_Text name = CreateText(root.transform, "Name", "강화 이름", 24f); // 이름 텍스트
        TMP_Text description = CreateText(root.transform, "Description", "강화 설명", 17f); // 설명 텍스트
        TMP_Text level = CreateText(root.transform, "Level", "Lv.0 / 10", 18f); // 레벨 텍스트
        TMP_Text current = CreateText(root.transform, "CurrentEffect", "현재 효과", 17f); // 현재 효과 텍스트
        TMP_Text next = CreateText(root.transform, "NextEffect", "다음 효과", 17f); // 다음 효과 텍스트
        Button button = CreateButton(root.transform, "UpgradeButton", "강화 · 100"); // 강화 버튼
        TMP_Text cost = button.GetComponentInChildren<TMP_Text>(); // 버튼 비용 텍스트
        CurrencyUpgradeRowView view = root.AddComponent<CurrencyUpgradeRowView>(); // 행 표시 컴포넌트
        SerializedObject serialized = new(view); // 행 참조 연결 객체
        SetReference(serialized, "nameText", name);
        SetReference(serialized, "descriptionText", description);
        SetReference(serialized, "levelText", level);
        SetReference(serialized, "currentEffectText", current);
        SetReference(serialized, "nextEffectText", next);
        SetReference(serialized, "costText", cost);
        SetReference(serialized, "upgradeButton", button);
        SavePrefab(root, RowPath);
        return AssetDatabase.LoadAssetAtPath<CurrencyUpgradeRowView>(RowPath);
    }

    // 재화 강화 패널 프리팹을 불러오거나 새로 생성합니다.
    private static CurrencyUpgradePanel LoadOrCreatePanelPrefab(CurrencyUpgradeRowView rowPrefab)
    {
        CurrencyUpgradePanel existing = AssetDatabase.LoadAssetAtPath<CurrencyUpgradePanel>(PanelPath); // 기존 재화 패널
        if (existing != null) return existing;
        GameObject root = CreateUiObject("CurrencyUpgradePanel", new Vector2(900f, 1100f)); // 패널 루트
        VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>(); // 패널 내부 세로 배치
        layout.padding = new RectOffset(24, 24, 24, 24);
        layout.spacing = 12f;
        TMP_Text title = CreateText(root.transform, "Title", "재화 강화", 32f); // 패널 제목
        title.alignment = TextAlignmentOptions.Center;
        TMP_Text currency = CreateText(root.transform, "Currency", "현재 재화: 0", 22f); // 현재 재화
        TMP_Text production = CreateText(root.transform, "Production", "초당 재화: 1", 20f); // 초당 생산량
        GameObject rowsObject = CreateUiObject("Rows", new Vector2(840f, 800f)); // 동적 행 부모
        rowsObject.transform.SetParent(root.transform, false);
        VerticalLayoutGroup rowsLayout = rowsObject.AddComponent<VerticalLayoutGroup>(); // 강화 행 세로 배치
        rowsLayout.spacing = 10f;
        rowsLayout.childControlHeight = false;
        CurrencyUpgradePanel panel = root.AddComponent<CurrencyUpgradePanel>(); // 재화 패널 컴포넌트
        SerializedObject serialized = new(panel); // 패널 참조 연결 객체
        SetReference(serialized, "currencyText", currency);
        SetReference(serialized, "productionText", production);
        SetReference(serialized, "rowsRoot", rowsObject.transform);
        SetReference(serialized, "rowPrefab", rowPrefab);
        SavePrefab(root, PanelPath);
        return AssetDatabase.LoadAssetAtPath<CurrencyUpgradePanel>(PanelPath);
    }

    // 오프라인 보상 팝업 프리팹을 불러오거나 새로 생성합니다.
    private static OfflineCurrencyRewardPopup LoadOrCreatePopupPrefab()
    {
        OfflineCurrencyRewardPopup existing = AssetDatabase.LoadAssetAtPath<OfflineCurrencyRewardPopup>(PopupPath); // 기존 오프라인 팝업
        if (existing != null) return existing;
        GameObject root = CreateUiObject("OfflineCurrencyRewardPopup", new Vector2(620f, 430f)); // 팝업 루트
        VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>(); // 팝업 세로 배치
        layout.padding = new RectOffset(30, 30, 30, 30);
        layout.spacing = 12f;
        TMP_Text title = CreateText(root.transform, "Title", "오프라인 보상", 30f); // 팝업 제목
        title.alignment = TextAlignmentOptions.Center;
        TMP_Text actual = CreateText(root.transform, "ActualTime", "실제 오프라인", 20f); // 실제 시간
        TMP_Text applied = CreateText(root.transform, "AppliedTime", "적립 적용 시간", 20f); // 적용 시간
        TMP_Text efficiency = CreateText(root.transform, "Efficiency", "적립 효율", 20f); // 효율
        TMP_Text reward = CreateText(root.transform, "Reward", "획득 재화", 24f); // 보상
        Button confirm = CreateButton(root.transform, "ConfirmButton", "확인"); // 확인 버튼
        OfflineCurrencyRewardPopup popup = root.AddComponent<OfflineCurrencyRewardPopup>(); // 팝업 컴포넌트
        SerializedObject serialized = new(popup); // 팝업 참조 연결 객체
        SetReference(serialized, "actualTimeText", actual);
        SetReference(serialized, "appliedTimeText", applied);
        SetReference(serialized, "efficiencyText", efficiency);
        SetReference(serialized, "rewardText", reward);
        SetReference(serialized, "confirmButton", confirm);
        SavePrefab(root, PopupPath);
        return AssetDatabase.LoadAssetAtPath<OfflineCurrencyRewardPopup>(PopupPath);
    }

    // 중앙 상태 전환이 연결된 카테고리 메뉴 프리팹을 불러오거나 생성합니다.
    private static UpgradeMenuController LoadOrCreateMenuPrefab(CurrencyUpgradePanel currencyPanelPrefab)
    {
        UpgradeMenuController existing = AssetDatabase.LoadAssetAtPath<UpgradeMenuController>(MenuPath); // 기존 카테고리 메뉴
        if (existing != null) return existing;
        GameObject root = CreateUiObject("UpgradeCategoryMenu", new Vector2(1080f, 1600f)); // 메뉴 루트
        GameObject category = CreateUiObject("CategorySelection", new Vector2(900f, 700f)); // 카테고리 선택 화면
        category.transform.SetParent(root.transform, false);
        VerticalLayoutGroup categoryLayout = category.AddComponent<VerticalLayoutGroup>(); // 카테고리 버튼 배치
        categoryLayout.padding = new RectOffset(50, 50, 50, 50);
        categoryLayout.spacing = 30f;
        TMP_Text title = CreateText(category.transform, "Title", "강화 선택", 36f); // 카테고리 제목
        title.alignment = TextAlignmentOptions.Center;
        Button zombieButton = CreateButton(category.transform, "ZombieUpgradeButton", "좀비 강화"); // 좀비 강화 선택 버튼
        Button currencyButton = CreateButton(category.transform, "CurrencyUpgradeButton", "재화 강화"); // 재화 강화 선택 버튼
        Button categoryBack = CreateButton(category.transform, "BackButton", "뒤로"); // 메인 복귀 버튼

        GameObject zombieRoot = CreateUiObject("ZombieUpgradeRoot", new Vector2(980f, 1450f)); // 기존 좀비 패널 화면
        zombieRoot.transform.SetParent(root.transform, false);
        VerticalLayoutGroup zombieLayout = zombieRoot.AddComponent<VerticalLayoutGroup>(); // 좀비 화면 배치
        zombieLayout.spacing = 12f;
        UpgradePanel zombiePrefab = AssetDatabase.LoadAssetAtPath<UpgradePanel>("Assets/03. Prefabs/UI/Upgrade/UpgradePanel.prefab"); // 기존 좀비 강화 패널
        if (zombiePrefab != null) PrefabUtility.InstantiatePrefab(zombiePrefab.gameObject, zombieRoot.transform);
        else CreateText(zombieRoot.transform, "MissingZombiePanel", "기존 UpgradePanel 프리팹을 연결하세요.", 22f);
        Button zombieBack = CreateButton(zombieRoot.transform, "BackButton", "뒤로"); // 좀비 화면 복귀 버튼

        GameObject currencyRoot = CreateUiObject("CurrencyUpgradeRoot", new Vector2(980f, 1450f)); // 재화 강화 화면
        currencyRoot.transform.SetParent(root.transform, false);
        VerticalLayoutGroup currencyLayout = currencyRoot.AddComponent<VerticalLayoutGroup>(); // 재화 화면 배치
        currencyLayout.spacing = 12f;
        if (currencyPanelPrefab != null) PrefabUtility.InstantiatePrefab(currencyPanelPrefab.gameObject, currencyRoot.transform);
        Button currencyBack = CreateButton(currencyRoot.transform, "BackButton", "뒤로"); // 재화 화면 복귀 버튼

        UpgradeMenuController controller = root.AddComponent<UpgradeMenuController>(); // 중앙 화면 전환 컴포넌트
        SerializedObject serialized = new(controller); // 메뉴 참조 연결 객체
        SetReference(serialized, "categorySelectionRoot", category);
        SetReference(serialized, "zombieUpgradeRoot", zombieRoot);
        SetReference(serialized, "currencyUpgradeRoot", currencyRoot);
        SetReference(serialized, "zombieUpgradeButton", zombieButton);
        SetReference(serialized, "currencyUpgradeButton", currencyButton);
        SetReference(serialized, "categoryBackButton", categoryBack);
        SetReference(serialized, "zombieBackButton", zombieBack);
        SetReference(serialized, "currencyBackButton", currencyBack);
        serialized.FindProperty("initialState").enumValueIndex = (int)UpgradeMenuState.CategorySelection;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        SavePrefab(root, MenuPath);
        return AssetDatabase.LoadAssetAtPath<UpgradeMenuController>(MenuPath);
    }

    // 기본 RectTransform과 배경을 가진 UI 오브젝트를 생성합니다.
    private static GameObject CreateUiObject(string name, Vector2 size)
    {
        GameObject gameObject = new(name, typeof(RectTransform), typeof(Image)); // 생성할 UI 오브젝트
        RectTransform rect = gameObject.GetComponent<RectTransform>(); // 크기를 설정할 RectTransform
        rect.sizeDelta = size;
        Image image = gameObject.GetComponent<Image>(); // 기본 배경 이미지
        image.color = new Color(0.09f, 0.1f, 0.13f, 0.96f);
        return gameObject;
    }

    // 지정 부모 아래에 TMP 텍스트를 생성합니다.
    private static TMP_Text CreateText(Transform parent, string name, string value, float fontSize)
    {
        GameObject gameObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI)); // 텍스트 오브젝트
        gameObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>(); // 설정할 TMP 텍스트
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.enableWordWrapping = true;
        return text;
    }

    // 지정 부모 아래에 TMP 라벨을 가진 버튼을 생성합니다.
    private static Button CreateButton(Transform parent, string name, string label)
    {
        GameObject gameObject = new(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement)); // 버튼 오브젝트
        gameObject.transform.SetParent(parent, false);
        Image image = gameObject.GetComponent<Image>(); // 버튼 배경
        image.color = new Color(0.2f, 0.45f, 0.7f, 1f);
        LayoutElement layout = gameObject.GetComponent<LayoutElement>(); // 버튼 권장 크기
        layout.preferredHeight = 70f;
        TMP_Text text = CreateText(gameObject.transform, "Label", label, 20f); // 버튼 라벨
        RectTransform textRect = text.rectTransform; // 라벨 전체 채움 RectTransform
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        text.alignment = TextAlignmentOptions.Center;
        return gameObject.GetComponent<Button>();
    }

    // SerializedObject의 오브젝트 참조를 설정합니다.
    private static void SetReference(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName); // 설정할 직렬화 필드
        if (property != null) property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // 임시 오브젝트를 프리팹으로 저장한 뒤 제거합니다.
    private static void SavePrefab(GameObject root, string path)
    {
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    // 지정 부모 아래 폴더가 없을 때 생성합니다.
    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child; // 확인할 전체 폴더 경로
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    // 지정 프리팹과 필수 컴포넌트가 존재하는지 검사합니다.
    private static int ValidatePrefab<T>(string path) where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); // 검사할 프리팹
        if (prefab != null && prefab.GetComponent<T>() != null) return 0;
        Debug.LogError($"[CurrencyUpgradeSetup] {typeof(T).Name} 프리팹 또는 컴포넌트가 없습니다: {path}");
        return 1;
    }

    // 프리팹 컴포넌트의 필수 Inspector 참조가 연결됐는지 검사합니다.
    private static int ValidateReferences<T>(string path, params string[] propertyNames) where T : Component
    {
        T component = AssetDatabase.LoadAssetAtPath<T>(path); // 참조를 검사할 프리팹 컴포넌트
        if (component == null) return 0;
        SerializedObject serialized = new(component); // 프리팹 직렬화 객체
        int errors = 0; // 이 프리팹에서 찾은 오류 수
        foreach (string propertyName in propertyNames) // 검사할 Inspector 필드명
        {
            SerializedProperty property = serialized.FindProperty(propertyName); // 필수 직렬화 필드
            if (property != null && property.objectReferenceValue != null) continue;
            Debug.LogError($"[CurrencyUpgradeSetup] {typeof(T).Name}.{propertyName} 참조가 비어 있습니다.", component);
            errors++;
        }

        return errors;
    }

    // 저장 필드, 인간 사망 보상 API와 오프라인 처리 경로가 존재하는지 검사합니다.
    private static int ValidateRuntimeIntegration()
    {
        int errors = 0; // 런타임 연결 구조 오류 수
        string[] saveFields =
        {
            "currencyPerSecondLevel", "humanKillBonusLevel", "offlineMaxTimeLevel", "offlineEfficiencyLevel", "lastActivityUtc"
        }; // 구버전 JSON과 함께 유지할 새 저장 필드
        foreach (string fieldName in saveFields) // 검사할 저장 필드명
        {
            if (typeof(UpgradeState).GetField(fieldName) != null) continue;
            Debug.LogError($"[CurrencyUpgradeSetup] UpgradeState 저장 필드가 없습니다: {fieldName}");
            errors++;
        }

        if (typeof(UpgradeManager).GetMethod("GrantHumanKillBonus") == null)
        {
            Debug.LogError("[CurrencyUpgradeSetup] 인간 사망 보상 API가 없습니다.");
            errors++;
        }

        System.Reflection.BindingFlags privateInstance = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic; // private 생명주기 검사 범위
        if (typeof(UpgradeManager).GetMethod("ProcessOfflineReward", privateInstance) == null ||
            typeof(UpgradeManager).GetMethod("OnApplicationQuit", privateInstance) == null)
        {
            Debug.LogError("[CurrencyUpgradeSetup] 오프라인 보상 처리 또는 마지막 UTC 저장 경로가 없습니다.");
            errors++;
        }

        if (typeof(UnitController).GetField("_deathRewardGranted", privateInstance) == null)
        {
            Debug.LogError("[CurrencyUpgradeSetup] 인간 사망 보상 중복 방지 필드가 없습니다.");
            errors++;
        }

        return errors;
    }
}
#endif
