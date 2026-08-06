using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

// 업그레이드 저장, 연구, 뽑기 비용, 패널 이벤트 갱신을 빠르게 확인합니다.
public static class UpgradeSmokeTestTool
{
    private const string SaveKey = "RaisingZombies.Upgrade.State";
    private const string BalancePath = "Assets/02. Scripts/Upgrade/UpgradeBalanceSettings_Default.asset";
    private const string PanelPath = "Assets/03. Prefabs/UI/Upgrade/UpgradePanel.prefab";

    [MenuItem("Tools/Raising Zombies/Upgrade/Run Core And UI Smoke Test")]
    public static void RunSmokeTest()
    {
        bool hadSavedState = PlayerPrefs.HasKey(SaveKey);
        string savedState = hadSavedState ? PlayerPrefs.GetString(SaveKey) : null;
        GameObject firstManagerObject = null;
        GameObject reloadedManagerObject = null;
        GameObject panelInstance = null;

        try
        {
            UpgradeBalanceSettings balance = AssetDatabase.LoadAssetAtPath<UpgradeBalanceSettings>(BalancePath);
            UpgradePanel panelPrefab = AssetDatabase.LoadAssetAtPath<UpgradePanel>(PanelPath);
            Assert(balance != null, $"기본 밸런스 에셋을 찾을 수 없습니다: {BalancePath}");
            Assert(panelPrefab != null, $"업그레이드 패널 프리팹을 찾을 수 없습니다: {PanelPath}");

            var validationErrors = new List<string>();
            balance.CollectValidationErrors(validationErrors);
            Assert(validationErrors.Count == 0, $"기본 밸런스 검증 실패: {string.Join(" | ", validationErrors)}");

            PlayerPrefs.DeleteKey(SaveKey);

            firstManagerObject = new GameObject("UpgradeSmokeTestManager");
            UpgradeManager firstManager = firstManagerObject.AddComponent<UpgradeManager>();
            AssignBalance(firstManager, balance);
            SetState(firstManager, CreateResearchTestState());

            UpgradeStatSnapshot beforeResearch = firstManager.GetStatSnapshot(UpgradeStatType.Health);
            int researchCost = firstManager.GetResearchCost(UpgradeStatType.Health);
            int currencyBeforeResearch = firstManager.Currency;
            Assert(firstManager.TryUpgradeResearch(UpgradeStatType.Health), "해금된 HP 연구 강화가 실패했습니다.");

            UpgradeStatSnapshot afterResearch = firstManager.GetStatSnapshot(UpgradeStatType.Health);
            Assert(afterResearch.RawAccumulatedValue == beforeResearch.RawAccumulatedValue,
                "연구 강화 후 원본 누적 수치가 변경됐습니다.");
            Assert(afterResearch.ResearchLevel == beforeResearch.ResearchLevel + 1, "연구 레벨이 증가하지 않았습니다.");
            Assert(afterResearch.FinalBonus > beforeResearch.FinalBonus, "연구 강화 후 최종 보너스가 증가하지 않았습니다.");
            Assert(firstManager.Currency == currencyBeforeResearch - researchCost, "연구 비용 차감값이 올바르지 않습니다.");

            UpgradeStatSnapshot amplifier = firstManager.GetStatSnapshot(UpgradeStatType.StatIncrease);
            Assert(Mathf.Approximately(amplifier.RawAccumulatedValue, amplifier.EffectiveAccumulatedValue),
                "수치 증가가 자기 자신을 증폭했습니다.");
            Assert(afterResearch.EffectiveAccumulatedValue > afterResearch.RawAccumulatedValue,
                "수치 증가가 다른 스탯의 유효 누적에 적용되지 않았습니다.");

            reloadedManagerObject = new GameObject("UpgradeSmokeTestReloadedManager");
            UpgradeManager reloadedManager = reloadedManagerObject.AddComponent<UpgradeManager>();
            AssignBalance(reloadedManager, balance);
            InvokeLoadState(reloadedManager);

            UpgradeStatSnapshot reloadedSnapshot = reloadedManager.GetStatSnapshot(UpgradeStatType.Health);
            Assert(reloadedSnapshot.RawAccumulatedValue == afterResearch.RawAccumulatedValue,
                "PlayerPrefs 재로딩 후 원본 누적 수치가 다릅니다.");
            Assert(reloadedSnapshot.ResearchLevel == afterResearch.ResearchLevel, "PlayerPrefs 재로딩 후 연구 레벨이 다릅니다.");

            panelInstance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab.gameObject);
            UpgradePanel panel = panelInstance.GetComponent<UpgradePanel>();
            panel.SetUpgradeManager(reloadedManager);

            TMP_Text currencyText = GetObjectReference<TMP_Text>(panel, "currencyText");
            Transform drawResultsRoot = GetObjectReference<Transform>(panel, "drawResultsRoot");
            Assert(currencyText != null && drawResultsRoot != null, "패널 내부 Inspector 참조가 연결되지 않았습니다.");

            string currencyBeforeEvent = currencyText.text;
            reloadedManager.AddCurrency(7);
            Assert(currencyText.text != currencyBeforeEvent, "StateChanged 후 패널 재화 표시가 갱신되지 않았습니다.");

            int tenDrawCost = reloadedManager.GetDrawCostForCount(10);
            int currencyBeforeDraw = reloadedManager.Currency;
            Assert(reloadedManager.TryDrawTen(out IReadOnlyList<GachaDrawResult> results), "10회 뽑기가 실패했습니다.");
            Assert(results != null && results.Count == 10, "10회 뽑기 결과 개수가 10개가 아닙니다.");
            Assert(reloadedManager.Currency == currencyBeforeDraw - tenDrawCost,
                "GetDrawCostForCount(10)과 실제 차감 비용이 다릅니다.");
            Assert(drawResultsRoot.childCount == 10, "DrawCompleted 후 최근 결과 행 10개가 표시되지 않았습니다.");

            Debug.Log("[UpgradeSmokeTest] 통과: 밸런스, 연구 원본 보존, 전역 증폭, PlayerPrefs 재로딩, 10회 비용, 패널 이벤트 갱신");
        }
        finally
        {
            if (panelInstance != null)
            {
                UnityEngine.Object.DestroyImmediate(panelInstance);
            }

            if (reloadedManagerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(reloadedManagerObject);
            }

            if (firstManagerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(firstManagerObject);
            }

            if (hadSavedState)
            {
                PlayerPrefs.SetString(SaveKey, savedState);
            }
            else
            {
                PlayerPrefs.DeleteKey(SaveKey);
            }

            PlayerPrefs.Save();
        }
    }

    private static UpgradeState CreateResearchTestState()
    {
        return new UpgradeState
        {
            currency = 100000,
            gachaLevel = 1,
            drawsAtCurrentLevel = 0,
            stats = new List<UpgradeStatValue>
            {
                new UpgradeStatValue
                {
                    statType = UpgradeStatType.Health,
                    accumulatedValue = 42,
                    researchLevel = 0
                },
                new UpgradeStatValue
                {
                    statType = UpgradeStatType.StatIncrease,
                    accumulatedValue = 10,
                    researchLevel = 0
                }
            }
        };
    }

    private static void AssignBalance(UpgradeManager manager, UpgradeBalanceSettings balance)
    {
        var serializedManager = new SerializedObject(manager);
        serializedManager.FindProperty("balanceSettings").objectReferenceValue = balance;
        serializedManager.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetState(UpgradeManager manager, UpgradeState state)
    {
        FieldInfo field = typeof(UpgradeManager).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(field != null, "UpgradeManager.state 필드를 찾을 수 없습니다.");
        field.SetValue(manager, state);
    }

    private static void InvokeLoadState(UpgradeManager manager)
    {
        MethodInfo method =
            typeof(UpgradeManager).GetMethod("LoadState", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert(method != null, "UpgradeManager.LoadState를 찾을 수 없습니다.");
        method.Invoke(manager, null);
    }

    private static T GetObjectReference<T>(UnityEngine.Object target, string propertyName) where T : UnityEngine.Object
    {
        var serializedObject = new SerializedObject(target);
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property == null ? null : property.objectReferenceValue as T;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"[UpgradeSmokeTest] {message}");
        }
    }
}
