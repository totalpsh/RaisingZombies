#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 재화 강화의 비용, 저장, 보상 계산과 메뉴 전환을 빠르게 검증합니다.
public static class CurrencyUpgradeSmokeTestTool
{
    private const string SaveKey = "RaisingZombies.Upgrade.State"; // 실제 업그레이드 저장 키
    private const string BalancePath = "Assets/02. Scripts/Upgrade/CurrencyUpgradeBalanceSettings_Default.asset"; // 테스트 밸런스 경로
    private const string MenuPath = "Assets/03. Prefabs/UI/Upgrade/Currency/UpgradeCategoryMenu.prefab"; // 테스트 메뉴 경로

    // 실제 저장값을 복구하면서 주요 재화 강화 시나리오를 실행합니다.
    [MenuItem("Tools/Raising Zombies/Upgrade/Run Currency Upgrade Smoke Test")]
    public static void Run()
    {
        bool hadSave = PlayerPrefs.HasKey(SaveKey); // 테스트 전 저장 존재 여부
        string originalSave = hadSave ? PlayerPrefs.GetString(SaveKey) : null; // 테스트 전 저장 JSON
        GameObject managerObject = null; // 테스트 매니저 오브젝트
        GameObject menuObject = null; // 테스트 메뉴 인스턴스

        try
        {
            CurrencyUpgradeSetupTool.CreateDefaultBalance();
            CurrencyUpgradeSetupTool.CreateCurrencyUpgradeUiPrefabs();
            CurrencyUpgradeBalanceSettings balance = AssetDatabase.LoadAssetAtPath<CurrencyUpgradeBalanceSettings>(BalancePath); // 생성된 기본 재화 밸런스
            Assert(balance != null, "기본 재화 강화 밸런스가 없습니다.");

            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new UpgradeState { currency = 100000 }));
            managerObject = new GameObject("CurrencyUpgradeSmokeTestManager");
            UpgradeManager manager = managerObject.AddComponent<UpgradeManager>(); // 테스트할 업그레이드 매니저
            SerializedObject serializedManager = new(manager); // private 밸런스 참조 연결 객체
            serializedManager.FindProperty("currencyUpgradeBalance").objectReferenceValue = balance;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            InvokePrivate(manager, "LoadState");

            Assert(Mathf.Approximately(manager.GetCurrencyPerSecond(), 1f), "기본 초당 재화가 1이 아닙니다.");
            CurrencyUpgradeSnapshot perSecond = manager.GetCurrencyUpgradeSnapshot(CurrencyUpgradeType.CurrencyPerSecond); // 초당 재화 기본 상태
            int beforeUpgrade = manager.Currency; // 정상 강화 전 재화
            Assert(manager.TryUpgradeCurrency(CurrencyUpgradeType.CurrencyPerSecond), "재화가 충분한 강화가 실패했습니다.");
            Assert(manager.Currency == beforeUpgrade - perSecond.NextCost, "강화 비용이 정확히 한 번 차감되지 않았습니다.");
            Assert(manager.GetCurrencyPerSecond() > 1f, "초당 재화 강화 효과가 적용되지 않았습니다.");

            UpgradeState poorState = new() { currency = 0 }; // 재화 부족 테스트 상태
            SetState(manager, poorState);
            Assert(!manager.TryUpgradeCurrency(CurrencyUpgradeType.HumanKillBonus), "재화 부족 상태에서 강화됐습니다.");
            Assert(manager.Currency == 0, "실패한 강화가 재화를 변경했습니다.");

            CurrencyUpgradeDefinition humanDefinition = balance.GetDefinition(CurrencyUpgradeType.HumanKillBonus); // 인간 보너스 정의
            UpgradeState maxState = new() { currency = 100000, humanKillBonusLevel = humanDefinition.maxLevel }; // 최대 레벨 테스트 상태
            SetState(manager, maxState);
            Assert(!manager.TryUpgradeCurrency(CurrencyUpgradeType.HumanKillBonus), "최대 레벨에서 강화됐습니다.");

            UnitData human = ScriptableObject.CreateInstance<UnitData>(); // 인간 판정 테스트 데이터
            UnitData zombie = ScriptableObject.CreateInstance<UnitData>(); // 비인간 판정 테스트 데이터
            SetUnitTeam(human, UnitTeam.Human);
            SetUnitTeam(zombie, UnitTeam.Zombie);
            maxState.humanKillBonusLevel = 1;
            int beforeHuman = manager.Currency; // 인간 보너스 전 재화
            int humanBonus = manager.GrantHumanKillBonus(human); // 실제 인간 보너스
            Assert(humanBonus > 0 && manager.Currency == beforeHuman + humanBonus, "인간 처치 보너스가 한 건 지급되지 않았습니다.");
            int beforeZombie = manager.Currency; // 비인간 호출 전 재화
            Assert(manager.GrantHumanKillBonus(zombie) == 0 && manager.Currency == beforeZombie, "비인간 대상에 보너스가 지급됐습니다.");
            UnityEngine.Object.DestroyImmediate(human);
            UnityEngine.Object.DestroyImmediate(zombie);

