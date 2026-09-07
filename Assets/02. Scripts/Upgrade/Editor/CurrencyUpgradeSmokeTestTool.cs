#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 실제 저장과 에셋을 건드리지 않고 재화 강화, 아이콘, 메뉴, 통합 저장을 검증합니다.
public static class CurrencyUpgradeSmokeTestTool
{
    private const string BalancePath = "Assets/02. Scripts/Upgrade/CurrencyUpgradeBalanceSettings_Default.asset"; // 실제 재화 밸런스 경로
    private const string StatBalancePath = "Assets/02. Scripts/Upgrade/UpgradeBalanceSettings_Default.asset"; // 보존 여부를 검사할 기존 스탯 밸런스
    private const string MenuPath = "Assets/03. Prefabs/UI/Upgrade/UpgradeMenuController.prefab"; // 실제 게임에서 사용하는 강화 메뉴
    private const string RowPath = "Assets/03. Prefabs/UI/Upgrade/Currency/CurrencyUpgradeRowView.prefab"; // 네 강화가 공유하는 Row 프리팹

    // 격리된 미리보기 씬과 임시 저장 폴더에서 기존 스모크 테스트를 실행합니다.
    [MenuItem("Tools/Raising Zombies/Upgrade/Run Currency Upgrade Smoke Test")]
    public static void Run()
    {
        Assert(!EditorApplication.isPlayingOrWillChangePlaymode, "플레이를 종료한 뒤 실행하세요.");
        string testDirectory = Path.Combine(Path.GetTempPath(), "RaisingZombiesCurrencySmoke_" + Guid.NewGuid().ToString("N")); // 이번 검증 전용 저장 경로
        string originalBalance = File.ReadAllText(BalancePath); // 원본 에셋 보호 확인값
        string originalRow = File.ReadAllText(RowPath); // Row 디자인과 직렬화 보호 확인값
        FieldInfo saveSingleton = GetSingletonField<SaveManager>(); // 테스트 동안 교체할 저장 싱글턴 필드
        FieldInfo upgradeSingleton = GetSingletonField<UpgradeManager>(); // 테스트 동안 교체할 강화 싱글턴 필드
        object previousSave = saveSingleton.GetValue(null); // 테스트 전 저장 매니저 참조
        object previousUpgrade = upgradeSingleton.GetValue(null); // 테스트 전 강화 매니저 참조
        Scene previewScene = EditorSceneManager.NewPreviewScene(); // 실제 씬을 오염시키지 않을 검증 씬
        CurrencyUpgradeBalanceSettings balance = null; // 원본 대신 수정할 메모리 밸런스 복제본
        GameObject menuRoot = null; // 실제 메뉴 프리팹의 미리보기 루트
        SaveFileService service = new(testDirectory); // 사용자 저장과 격리된 파일 서비스
        try
        {
            CurrencyUpgradeBalanceSettings original = AssetDatabase.LoadAssetAtPath<CurrencyUpgradeBalanceSettings>(BalancePath); // 보호할 기존 밸런스
            Assert(original != null, "재화 밸런스 에셋이 없습니다.");
            balance = UnityEngine.Object.Instantiate(original);
            List<string> errors = new(); // 실제 밸런스 검증 오류
            balance.CollectValidationErrors(errors);
            Assert(errors.Count == 0, string.Join(" | ", errors));
            foreach (CurrencyUpgradeDefinition definition in balance.Definitions) // 기존 에셋에 새 정책의 기본값이 적용됐는지 검사
                Assert(definition.unlimited, $"{definition.type}의 기존 에셋에 무제한 기본값이 적용되지 않았습니다.");

            SaveManager saveManager = CreateInactiveComponent<SaveManager>(previewScene); // 부트스트랩을 실행하지 않을 저장 매니저
            SetField(saveManager, "_fileService", service);
            SetField(saveManager, "_saveData", GameSaveData.CreateNew());
            saveSingleton.SetValue(null, saveManager);
            UpgradeManager manager = CreateInactiveComponent<UpgradeManager>(previewScene); // 격리된 강화 매니저
            SetField(manager, "currencyUpgradeBalance", balance);
            SetField(manager, "balanceSettings", AssetDatabase.LoadAssetAtPath<UpgradeBalanceSettings>(StatBalancePath));
            SetField(manager, "_state", new UpgradeState());
            upgradeSingleton.SetValue(null, manager);
            Assert(saveManager.RegisterProvider(manager, true), "격리된 Provider 등록 실패");

            TestUnlimitedPurchases(manager, balance, saveManager);
            TestOfflineAndNumericSafety(manager, balance);
            TestSaveRoundTrip(manager, saveManager, service);
            menuRoot = PrefabUtility.LoadPrefabContents(MenuPath);
            TestMenuAndIcons(menuRoot, manager, balance);
            Assert(File.ReadAllText(BalancePath) == originalBalance, "기존 밸런스 에셋 변경");
            Assert(File.ReadAllText(RowPath) == originalRow, "공용 Row 프리팹 변경");
            Debug.Log("[CurrencyUpgradeSmokeTest] PASS: 기존 에셋 무제한 기본값, 초반 비용, Lv.10/20/100/500/1000 구매·효과, 재화 부족, 유한 모드, 정수 경계, 생산·처치·오프라인 보상, 기존 JSON/통합 SaveManager 저장·복원, 메뉴 재진입, 공용 Row 아이콘·null·버튼·이벤트, 원본 에셋 보존. 오프라인 효율 100% 및 int 비용 상한은 기존 정책대로 유지됩니다.");
        }
        finally
        {
            if (menuRoot != null)
            {
                foreach (CurrencyUpgradePanel panel in menuRoot.GetComponentsInChildren<CurrencyUpgradePanel>(true)) // 편집 모드에서 명시적으로 해제할 패널
                    InvokePrivate(panel, "OnDisable");
                PrefabUtility.UnloadPrefabContents(menuRoot);
            }
            EditorSceneManager.ClosePreviewScene(previewScene);
            saveSingleton.SetValue(null, previousSave);
            upgradeSingleton.SetValue(null, previousUpgrade);
            if (balance != null) UnityEngine.Object.DestroyImmediate(balance);
            service.DeleteAll();
            if (Directory.Exists(testDirectory)) Directory.Delete(testDirectory, false);
        }
    }

