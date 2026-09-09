#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 기존 Asset을 덮어쓰지 않고 신체 장비 기본 Balance Asset을 생성합니다.
public static class BodyEquipmentSetupTool
{
    private const string RootFolder = "Assets/Resources/BodyEquipment"; // Runtime에서 한 번 불러올 기본 Asset 폴더
    private const string StatFolder = RootFolder + "/Stats"; // 장비 스탯 원형 폴더
    private const string RarityFolder = RootFolder + "/Rarities"; // 12개 레어도 원형 폴더
    private const string EquipmentFolder = RootFolder + "/Equipment"; // 8개 부위 장비 원형 폴더
    private const string DatabasePath = RootFolder + "/BodyEquipmentDatabase_Default.asset"; // 기본 Database Asset 경로
    private const string ResearchPath = RootFolder + "/BodyDrawResearchSettings_Default.asset"; // 기본 연구 확률표 Asset 경로

    // 누락된 기본 Asset만 생성하고 기존 Sprite와 Balance 값은 유지합니다.
    [MenuItem("Tools/Raising Zombies/Body Equipment/Create Missing Default Assets")]
    public static void CreateMissingDefaultAssets()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(StatFolder);
        EnsureFolder(RarityFolder);
        EnsureFolder(EquipmentFolder);