            SetState(manager, new UpgradeState { currency = 0 });
            OfflineCurrencyReward capped = manager.CalculateOfflineReward(86400d); // 24시간 경과 보상
            Assert(Math.Abs(capped.AppliedSeconds - balance.baseOfflineMaxHours * 3600d) < 0.01d, "오프라인 최대 시간이 적용되지 않았습니다.");
            Assert(capped.EarnedCurrency == Mathf.FloorToInt(manager.GetCurrencyPerSecond() * (float)capped.AppliedSeconds * capped.Efficiency),
                "오프라인 효율 계산값이 다릅니다.");
            Assert(manager.CalculateOfflineReward(-10d).EarnedCurrency == 0, "음수 경과 시간에 보상이 생겼습니다.");
            UpgradeState efficiencyState = new() { currency = 0, offlineEfficiencyLevel = 2 }; // 오프라인 효율 강화 테스트 상태
            SetState(manager, efficiencyState);
            OfflineCurrencyReward efficient = manager.CalculateOfflineReward(60d); // 효율 강화 후 1분 보상
            Assert(Mathf.Approximately(efficient.Efficiency, 0.7f), "오프라인 효율 강화가 적용되지 않았습니다.");

            string oldJson = "{\"version\":1,\"currency\":77,\"gachaLevel\":1,\"drawsAtCurrentLevel\":0,\"stats\":[]}"; // 구버전 저장 예시
            PlayerPrefs.SetString(SaveKey, oldJson);
            InvokePrivate(manager, "LoadState");
            Assert(manager.Currency == 77, "구버전 저장 재화가 호환 로드되지 않았습니다.");
            Assert(manager.GetCurrencyUpgradeSnapshot(CurrencyUpgradeType.CurrencyPerSecond).CurrentLevel == 0,
                "구버전 저장의 새 강화 레벨 기본값이 0이 아닙니다.");

            UpgradeState offlineState = new()
            {
                currency = 0,
                lastActivityUtc = DateTime.UtcNow.AddHours(-3d).ToString("O", CultureInfo.InvariantCulture)
            }; // 중복 오프라인 지급 테스트 상태
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(offlineState));
            InvokePrivate(manager, "LoadState");
            int firstOfflineCurrency = manager.Currency; // 첫 접속 처리 후 재화
            Assert(firstOfflineCurrency > 0, "유효한 오프라인 보상이 지급되지 않았습니다.");
            OfflineCurrencyReward consumed; // UI가 최초 한 번 가져온 오프라인 결과
            Assert(manager.TryConsumeOfflineReward(out consumed) && consumed.EarnedCurrency > 0,
                "오프라인 보상 결과를 최초 한 번 가져오지 못했습니다.");
            Assert(!manager.TryConsumeOfflineReward(out _), "오프라인 보상 결과가 UI에 두 번 전달됐습니다.");
            InvokePrivate(manager, "LoadState");
            Assert(manager.Currency == firstOfflineCurrency, "같은 오프라인 구간이 중복 지급됐습니다.");

            GameObject menuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath); // 생성된 카테고리 메뉴
            Assert(menuPrefab != null, "카테고리 메뉴 프리팹이 없습니다.");
            menuObject = PrefabUtility.InstantiatePrefab(menuPrefab) as GameObject;
            UpgradeMenuController menu = menuObject.GetComponent<UpgradeMenuController>(); // 전환을 검사할 메뉴
            Assert(menu != null, "카테고리 메뉴 컨트롤러가 없습니다.");
            menu.ShowZombieUpgrade();
            Assert(menu.CurrentState == UpgradeMenuState.StatUpgrade, "좀비 강화 화면으로 전환되지 않았습니다.");
            menu.ShowCategorySelection();
            Assert(menu.CurrentState == UpgradeMenuState.CategorySelection, "좀비 뒤로가기가 카테고리로 돌아오지 않았습니다.");
            menu.ShowCurrencyUpgrade();
            Assert(menu.CurrentState == UpgradeMenuState.CurrencyUpgrade, "재화 강화 화면으로 전환되지 않았습니다.");
            menu.ShowCategorySelection();
            Assert(menu.CurrentState == UpgradeMenuState.CategorySelection, "재화 뒤로가기가 카테고리로 돌아오지 않았습니다.");

            Debug.Log("[CurrencyUpgradeSmokeTest] 통과: 기본 생산, 구매 비용/실패/최대, 인간 판정, 오프라인 상한/효율/음수/중복, 구버전 저장, 화면 전환/뒤로가기");
        }
        finally
        {
            if (menuObject != null) UnityEngine.Object.DestroyImmediate(menuObject);
            if (managerObject != null) UnityEngine.Object.DestroyImmediate(managerObject);
            if (hadSave) PlayerPrefs.SetString(SaveKey, originalSave);
            else PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }
    }

    // 테스트 실패를 즉시 명확한 예외로 보고합니다.
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"[CurrencyUpgradeSmokeTest] {message}");
    }

    // private 매니저 함수를 테스트에서 호출합니다.
    private static void InvokePrivate(UpgradeManager manager, string methodName)
    {
        MethodInfo method = typeof(UpgradeManager).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic); // 호출할 private 함수
        Assert(method != null, $"{methodName} 함수를 찾을 수 없습니다.");
        method.Invoke(manager, null);
    }

    // 테스트용 저장 상태를 매니저에 직접 연결합니다.
    private static void SetState(UpgradeManager manager, UpgradeState state)
    {
        FieldInfo field = typeof(UpgradeManager).GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic); // 런타임 상태 필드
        Assert(field != null, "UpgradeManager 상태 필드를 찾을 수 없습니다.");
        field.SetValue(manager, state);
    }

    // UnitData의 직렬화된 팀을 테스트 값으로 설정합니다.
    private static void SetUnitTeam(UnitData data, UnitTeam team)
    {
        SerializedObject serialized = new(data); // UnitData 직렬화 객체
        serialized.FindProperty("team").enumValueIndex = (int)team;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