    // 기존 최대 레벨 이후 구매와 비용·효과 증가, 유한 정책 호환을 검사합니다.
    private static void TestUnlimitedPurchases(UpgradeManager manager, CurrencyUpgradeBalanceSettings balance, SaveManager saveManager)
    {
        int[] levels = { 10, 20, 100, 500, 1000 }; // 무제한 구매를 확인할 장기 성장 레벨
        foreach (CurrencyUpgradeDefinition definition in balance.Definitions) // 실제 네 강화 정의
        {
            for (int level = 0; level < definition.maxLevel; level++) // 기존 초반 비용 보존 검사
            {
                SetLevelState(manager, definition.type, level, int.MaxValue);
                int expected = (int)Math.Min(int.MaxValue, Math.Ceiling(definition.baseCost * Math.Pow(definition.costGrowth, level))); // 기존 비용
                Assert(manager.GetCurrencyUpgradeSnapshot(definition.type).NextCost == expected, $"{definition.type} Lv.{level} 초반 비용 변경");
            }
            foreach (int level in levels) // 높은 레벨 구매와 실제 효과 검사
            {
                SetLevelState(manager, definition.type, level, int.MaxValue);
                CurrencyUpgradeSnapshot before = manager.GetCurrencyUpgradeSnapshot(definition.type); // 구매 전 표시값
                Assert(before.IsUnlimited && !before.IsMaxLevel && before.CurrentLevel == level, "무제한 레벨이 잘리거나 MAX로 표시됩니다.");
                Assert(before.NextCost > 0 && manager.CanUpgradeCurrency(definition.type), "높은 레벨 비용 또는 강화 가능 검사 실패");
                Assert(manager.TryUpgradeCurrency(definition.type), $"{definition.type} Lv.{level} 구매 실패");
                CurrencyUpgradeSnapshot after = manager.GetCurrencyUpgradeSnapshot(definition.type); // 구매 후 표시값
                Assert(after.CurrentLevel == level + 1 && manager.Currency == int.MaxValue - before.NextCost, "레벨 증가 또는 비용 차감 오류");
                AssertFinite(after.CurrentEffect);
                AssertFinite(after.NextEffect);
                if (definition.type != CurrencyUpgradeType.OfflineEfficiency)
                    Assert(after.CurrentEffect > before.CurrentEffect, "높은 레벨의 실제 효과가 증가하지 않습니다.");
                else
                    Assert(after.CurrentEffect == balance.maximumOfflineEfficiency, "기존 오프라인 효율 상한 변경");
                Assert(saveManager.LoadGame(), "높은 레벨 저장 복원 실패");
                Assert(manager.GetCurrencyUpgradeSnapshot(definition.type).CurrentLevel == level + 1, "저장 정규화가 높은 레벨을 잘랐습니다.");
            }
            SetLevelState(manager, definition.type, 100, 0);
            Assert(!manager.CanUpgradeCurrency(definition.type) && !manager.TryUpgradeCurrency(definition.type), "재화 부족 구매 허용");
            Assert(manager.Currency == 0 && manager.GetCurrencyUpgradeSnapshot(definition.type).CurrentLevel == 100, "실패 구매의 상태 변경");
            definition.unlimited = false;
            SetLevelState(manager, definition.type, definition.maxLevel, int.MaxValue);
            Assert(manager.GetCurrencyUpgradeSnapshot(definition.type).IsMaxLevel && !manager.TryUpgradeCurrency(definition.type), "유한 모드 호환 실패");
            definition.unlimited = true;
            SetLevelState(manager, definition.type, int.MaxValue - 1, int.MaxValue);
            Assert(manager.TryUpgradeCurrency(definition.type), "정수 경계 직전 구매 실패");
            Assert(manager.GetCurrencyUpgradeSnapshot(definition.type).CurrentLevel == int.MaxValue && !manager.TryUpgradeCurrency(definition.type),
                "레벨 정수 오버플로 보호 실패");
        }
    }