        Dictionary<EquipmentStatType, EquipmentStatDefinitionSO> stats = CreateStats(); // 장비 Pool과 Database에 연결할 전체 스탯
        BodyRarityDefinitionSO[] rarities = CreateRarities(); // Database에 연결할 Tier 1~12 레어도
        BodyEquipmentDefinitionSO[] equipment = CreateEquipment(stats); // Database에 연결할 8개 기본 부위
        CreateDatabase(stats, rarities, equipment);
        CreateResearchSettings();
        MigrateDefaultQuestAsset();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[BodyEquipmentSetup] 누락된 기본 장비/레어도/스탯/연구 Asset 생성과 Quest 조건 이전을 완료했습니다. 기존 Asset은 덮어쓰지 않았습니다.");
    }

    // 최소 13개 전투 스탯 원형을 누락된 경우에만 생성합니다.
    private static Dictionary<EquipmentStatType, EquipmentStatDefinitionSO> CreateStats()
    {
        Dictionary<EquipmentStatType, EquipmentStatDefinitionSO> values = new(); // 장비 Pool 구성에 사용할 타입별 원형
        AddStat(values, EquipmentStatType.Attack, "attack", "공격력", EquipmentStatValueKind.Flat, 1f, 5f, 20f, "+{0:0.##}", 0f);
        AddStat(values, EquipmentStatType.Health, "health", "체력", EquipmentStatValueKind.Flat, 8f, 40f, 120f, "+{0:0.##}", 0f);
        AddStat(values, EquipmentStatType.Defense, "defense", "방어력", EquipmentStatValueKind.Flat, 0.7f, 1f, 5f, "+{0:0.##}", 0f);
        AddStat(values, EquipmentStatType.AttackSpeed, "attack_speed", "공격속도", EquipmentStatValueKind.Percent, 0.04f, 1f, 4f, "+{0:0.##}%", 200f);
        AddStat(values, EquipmentStatType.MoveSpeed, "move_speed", "이동속도", EquipmentStatValueKind.Percent, 0.03f, 1f, 3f, "+{0:0.##}%", 100f);
        AddStat(values, EquipmentStatType.LifeSteal, "life_steal", "흡혈", EquipmentStatValueKind.Percent, 0.015f, 0.5f, 1.5f, "+{0:0.##}%", 50f);
        AddStat(values, EquipmentStatType.HealthRegen, "health_regen", "체력 재생", EquipmentStatValueKind.Flat, 0.2f, 0.2f, 1.2f, "+{0:0.##}", 0f);
        AddStat(values, EquipmentStatType.DamagePercent, "damage_percent", "데미지 증가", EquipmentStatValueKind.Percent, 0.05f, 2f, 5f, "+{0:0.##}%", 500f);
        AddStat(values, EquipmentStatType.HealthPercent, "health_percent", "최대 체력 증가", EquipmentStatValueKind.Percent, 0.05f, 2f, 5f, "+{0:0.##}%", 500f);
        AddStat(values, EquipmentStatType.DefensePercent, "defense_percent", "방어력 증가", EquipmentStatValueKind.Percent, 0.05f, 2f, 5f, "+{0:0.##}%", 500f);
        AddStat(values, EquipmentStatType.CriticalChance, "critical_chance", "치명타 확률", EquipmentStatValueKind.Percent, 0.02f, 1f, 2f, "+{0:0.##}%", 75f);
        AddStat(values, EquipmentStatType.CriticalDamage, "critical_damage", "치명타 피해", EquipmentStatValueKind.Percent, 0.1f, 5f, 10f, "+{0:0.##}%", 500f);
        AddStat(values, EquipmentStatType.DamageReduction, "damage_reduction", "피해 감소", EquipmentStatValueKind.Percent, 0.02f, 1f, 3f, "+{0:0.##}%", 80f);
        return values;
    }

    // 한 스탯 Asset을 기존 값 보존 정책으로 준비합니다.
    private static void AddStat(Dictionary<EquipmentStatType, EquipmentStatDefinitionSO> values, EquipmentStatType type,
        string id, string displayName, EquipmentStatValueKind kind, float mainScale, float subMin, float subMax, string format, float maximum)
    {
        string path = $"{StatFolder}/EquipmentStat_{type}.asset"; // 타입별 안정적인 Asset 경로
        EquipmentStatDefinitionSO asset = AssetDatabase.LoadAssetAtPath<EquipmentStatDefinitionSO>(path); // 사용자가 이미 조절한 기존 Asset
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<EquipmentStatDefinitionSO>();
            SerializedObject serialized = new(asset); // private Inspector 필드를 초기화할 편집 객체
            Set(serialized, "statId", id);
            Set(serialized, "statType", (int)type);
            Set(serialized, "displayName", displayName);
            Set(serialized, "valueKind", (int)kind);
            Set(serialized, "mainStatScale", mainScale);
            Set(serialized, "subStatMin", subMin);
            Set(serialized, "subStatMax", subMax);
            Set(serialized, "uiFormat", format);
            Set(serialized, "maximumValue", maximum);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, path);
        }
        values[type] = asset;
    }

    // Tier 1~12 레어도 Asset을 기본 Roll 값으로 준비합니다.
    private static BodyRarityDefinitionSO[] CreateRarities()
    {
        string[] names = { "일반", "고급", "희귀", "정예", "영웅", "유일", "전설", "신화", "고대", "변이", "초월", "완전체" }; // 초기 한국어 표시명
        float[] minimums = { 1f, 8f, 15f, 30f, 50f, 100f, 180f, 300f, 500f, 800f, 1300f, 2000f }; // Tier별 Main 최소 Roll
        float[] maximums = { 10f, 20f, 50f, 80f, 150f, 250f, 400f, 650f, 1000f, 1600f, 2500f, 4000f }; // Tier별 Main 최대 Roll
        int[] subMinimums = { 0, 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4 }; // Tier별 보조 옵션 최소 개수
        int[] subMaximums = { 0, 1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4 }; // Tier별 보조 옵션 최대 개수
        int[] points = { 1, 2, 4, 8, 15, 25, 40, 65, 100, 160, 250, 400 }; // Tier별 파기 연구 포인트
        BodyRarityDefinitionSO[] values = new BodyRarityDefinitionSO[12]; // Database에 연결할 레어도 배열
        for (int index = 0; index < values.Length; index++)
        {
            int tier = index + 1; // 1 기준 실제 레어도 Tier
            string path = $"{RarityFolder}/BodyRarity_Tier{tier:00}.asset"; // Tier별 안정적인 Asset 경로
            BodyRarityDefinitionSO asset = AssetDatabase.LoadAssetAtPath<BodyRarityDefinitionSO>(path); // 사용자가 이미 조절한 기존 Asset
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BodyRarityDefinitionSO>();
                SerializedObject serialized = new(asset); // private Inspector 필드를 초기화할 편집 객체
                Set(serialized, "tier", tier);
                Set(serialized, "displayName", names[index]);
                Set(serialized, "uiColor", Color.HSVToRGB(Mathf.Repeat(0.32f + index * 0.075f, 1f), index == 0 ? 0f : 0.65f, 1f));
                Set(serialized, "mainRollMin", minimums[index]);
                Set(serialized, "mainRollMax", maximums[index]);
                Set(serialized, "subStatMinCount", subMinimums[index]);
                Set(serialized, "subStatMaxCount", subMaximums[index]);
                Set(serialized, "subStatMultiplier", 1f + index * 0.18f);
                Set(serialized, "dismantleResearchPoint", points[index]);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(asset, path);
            }
            values[index] = asset;
        }
        return values;
    }

    // 기본 8개 슬롯 장비 원형과 슬롯별 Stat Pool을 준비합니다.
    private static BodyEquipmentDefinitionSO[] CreateEquipment(Dictionary<EquipmentStatType, EquipmentStatDefinitionSO> stats)
    {
        BodyEquipmentDefinitionSO[] values = new BodyEquipmentDefinitionSO[8]; // Database에 연결할 장비 원형 배열
        values[0] = CreateEquipmentAsset("head", "부패한 머리", "머리", BodyEquipmentSlot.Head, stats,
            new[] { EquipmentStatType.Defense, EquipmentStatType.Health }, new[] { EquipmentStatType.DefensePercent, EquipmentStatType.HealthPercent, EquipmentStatType.DamageReduction, EquipmentStatType.HealthRegen });
        values[1] = CreateEquipmentAsset("torso", "봉합된 몸통", "몸통", BodyEquipmentSlot.Torso, stats,
            new[] { EquipmentStatType.Health, EquipmentStatType.Defense }, new[] { EquipmentStatType.HealthPercent, EquipmentStatType.DefensePercent, EquipmentStatType.DamageReduction, EquipmentStatType.HealthRegen });
        values[2] = CreateEquipmentAsset("arms", "괴력의 팔", "팔", BodyEquipmentSlot.Arms, stats,
            new[] { EquipmentStatType.Attack }, new[] { EquipmentStatType.DamagePercent, EquipmentStatType.AttackSpeed, EquipmentStatType.CriticalDamage, EquipmentStatType.LifeSteal });
        values[3] = CreateEquipmentAsset("legs", "질주의 다리", "다리", BodyEquipmentSlot.Legs, stats,
            new[] { EquipmentStatType.MoveSpeed, EquipmentStatType.Health }, new[] { EquipmentStatType.MoveSpeed, EquipmentStatType.AttackSpeed, EquipmentStatType.HealthPercent, EquipmentStatType.DamageReduction });
        values[4] = CreateEquipmentAsset("eyes", "포식자의 눈", "눈", BodyEquipmentSlot.Eyes, stats,
            new[] { EquipmentStatType.CriticalChance, EquipmentStatType.CriticalDamage }, new[] { EquipmentStatType.CriticalChance, EquipmentStatType.CriticalDamage, EquipmentStatType.AttackSpeed, EquipmentStatType.DamagePercent });
        values[5] = CreateEquipmentAsset("jaw", "갈라진 턱", "턱", BodyEquipmentSlot.Jaw, stats,
            new[] { EquipmentStatType.Attack, EquipmentStatType.Defense }, new[] { EquipmentStatType.LifeSteal, EquipmentStatType.DamagePercent, EquipmentStatType.CriticalDamage, EquipmentStatType.DefensePercent });
        values[6] = CreateEquipmentAsset("heart", "썩은 심장", "심장", BodyEquipmentSlot.Heart, stats,
            new[] { EquipmentStatType.Health, EquipmentStatType.HealthRegen }, new[] { EquipmentStatType.HealthPercent, EquipmentStatType.HealthRegen, EquipmentStatType.LifeSteal, EquipmentStatType.DamageReduction });
        values[7] = CreateEquipmentAsset("spine", "강철 척추", "척추", BodyEquipmentSlot.Spine, stats,
            new[] { EquipmentStatType.Defense, EquipmentStatType.Attack }, new[] { EquipmentStatType.DefensePercent, EquipmentStatType.HealthPercent, EquipmentStatType.AttackSpeed, EquipmentStatType.MoveSpeed });
        return values;
    }

    // 한 슬롯 장비 Asset을 기존 값 보존 정책으로 준비합니다.
    private static BodyEquipmentDefinitionSO CreateEquipmentAsset(string id, string displayName, string slotName, BodyEquipmentSlot slot,
        Dictionary<EquipmentStatType, EquipmentStatDefinitionSO> stats, EquipmentStatType[] mainTypes, EquipmentStatType[] subTypes)
    {
        string path = $"{EquipmentFolder}/BodyEquipment_{slot}.asset"; // 슬롯별 안정적인 Asset 경로
        BodyEquipmentDefinitionSO asset = AssetDatabase.LoadAssetAtPath<BodyEquipmentDefinitionSO>(path); // 사용자가 이미 꾸민 기존 Asset
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<BodyEquipmentDefinitionSO>();
        SerializedObject serialized = new(asset); // private Inspector 필드를 초기화할 편집 객체
        Set(serialized, "definitionId", id);
        Set(serialized, "displayName", displayName);
        Set(serialized, "slotDisplayName", slotName);
        Set(serialized, "slot", (int)slot);
        Set(serialized, "drawWeight", 1f);
        Set(serialized, "description", $"{slotName} 슬롯에 장착하는 좀비 신체 장비입니다.");
        SetObjectArray(serialized.FindProperty("possibleMainStats"), mainTypes, stats);
        SetObjectArray(serialized.FindProperty("possibleSubStats"), subTypes, stats);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    // 모든 원형을 ID Dictionary로 캐시할 기본 Database를 생성합니다.
    private static void CreateDatabase(Dictionary<EquipmentStatType, EquipmentStatDefinitionSO> stats,
        BodyRarityDefinitionSO[] rarities, BodyEquipmentDefinitionSO[] equipment)
    {
        if (AssetDatabase.LoadAssetAtPath<BodyEquipmentDatabaseSO>(DatabasePath) != null) return;
        BodyEquipmentDatabaseSO database = ScriptableObject.CreateInstance<BodyEquipmentDatabaseSO>(); // 새 기본 Database
        SerializedObject serialized = new(database); // private 배열을 연결할 편집 객체
        SetObjectArray(serialized.FindProperty("equipmentDefinitions"), equipment);
        SetObjectArray(serialized.FindProperty("rarityDefinitions"), rarities);
        EquipmentStatDefinitionSO[] statArray = new EquipmentStatDefinitionSO[Enum.GetValues(typeof(EquipmentStatType)).Length]; // enum 순서의 전체 스탯
        foreach (KeyValuePair<EquipmentStatType, EquipmentStatDefinitionSO> pair in stats) statArray[(int)pair.Key] = pair.Value;
        SetObjectArray(serialized.FindProperty("statDefinitions"), statArray);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(database, DatabasePath);
    }

    // 12단계 연구 시간과 Weight를 가진 기본 Settings를 생성합니다.
    private static void CreateResearchSettings()
    {
        if (AssetDatabase.LoadAssetAtPath<BodyDrawResearchSettingsSO>(ResearchPath) != null) return;
        double[] durations = { 300d, 600d, 1800d, 3600d, 10800d, 18000d, 28800d, 54000d, 86400d, 108000d, 180000d, 288000d }; // 기획 기본 연구 시간
        int[] points = { 10, 25, 60, 150, 350, 800, 1800, 4000, 9000, 20000, 45000, 100000 }; // 임시 연구 포인트 비용
        double[] baseWeights = { 650000d, 250000d, 80000d, 18000d, 1800d, 180d, 18d, 2d, 0.2d, 0.02d, 0.002d, 0.0002d }; // Lv.1 기준 Tier Weight
        BodyDrawResearchSettingsSO settings = ScriptableObject.CreateInstance<BodyDrawResearchSettingsSO>(); // 새 기본 연구 Settings
        SerializedObject serialized = new(settings); // private 연구 배열을 초기화할 편집 객체
        SerializedProperty levels = serialized.FindProperty("levels"); // 생성할 12개 연구 레벨 배열
        levels.arraySize = 12;
        for (int levelIndex = 0; levelIndex < 12; levelIndex++)
        {
            SerializedProperty level = levels.GetArrayElementAtIndex(levelIndex); // 현재 연구 레벨 데이터
            level.FindPropertyRelative("level").intValue = levelIndex + 1;
            level.FindPropertyRelative("requiredResearchPoints").intValue = points[levelIndex];
            level.FindPropertyRelative("durationSeconds").doubleValue = durations[levelIndex];
            SerializedProperty weights = level.FindPropertyRelative("rarityWeights"); // 현재 레벨의 12개 Tier Weight
            weights.arraySize = 12;
            for (int tierIndex = 0; tierIndex < 12; tierIndex++)
            {
                SerializedProperty weight = weights.GetArrayElementAtIndex(tierIndex); // 현재 Tier Weight 항목
                weight.FindPropertyRelative("rarityTier").intValue = tierIndex + 1;
                double commonAdjustment = tierIndex == 0 ? Math.Pow(0.92d, levelIndex) : 1d; // 연구마다 일반 확률을 낮추는 배율
                double rareBoost = Math.Pow(1d + tierIndex * 0.12d, levelIndex); // 높은 Tier일수록 더 크게 증가하는 배율
                weight.FindPropertyRelative("weight").floatValue = (float)Math.Max(0.000001d, baseWeights[tierIndex] * commonAdjustment * rareBoost);
            }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(settings, ResearchPath);
    }

    // 기존 Stat Gacha 퀘스트 조건만 신체 Draw 조건으로 안전하게 이전합니다.
    private static void MigrateDefaultQuestAsset()
    {
        QuestSettings settings = AssetDatabase.LoadAssetAtPath<QuestSettings>("Assets/Resources/QuestSettings_Default.asset"); // 현재 기본 Quest Asset
        if (settings == null) return;
        bool changed = false; // 실제 이전할 조건이 있었는지 여부
        if (settings.quests != null)
        {
            foreach (QuestDefinition quest in settings.quests) // 이전할 수동 Quest
            {
                if (quest == null || quest.type != QuestType.StatGachaCount) continue;
                quest.type = QuestType.BodyDrawCount;
                quest.title = ReplaceStatGachaText(quest.title);
                quest.description = ReplaceStatGachaText(quest.description);
                changed = true;
            }
        }
        if (settings.infiniteRules != null)
        {
            foreach (InfiniteQuestRule rule in settings.infiniteRules) // 이전할 반복 Quest 규칙
            {
                if (rule == null || rule.type != QuestType.StatGachaCount) continue;
                rule.type = QuestType.BodyDrawCount;
                rule.title = ReplaceStatGachaText(rule.title);
                rule.descriptionFormat = ReplaceStatGachaText(rule.descriptionFormat);
                changed = true;
            }
        }
        if (changed) EditorUtility.SetDirty(settings);
    }

    // 기존 문구 안의 스탯 가챠 이름만 새 기능명으로 바꿉니다.
    private static string ReplaceStatGachaText(string value)
    {
        return string.IsNullOrEmpty(value) ? value : value.Replace("스탯 가챠", "신체 장비 뽑기");
    }

    // 중첩된 Asset 폴더를 누락된 경우에만 순서대로 생성합니다.
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'); // 생성할 폴더의 Unity 부모 경로
        string name = System.IO.Path.GetFileName(path); // 생성할 마지막 폴더 이름
        if (!string.IsNullOrWhiteSpace(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    // SerializedObject 문자열 필드를 설정합니다.
    private static void Set(SerializedObject serialized, string propertyName, string value) { serialized.FindProperty(propertyName).stringValue = value; }

    // SerializedObject 정수 또는 enum 필드를 설정합니다.
    private static void Set(SerializedObject serialized, string propertyName, int value) { serialized.FindProperty(propertyName).intValue = value; }

    // SerializedObject 실수 필드를 설정합니다.
    private static void Set(SerializedObject serialized, string propertyName, float value) { serialized.FindProperty(propertyName).floatValue = value; }

    // SerializedObject 색상 필드를 설정합니다.
    private static void Set(SerializedObject serialized, string propertyName, Color value) { serialized.FindProperty(propertyName).colorValue = value; }

    // Unity Object 배열을 SerializedProperty에 순서대로 연결합니다.
    private static void SetObjectArray<T>(SerializedProperty property, IReadOnlyList<T> values) where T : UnityEngine.Object
    {
        property.arraySize = values == null ? 0 : values.Count;
        for (int index = 0; index < property.arraySize; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
    }

    // Stat Type 배열을 실제 Stat Asset 참조 배열로 연결합니다.
    private static void SetObjectArray(SerializedProperty property, IReadOnlyList<EquipmentStatType> types,
        Dictionary<EquipmentStatType, EquipmentStatDefinitionSO> stats)
    {
        property.arraySize = types == null ? 0 : types.Count;
        for (int index = 0; index < property.arraySize; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = stats[types[index]];
    }
}
#endif
