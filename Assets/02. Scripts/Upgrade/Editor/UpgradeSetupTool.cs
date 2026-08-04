using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 기본 업그레이드 밸런스와 uGUI 프리팹을 안전하게 생성합니다.
public static class UpgradeSetupTool
{
    private const string BalanceFolder = "Assets/02. Scripts/Upgrade";
    private const string PrefabFolder = "Assets/03. Prefabs/UI/Upgrade";

    [MenuItem("Tools/Raising Zombies/Upgrade/Create All Default Assets")]
    public static void CreateAllDefaultAssets()
    {
        EnsureFolder(PrefabFolder);

        UpgradeBalanceSettings balance = CreateDefaultBalanceAssetInternal();
        UpgradeDrawResultView resultPrefab = CreateDrawResultPrefab();
        UpgradeStatRowView statRowPrefab = CreateStatRowPrefab();
        UpgradePanel panelPrefab = CreatePanelPrefab(resultPrefab, statRowPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = panelPrefab == null ? balance : panelPrefab.gameObject;
        EditorGUIUtility.PingObject(Selection.activeObject);

        Debug.Log(
            $"[UpgradeSetupTool] 생성 완료\n" +
            $"밸런스: {AssetDatabase.GetAssetPath(balance)}\n" +
            $"패널: {AssetDatabase.GetAssetPath(panelPrefab)}");
    }

    [MenuItem("Tools/Raising Zombies/Upgrade/Create Default Balance Asset")]
    public static void CreateDefaultBalanceAsset()
    {
        UpgradeBalanceSettings balance = CreateDefaultBalanceAssetInternal();
        AssetDatabase.SaveAssets();
        Selection.activeObject = balance;
        EditorGUIUtility.PingObject(balance);
        Debug.Log($"[UpgradeSetupTool] 기본 밸런스 생성: {AssetDatabase.GetAssetPath(balance)}", balance);
    }

    [MenuItem("Tools/Raising Zombies/Upgrade/Create Upgrade UI Prefabs")]
    public static void CreateUpgradeUiPrefabs()
    {
        EnsureFolder(PrefabFolder);
        UpgradeDrawResultView resultPrefab = CreateDrawResultPrefab();
        UpgradeStatRowView statRowPrefab = CreateStatRowPrefab();
        UpgradePanel panelPrefab = CreatePanelPrefab(resultPrefab, statRowPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = panelPrefab.gameObject;
        EditorGUIUtility.PingObject(panelPrefab.gameObject);
        Debug.Log($"[UpgradeSetupTool] 업그레이드 UI 프리팹 생성: {AssetDatabase.GetAssetPath(panelPrefab)}", panelPrefab);
    }

    private static UpgradeBalanceSettings CreateDefaultBalanceAssetInternal()
    {
        var settings = ScriptableObject.CreateInstance<UpgradeBalanceSettings>();
        var serializedSettings = new SerializedObject(settings);

        SerializedProperty stats = serializedSettings.FindProperty("statDefinitions");
        stats.arraySize = 10;
        SetStat(stats.GetArrayElementAtIndex(0), UpgradeStatType.Health, "HP", UpgradeResultKind.Percent, 0.01f, 50);
        SetStat(stats.GetArrayElementAtIndex(1), UpgradeStatType.Defense, "방어력", UpgradeResultKind.Percent, 0.01f, 50);
        SetStat(stats.GetArrayElementAtIndex(2), UpgradeStatType.Attack, "공격력", UpgradeResultKind.Percent, 0.01f, 60);
        SetStat(stats.GetArrayElementAtIndex(3), UpgradeStatType.InfectionCount, "감염체 수", UpgradeResultKind.Integer, 1f, 100);
        SetStat(stats.GetArrayElementAtIndex(4), UpgradeStatType.MoveSpeed, "이동 속도", UpgradeResultKind.Percent, 0.005f, 80);
        SetStat(stats.GetArrayElementAtIndex(5), UpgradeStatType.AttackSpeed, "공격 속도", UpgradeResultKind.Percent, 0.005f, 100);
        SetStat(stats.GetArrayElementAtIndex(6), UpgradeStatType.ZombieCount, "좀비 수", UpgradeResultKind.Integer, 1f, 180);
        SetStat(stats.GetArrayElementAtIndex(7), UpgradeStatType.StatIncrease, "수치 증가", UpgradeResultKind.GlobalAmplifier, 0.01f, 250);
        SetStat(stats.GetArrayElementAtIndex(8), UpgradeStatType.CriticalChance, "치명타 확률", UpgradeResultKind.Percent, 0.005f, 250);
        SetStat(stats.GetArrayElementAtIndex(9), UpgradeStatType.CriticalDamage, "치명타 피해", UpgradeResultKind.Percent, 0.02f, 250);

        SerializedProperty levels = serializedSettings.FindProperty("gachaLevels");
        levels.arraySize = 8;
        SetLevel(levels.GetArrayElementAtIndex(0), 1, 30, 10, UpgradeStatType.Health, UpgradeStatType.Defense);
        SetLevel(levels.GetArrayElementAtIndex(1), 2, 50, 50, UpgradeStatType.Attack);
        SetLevel(levels.GetArrayElementAtIndex(2), 3, 100, 80, UpgradeStatType.InfectionCount);
        SetLevel(levels.GetArrayElementAtIndex(3), 4, 150, 300, UpgradeStatType.MoveSpeed);
        SetLevel(levels.GetArrayElementAtIndex(4), 5, 300, 500, UpgradeStatType.AttackSpeed);
        SetLevel(levels.GetArrayElementAtIndex(5), 6, 500, 1200, UpgradeStatType.ZombieCount);
        SetLevel(levels.GetArrayElementAtIndex(6), 7, 1000, 3000, UpgradeStatType.StatIncrease);
        SetLevel(levels.GetArrayElementAtIndex(7), 8, 1500, 0, UpgradeStatType.CriticalChance, UpgradeStatType.CriticalDamage);

        serializedSettings.ApplyModifiedPropertiesWithoutUndo();

        string path = AssetDatabase.GenerateUniqueAssetPath($"{BalanceFolder}/UpgradeBalanceSettings_Default.asset");
        AssetDatabase.CreateAsset(settings, path);
        return settings;
    }

    private static void SetStat(
        SerializedProperty property,
        UpgradeStatType statType,
        string displayName,
        UpgradeResultKind resultKind,
        float baseCoefficient,
        int researchBaseCost)
    {
        property.FindPropertyRelative("statType").enumValueIndex = (int)statType;
        property.FindPropertyRelative("displayName").stringValue = displayName;
        property.FindPropertyRelative("resultKind").enumValueIndex = (int)resultKind;
        property.FindPropertyRelative("baseCoefficient").floatValue = baseCoefficient;
        property.FindPropertyRelative("researchBaseCost").intValue = researchBaseCost;
        property.FindPropertyRelative("researchCostGrowth").floatValue = 1.2f;
        property.FindPropertyRelative("researchMaxMultiplierBonus").floatValue = 1f;
        property.FindPropertyRelative("researchCurveRate").floatValue = 0.2f;
    }

    private static void SetLevel(
        SerializedProperty property,
        int level,
        int drawCost,
        int drawsToNextLevel,
        params UpgradeStatType[] newlyUnlockedStats)
    {
        property.FindPropertyRelative("level").intValue = level;
        property.FindPropertyRelative("drawCost").intValue = drawCost;
        property.FindPropertyRelative("drawsToNextLevel").intValue = drawsToNextLevel;

        SerializedProperty unlocked = property.FindPropertyRelative("newlyUnlockedStats");
        unlocked.arraySize = newlyUnlockedStats.Length;
        for (var index = 0; index < newlyUnlockedStats.Length; index++)
        {
            unlocked.GetArrayElementAtIndex(index).enumValueIndex = (int)newlyUnlockedStats[index];
        }
    }

    private static UpgradeDrawResultView CreateDrawResultPrefab()
    {
        GameObject root = CreateUiObject("UpgradeDrawResultView");
        var layout = root.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 4, 4);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;

        var layoutElement = root.AddComponent<LayoutElement>();
        layoutElement.minHeight = 34f;
        layoutElement.preferredHeight = 34f;

        TMP_Text statName = CreateText(root.transform, "StatName", "스탯", 18f, TextAlignmentOptions.Left, 1f, 34f);
        TMP_Text value = CreateText(root.transform, "Value", "+10", 18f, TextAlignmentOptions.Center, 0f, 34f, 90f);
        TMP_Text total = CreateText(root.transform, "Total", "누적 10", 18f, TextAlignmentOptions.Right, 0f, 34f, 120f);

        UpgradeDrawResultView view = root.AddComponent<UpgradeDrawResultView>();
        var serializedView = new SerializedObject(view);
        SetReference(serializedView, "statNameText", statName);
        SetReference(serializedView, "valueText", value);
        SetReference(serializedView, "totalText", total);
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        string path = AssetDatabase.GenerateUniqueAssetPath($"{PrefabFolder}/UpgradeDrawResultView.prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<UpgradeDrawResultView>();
    }

    private static UpgradeStatRowView CreateStatRowPrefab()
    {
        GameObject root = CreateUiObject("UpgradeStatRowView");
        Image background = root.AddComponent<Image>();
        background.color = new Color(0.13f, 0.15f, 0.18f, 0.96f);

        var vertical = root.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(14, 14, 10, 10);
        vertical.spacing = 3f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandHeight = false;

        var rowLayout = root.AddComponent<LayoutElement>();
        rowLayout.minHeight = 220f;
        rowLayout.preferredHeight = 220f;

        TMP_Text statName = CreateText(root.transform, "StatName", "공격력", 23f, TextAlignmentOptions.Left, 0f, 32f);
        TMP_Text raw = CreateText(root.transform, "RawValue", "원본 누적: 42", 17f, TextAlignmentOptions.Left, 0f, 24f);
        TMP_Text effective = CreateText(root.transform, "EffectiveValue", "유효 누적: 48.3", 17f, TextAlignmentOptions.Left, 0f, 24f);
        TMP_Text researchLevel = CreateText(root.transform, "ResearchLevel", "연구 Lv.5", 17f, TextAlignmentOptions.Left, 0f, 24f);
        TMP_Text efficiency = CreateText(root.transform, "PerPointEfficiency", "수치 1당 효율: 1.92%", 17f, TextAlignmentOptions.Left, 0f, 24f);
        TMP_Text finalBonus = CreateText(root.transform, "FinalBonus", "최종 보너스: 공격력 +92.73%", 18f, TextAlignmentOptions.Left, 0f, 26f);
        TMP_Text description = CreateText(root.transform, "Description", string.Empty, 14f, TextAlignmentOptions.Left, 0f, 34f);
        Button researchButton = CreateButton(root.transform, "ResearchButton", "연구 강화 · 143", out TMP_Text researchButtonText);

        UpgradeStatRowView view = root.AddComponent<UpgradeStatRowView>();
        var serializedView = new SerializedObject(view);
        SetReference(serializedView, "statNameText", statName);
        SetReference(serializedView, "rawValueText", raw);
        SetReference(serializedView, "effectiveValueText", effective);
        SetReference(serializedView, "researchLevelText", researchLevel);
        SetReference(serializedView, "perPointEfficiencyText", efficiency);
        SetReference(serializedView, "finalBonusText", finalBonus);
        SetReference(serializedView, "descriptionText", description);
        SetReference(serializedView, "researchButton", researchButton);
        SetReference(serializedView, "researchButtonText", researchButtonText);
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        string path = AssetDatabase.GenerateUniqueAssetPath($"{PrefabFolder}/UpgradeStatRowView.prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<UpgradeStatRowView>();
    }

    private static UpgradePanel CreatePanelPrefab(UpgradeDrawResultView resultPrefab, UpgradeStatRowView statRowPrefab)
    {
        GameObject root = CreateUiObject("UpgradePanel");
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(720f, 1100f);

        Image background = root.AddComponent<Image>();
        background.color = new Color(0.055f, 0.065f, 0.08f, 0.98f);

        var vertical = root.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(20, 20, 20, 20);
        vertical.spacing = 9f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandHeight = false;

        CreateText(root.transform, "Title", "업그레이드", 32f, TextAlignmentOptions.Center, 0f, 46f);

        GameObject drawSection = CreateSection(root.transform, "MutationDrawSection");
        CreateText(drawSection.transform, "SectionTitle", "변이 뽑기", 25f, TextAlignmentOptions.Left, 0f, 36f);
        TMP_Text currency = CreateText(drawSection.transform, "Currency", "현재 재화: 0", 20f, TextAlignmentOptions.Left, 0f, 30f);
        TMP_Text progress = CreateText(drawSection.transform, "GachaProgress", "Lv.1 · 0 / 10", 20f, TextAlignmentOptions.Left, 0f, 30f);
        TMP_Text unlocked = CreateText(drawSection.transform, "UnlockedStats", "해금 스탯: HP, 방어력", 17f, TextAlignmentOptions.Left, 0f, 42f);

        GameObject buttons = CreateUiObject("DrawButtons", drawSection.transform);
        var buttonLayout = buttons.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 10f;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = true;
        var buttonsElement = buttons.AddComponent<LayoutElement>();
        buttonsElement.minHeight = 48f;
        buttonsElement.preferredHeight = 48f;
        Button drawOneButton = CreateButton(buttons.transform, "DrawOneButton", "1회 뽑기 · 30", out TMP_Text drawOneText);
        Button drawTenButton = CreateButton(buttons.transform, "DrawTenButton", "10회 뽑기 · 300", out TMP_Text drawTenText);
        TMP_Text drawStatus = CreateText(drawSection.transform, "DrawStatus", string.Empty, 16f, TextAlignmentOptions.Left, 0f, 26f);

        CreateText(drawSection.transform, "RecentResultsTitle", "최근 뽑기 결과", 20f, TextAlignmentOptions.Left, 0f, 30f);
        GameObject drawResults = CreateUiObject("DrawResults", drawSection.transform);
        var drawResultsLayout = drawResults.AddComponent<VerticalLayoutGroup>();
        drawResultsLayout.spacing = 2f;
        drawResultsLayout.childControlWidth = true;
        drawResultsLayout.childControlHeight = true;
        drawResultsLayout.childForceExpandHeight = false;
        drawResults.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TMP_Text unlockNotice = CreateText(drawSection.transform, "UnlockNotice", "아직 뽑기 결과가 없습니다.", 17f, TextAlignmentOptions.Left, 0f, 42f);

        GameObject researchSection = CreateSection(root.transform, "CoefficientResearchSection");
        var researchSectionElement = researchSection.GetComponent<LayoutElement>();
        researchSectionElement.flexibleHeight = 1f;
        researchSectionElement.minHeight = 380f;
        CreateText(researchSection.transform, "SectionTitle", "계수 연구", 25f, TextAlignmentOptions.Left, 0f, 36f);
        CreateText(
            researchSection.transform,
            "GlobalAmplifierHint",
            "수치 증가는 다른 스탯의 유효 누적 수치를 증폭하며 자기 자신은 증폭하지 않습니다.",
            15f,
            TextAlignmentOptions.Left,
            0f,
            44f);

        CreateScrollView(researchSection.transform, out Transform researchContent);

        UpgradePanel panel = root.AddComponent<UpgradePanel>();
        var serializedPanel = new SerializedObject(panel);
        SetReference(serializedPanel, "currencyText", currency);
        SetReference(serializedPanel, "gachaProgressText", progress);
        SetReference(serializedPanel, "unlockedStatsText", unlocked);
        SetReference(serializedPanel, "drawStatusText", drawStatus);
        SetReference(serializedPanel, "drawOneButton", drawOneButton);
        SetReference(serializedPanel, "drawOneButtonText", drawOneText);
        SetReference(serializedPanel, "drawTenButton", drawTenButton);
        SetReference(serializedPanel, "drawTenButtonText", drawTenText);
        SetReference(serializedPanel, "drawResultsRoot", drawResults.transform);
        SetReference(serializedPanel, "drawResultPrefab", resultPrefab);
        SetReference(serializedPanel, "unlockNoticeText", unlockNotice);
        SetReference(serializedPanel, "researchRowsRoot", researchContent);
        SetReference(serializedPanel, "statRowPrefab", statRowPrefab);
        serializedPanel.ApplyModifiedPropertiesWithoutUndo();

        string path = AssetDatabase.GenerateUniqueAssetPath($"{PrefabFolder}/UpgradePanel.prefab");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<UpgradePanel>();
    }

    private static GameObject CreateSection(Transform parent, string name)
    {
        GameObject section = CreateUiObject(name, parent);
        Image image = section.AddComponent<Image>();
        image.color = new Color(0.09f, 0.105f, 0.13f, 0.96f);

        var layout = section.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 5f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        var element = section.AddComponent<LayoutElement>();
        element.minHeight = 100f;
        return section;
    }

    private static ScrollRect CreateScrollView(Transform parent, out Transform contentTransform)
    {
        GameObject scrollObject = CreateUiObject("ResearchScroll", parent);
        Image scrollBackground = scrollObject.AddComponent<Image>();
        scrollBackground.color = new Color(0.035f, 0.04f, 0.05f, 0.9f);
        var scrollElement = scrollObject.AddComponent<LayoutElement>();
        scrollElement.minHeight = 280f;
        scrollElement.flexibleHeight = 1f;

        var scrollRect = scrollObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = CreateUiObject("Viewport", scrollObject.transform);
        Stretch(viewport.GetComponent<RectTransform>());
        Image viewportImage = viewport.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewport.AddComponent<RectMask2D>();

        GameObject content = CreateUiObject("Content", viewport.transform);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        var contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(4, 4, 4, 4);
        contentLayout.spacing = 6f;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandHeight = false;
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRect;
        contentTransform = content.transform;
        return scrollRect;
    }

    private static Button CreateButton(Transform parent, string name, string label, out TMP_Text labelText)
    {
        GameObject buttonObject = CreateUiObject(name, parent);
        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.18f, 0.42f, 0.62f, 1f);

        var button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;

        var element = buttonObject.AddComponent<LayoutElement>();
        element.minHeight = 44f;
        element.preferredHeight = 44f;
        element.flexibleWidth = 1f;

        labelText = CreateText(buttonObject.transform, "Label", label, 18f, TextAlignmentOptions.Center, 1f, 44f);
        RectTransform labelRect = labelText.rectTransform;
        Stretch(labelRect);
        return button;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        TextAlignmentOptions alignment,
        float flexibleWidth,
        float preferredHeight = 24f,
        float preferredWidth = -1f)
    {
        GameObject textObject = CreateUiObject(name, parent);
        var textComponent = textObject.AddComponent<TextMeshProUGUI>();
        textComponent.text = text;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = Color.white;
        textComponent.raycastTarget = false;
        if (TMP_Settings.defaultFontAsset != null)
        {
            textComponent.font = TMP_Settings.defaultFontAsset;
        }

        var element = textObject.AddComponent<LayoutElement>();
        element.flexibleWidth = flexibleWidth;
        element.minHeight = preferredHeight;
        element.preferredHeight = preferredHeight;
        if (preferredWidth >= 0f)
        {
            element.preferredWidth = preferredWidth;
        }

        return textComponent;
    }

    private static GameObject CreateUiObject(string name, Transform parent = null)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        if (parent != null)
        {
            gameObject.transform.SetParent(parent, false);
        }

        return gameObject;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void SetReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (var index = 1; index < parts.Length; index++)
        {
            string next = $"{current}/{parts[index]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
    }
}