    // 생산량과 보상 수치가 음수나 무한대가 되지 않는지 확인합니다.
    private static void TestOfflineAndNumericSafety(UpgradeManager manager, CurrencyUpgradeBalanceSettings balance)
    {
        SetField(manager, "_state", new UpgradeState());
        Assert(Mathf.Approximately(manager.GetCurrencyPerSecond(), balance.baseCurrencyPerSecond), "기본 생산량 변경");
        OfflineCurrencyReward capped = manager.CalculateOfflineReward(86400d); // 기존 최대 시간을 넘긴 오프라인 보상
        Assert(capped.AppliedSeconds == balance.baseOfflineMaxHours * 3600d, "기존 적립 시간 변경");
        Assert(capped.EarnedCurrency == (int)Math.Floor(manager.GetCurrencyPerSecond() * capped.AppliedSeconds * capped.Efficiency), "오프라인 공식 변경");
        SetField(manager, "_state", new UpgradeState { offlineEfficiencyLevel = 2 });
        Assert(Mathf.Approximately(manager.CalculateOfflineReward(60d).Efficiency, 0.7f), "초반 효율 변경");
        SetField(manager, "_state", new UpgradeState { currencyPerSecondLevel = 1000, offlineMaxTimeLevel = 1000, offlineEfficiencyLevel = 1000 });
        OfflineCurrencyReward highReward = manager.CalculateOfflineReward(100000d); // 높은 레벨의 실제 보상
        Assert(highReward.EarnedCurrency > capped.EarnedCurrency && highReward.Efficiency == 1f, "높은 레벨의 오프라인 보상 오류");
        foreach (double seconds in new[] { -1d, double.NaN, double.PositiveInfinity }) // 비정상 경과 시간 입력
            Assert(manager.CalculateOfflineReward(seconds).EarnedCurrency == 0, "비정상 경과 시간 보상 발생");
        SetLevelState(manager, CurrencyUpgradeType.CurrencyPerSecond, int.MaxValue, 0);
        InvokePrivate(manager, "ProduceCurrency", 1);
        Assert(manager.Currency == int.MaxValue, "큰 초당 생산량 지급 또는 정수 보호 실패");
        SetField(manager, "_state", new UpgradeState());
        InvokePrivate(manager, "ProduceCurrency", 1);
        Assert(manager.Currency == (int)balance.baseCurrencyPerSecond, "지갑 초과 생산량이 잔여량으로 중복 지급됩니다.");

        CurrencyUpgradeDefinition passive = balance.GetDefinition(CurrencyUpgradeType.CurrencyPerSecond); // 비정상 계수 검사 대상
        float originalValue = passive.valuePerLevel; // 복구할 원래 계수
        int originalCost = passive.baseCost; // 복구할 기본 비용
        float originalGrowth = passive.costGrowth; // 복구할 증가율
        try
        {
            passive.valuePerLevel = float.MaxValue;
            SetLevelState(manager, passive.type, int.MaxValue, 0);
            AssertFinite(manager.GetCurrencyPerSecond());
            Assert(manager.CalculateOfflineReward(double.MaxValue).EarnedCurrency == int.MaxValue, "극단적인 보상 보호 실패");
            passive.valuePerLevel = float.NaN;
            AssertFinite(manager.GetCurrencyPerSecond());
            passive.baseCost = 0;
            Assert(manager.GetCurrencyUpgradeSnapshot(passive.type).NextCost == 0, "0 비용과 큰 지수의 곱 오류");
            passive.baseCost = originalCost;
            passive.costGrowth = float.NaN;
            Assert(manager.GetCurrencyUpgradeSnapshot(passive.type).NextCost == int.MaxValue, "NaN 비용 보호 실패");
        }
        finally
        {
            passive.valuePerLevel = originalValue;
            passive.baseCost = originalCost;
            passive.costGrowth = originalGrowth;
        }
        UnitData human = ScriptableObject.CreateInstance<UnitData>(); // 인간 판정 검사 데이터
        UnitData zombie = ScriptableObject.CreateInstance<UnitData>(); // 비인간 보상 배제 검사 데이터
        try
        {
            SerializedObject humanData = new(human); // 인간 팀 편집 객체
            humanData.FindProperty("team").enumValueIndex = (int)UnitTeam.Human;
            humanData.ApplyModifiedPropertiesWithoutUndo();
            SerializedObject zombieData = new(zombie); // 좀비 팀 편집 객체
            zombieData.FindProperty("team").enumValueIndex = (int)UnitTeam.Zombie;
            zombieData.ApplyModifiedPropertiesWithoutUndo();
            SetLevelState(manager, CurrencyUpgradeType.HumanKillBonus, 1000, 0);
            Assert(manager.GrantHumanKillBonus(human) == 1000 && manager.Currency == 1000, "높은 레벨 인간 보상 지급 실패");
            Assert(manager.GrantHumanKillBonus(zombie) == 0 && manager.Currency == 1000, "비인간 추가 보상 발생");
            SetLevelState(manager, CurrencyUpgradeType.HumanKillBonus, int.MaxValue, 0);
            Assert(manager.GrantHumanKillBonus(human) == int.MaxValue, "처치 보너스 정수 오버플로 발생");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(human);
            UnityEngine.Object.DestroyImmediate(zombie);
        }
    }

    // 기존 JSON과 통합 파일의 레벨 보존 및 오프라인 보상 중복 방지를 검사합니다.
    private static void TestSaveRoundTrip(UpgradeManager manager, SaveManager saveManager, SaveFileService service)
    {
        UpgradeState state = JsonUtility.FromJson<UpgradeState>("{\"version\":2,\"currency\":77,\"gachaLevel\":1,\"offlineEfficiencyLevel\":5,\"currencyPerSecondLevel\":1000,\"humanKillBonusLevel\":500,\"offlineMaxTimeLevel\":100,\"stats\":[]}"); // 기존 필드로 구성한 저장 예시
        manager.RestoreSaveData(state);
        Assert(manager.GetCurrencyUpgradeSnapshot(CurrencyUpgradeType.OfflineEfficiency).CurrentLevel == 5, "기존 저장 Lv.5 변경");
        Assert(saveManager.SaveGame(), "통합 SaveManager 저장 실패");
        SetField(manager, "_state", new UpgradeState());
        Assert(saveManager.LoadGame(), "통합 SaveManager 복원 실패");
        int loadReward = manager.TryConsumeOfflineReward(out OfflineCurrencyReward reward) ? reward.EarnedCurrency : 0; // 파일을 읽는 실제 경과 시간에 대한 정상 오프라인 보상
        Assert(manager.Currency == 77 + loadReward && manager.GetCurrencyUpgradeSnapshot(CurrencyUpgradeType.CurrencyPerSecond).CurrentLevel == 1000, $"저장 재화 또는 높은 레벨 유실: 재화 {manager.Currency}, 복귀 보상 {loadReward}");
        Assert(manager.GetCurrencyUpgradeSnapshot(CurrencyUpgradeType.HumanKillBonus).CurrentLevel == 500 &&
            manager.GetCurrencyUpgradeSnapshot(CurrencyUpgradeType.OfflineMaxTime).CurrentLevel == 100, "다른 강화 레벨 유실");
        Assert(service.TryLoad(out GameSaveData diskSave, out _), "프로세스 메모리 없이 파일 재로드 실패");
        Assert(diskSave.TryGetSection("upgrade", out SaveDataSection section), "기존 upgrade 저장 구역 유실");
        UpgradeState restored = JsonUtility.FromJson<UpgradeState>(section.json); // 디스크에서 다시 읽은 기존 저장 DTO
        Assert(restored.version == 2 && restored.offlineEfficiencyLevel == 5, "저장 버전이나 필드 형식 변경");
        state = new UpgradeState { lastActivityUtc = DateTime.UtcNow.AddHours(-3d).ToString("O", CultureInfo.InvariantCulture) };
        manager.RestoreSaveData(state);
        int firstReward = manager.Currency; // 첫 복귀 후 재화
        Assert(firstReward > 0 && manager.TryConsumeOfflineReward(out _), "오프라인 보상 최초 지급 실패");
        Assert(!manager.TryConsumeOfflineReward(out _), "오프라인 UI 보상 중복 소비");
        Assert(saveManager.SaveGame() && saveManager.LoadGame() && manager.Currency == firstReward, "동일 오프라인 보상 중복 지급");
        manager.RestoreSaveData(JsonUtility.FromJson<UpgradeState>("{\"version\":1,\"currency\":77,\"gachaLevel\":1,\"stats\":[]}"));
        Assert(manager.Currency == 77 && manager.GetCurrencyUpgradeSnapshot(CurrencyUpgradeType.CurrencyPerSecond).CurrentLevel == 0, "구버전 저장 기본값 변경");
    }

    // 실제 메뉴 재진입, 공용 Row 재사용, 정의별 Sprite와 구매 버튼을 검증합니다.
    private static void TestMenuAndIcons(GameObject menuRoot, UpgradeManager manager, CurrencyUpgradeBalanceSettings balance)
    {
        UpgradeMenuController menu = menuRoot.GetComponent<UpgradeMenuController>(); // 실제 화면 전환 Controller
        CurrencyUpgradePanel panel = menuRoot.GetComponentInChildren<CurrencyUpgradePanel>(true); // 메뉴 내부 재화 패널
        CurrencyUpgradeRowView prefab = AssetDatabase.LoadAssetAtPath<CurrencyUpgradeRowView>(RowPath); // 공용 Row 프리팹
        Assert(menu != null && panel != null && prefab != null, "실제 강화 메뉴나 Row 프리팹 누락");
        Assert(GetReference<CurrencyUpgradeRowView>(panel, "rowPrefab") == prefab, "공용 Row 참조 불일치");
        Assert(GetReference<Image>(prefab, "iconImage") != null, "공용 Row 아이콘 참조 누락");
        SetField(manager, "_state", new UpgradeState { currency = int.MaxValue, currencyPerSecondLevel = 100 });
        menu.ShowCurrencyUpgrade();
        InvokePrivate(panel, "OnEnable");
        Assert(menu.CurrentState == UpgradeMenuState.CurrencyUpgrade && panel.gameObject.activeInHierarchy, "재화 강화 진입 실패");
        Dictionary<CurrencyUpgradeType, CurrencyUpgradeRowView> rows = GetField<Dictionary<CurrencyUpgradeType, CurrencyUpgradeRowView>>(panel, "_rows"); // 종류별 재사용 Row
        Assert(rows.Count == 4, "종류별 Row 개수 오류");
        Texture2D texture = new(2, 2); // 파일로 저장하지 않는 Sprite 원본
        Sprite first = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f); // 첫 강화 아이콘
        Sprite second = Sprite.Create(texture, new Rect(1, 1, 1, 1), Vector2.one * 0.5f); // 다른 강화 아이콘
        try
        {
            balance.GetDefinition(CurrencyUpgradeType.CurrencyPerSecond).icon = first;
            balance.GetDefinition(CurrencyUpgradeType.HumanKillBonus).icon = second;
            panel.Refresh();
            CurrencyUpgradeRowView passiveRow = rows[CurrencyUpgradeType.CurrencyPerSecond]; // 실제 초당 재화 Row
            CurrencyUpgradeRowView killRow = rows[CurrencyUpgradeType.HumanKillBonus]; // 실제 인간 보너스 Row
            Image passiveIcon = GetReference<Image>(passiveRow, "iconImage"); // 첫 Row 이미지
            Image killIcon = GetReference<Image>(killRow, "iconImage"); // 다른 Row 이미지
            Assert(passiveIcon.sprite == first && killIcon.sprite == second && passiveIcon.enabled, "종류별 서로 다른 Sprite 연결 실패");
            balance.GetDefinition(CurrencyUpgradeType.CurrencyPerSecond).icon = second;
            panel.Refresh();
            Assert(passiveIcon.sprite == second && killIcon.sprite == second, "단일 정의의 아이콘 갱신 실패");
            passiveRow.Bind(manager, CurrencyUpgradeType.OfflineMaxTime);
            Assert(passiveIcon.sprite == null && !passiveIcon.enabled, "Row 재사용 시 이전 Sprite 잔류");
            passiveRow.Bind(null, CurrencyUpgradeType.CurrencyPerSecond);
            Assert(passiveIcon.sprite == null && !passiveIcon.enabled, "null 매니저 아이콘 정리 실패");
            panel.Refresh();
            Assert(GetReference<TMP_Text>(passiveRow, "levelText").text == "Lv.100", "높은 레벨 Text 오류");
            Assert(GetReference<TMP_Text>(passiveRow, "costText").text == int.MaxValue.ToString(), "높은 비용 Text 오류");
            Button button = GetReference<Button>(passiveRow, "upgradeButton"); // 실제 구매 버튼
            InvokePrivate(passiveRow, "Awake");
            InvokePrivate(passiveRow, "Awake");
            Assert(button.interactable, "무제한 레벨에서 버튼이 막혔습니다.");
            button.onClick.Invoke();
            Assert(manager.GetCurrencyUpgradeSnapshot(CurrencyUpgradeType.CurrencyPerSecond).CurrentLevel == 101 && manager.Currency == 0, "버튼 구매 처리 오류");
            Assert(GetReference<TMP_Text>(passiveRow, "levelText").text == "Lv.101" && !button.interactable, "이벤트 UI 갱신 또는 재화 부족 버튼 오류");
            menu.ShowCategorySelection();
            InvokePrivate(panel, "OnDisable");
            menu.ShowCurrencyUpgrade();
            InvokePrivate(panel, "OnEnable");
            Assert(rows.Count == 4 && ReferenceEquals(rows[CurrencyUpgradeType.CurrencyPerSecond], passiveRow), "재진입 Row 중복 생성");
            Assert(passiveIcon.sprite == second, "재진입 아이콘 유실");
            menu.ShowStatUpgrade();
            Assert(menu.CurrentState == UpgradeMenuState.StatUpgrade, "스탯 화면 전환 영향");
            menu.ShowProductionUpgrade();
            Assert(menu.CurrentState == UpgradeMenuState.ProductionUpgrade, "생산 화면 전환 영향");
            menu.ShowCategorySelection();
        }
        finally
        {
            foreach (CurrencyUpgradeDefinition definition in balance.Definitions) definition.icon = null; // 임시 Sprite 참조 해제
            panel.Refresh();
            InvokePrivate(panel, "OnDisable");
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    // 기존 저장 필드에 테스트할 재화 강화 진행도를 설정합니다.
    private static void SetLevelState(UpgradeManager manager, CurrencyUpgradeType type, int level, int currency)
    {
        SetField(manager, "_state", new UpgradeState { currency = currency });
        InvokePrivate(manager, "SetCurrencyUpgradeLevel", type, level);
    }

    // 게임 Awake 실행 없이 미리보기 씬에 컴포넌트를 준비합니다.
    private static T CreateInactiveComponent<T>(Scene scene) where T : Component
    {
        GameObject root = new("CurrencySmoke_" + typeof(T).Name); // 검증 컴포넌트만 보관할 임시 루트
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, scene);
        return root.AddComponent<T>();
    }

    // Inspector에 연결된 실제 UI 참조를 읽습니다.
    private static T GetReference<T>(UnityEngine.Object target, string field) where T : UnityEngine.Object
    {
        SerializedObject serialized = new(target); // 검사할 UI 직렬화 객체
        SerializedProperty property = serialized.FindProperty(field); // 검사할 Inspector 필드
        Assert(property != null && property.objectReferenceValue is T, $"{target.name}.{field} 참조 누락");
        return property.objectReferenceValue as T;
    }

    // 테스트 동안 기존 매니저를 보존할 싱글턴 필드를 읽습니다.
    private static FieldInfo GetSingletonField<T>() where T : Singleton<T>
    {
        return typeof(Singleton<T>).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
    }

    // 기존 테스트 패턴대로 private 상태를 격리된 값으로 바꿉니다.
    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); // 변경할 테스트 필드
        Assert(field != null, $"{name} 필드 누락");
        field.SetValue(target, value);
    }

    // private 컬렉션과 원본 상태를 읽어 중복 생성을 검사합니다.
    private static T GetField<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    // 기존 private 구매·이벤트 흐름을 테스트에서 실행합니다.
    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic); // 실행할 기존 함수
        Assert(method != null, $"{methodName} 함수 누락");
        method.Invoke(target, arguments);
    }

    // 효과 표시값이 음수나 무한대 또는 NaN인지 검사합니다.
    private static void AssertFinite(float value)
    {
        Assert(!float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f, "비정상 효과 수치 발생");
    }

    // 검증 실패를 구체적인 예외로 알립니다.
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"[CurrencyUpgradeSmokeTest] {message}");
    }
}
#endif
