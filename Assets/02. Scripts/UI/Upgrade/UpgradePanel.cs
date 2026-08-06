using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 업그레이드 가챠와 계수 연구를 이벤트 기반으로 표시하는 패널입니다.
public sealed class UpgradePanel : MonoBehaviour
{
    [Header("데이터")]
    [SerializeField] private UpgradeManager upgradeManager; // 씬에 배치된 업그레이드 매니저
    [SerializeField] private TMP_FontAsset uiFont; // 선택 사항인 한국어 지원 TMP 폰트

    [Header("가챠 상태")]
    [SerializeField] private TMP_Text currencyText; // 현재 재화
    [SerializeField] private TMP_Text gachaProgressText; // 현재 가챠 레벨과 진행도
    [SerializeField] private TMP_Text unlockedStatsText; // 현재 해금된 스탯 목록
    [SerializeField] private TMP_Text drawStatusText; // 재화 부족 또는 설정 오류 안내
    [SerializeField] private Button drawOneButton; // 1회 뽑기 버튼
    [SerializeField] private TMP_Text drawOneButtonText; // 1회 뽑기 비용 문구
    [SerializeField] private Button drawTenButton; // 10회 뽑기 버튼
    [SerializeField] private TMP_Text drawTenButtonText; // 정확한 10회 총비용 문구

    [Header("최근 결과")]
    [SerializeField] private Transform drawResultsRoot; // 최근 결과 행이 생성될 부모
    [SerializeField] private UpgradeDrawResultView drawResultPrefab; // 최근 결과 행 프리팹
    [SerializeField] private TMP_Text unlockNoticeText; // 레벨 상승과 신규 해금 안내
    [SerializeField, Min(1)] private int maxVisibleDrawResults = 10; // 화면에 유지할 최근 결과 수

    [Header("계수 연구")]
    [SerializeField] private Transform researchRowsRoot; // 연구 스탯 행이 생성될 부모
    [SerializeField] private UpgradeStatRowView statRowPrefab; // 재사용할 연구 스탯 행 프리팹

    private readonly Dictionary<UpgradeStatType, UpgradeStatRowView> researchRows = new();
    private readonly List<UpgradeDrawResultView> drawResultViews = new(10);
    private readonly HashSet<int> announcedLevels = new();
    private readonly StringBuilder textBuilder = new(256);

    private void Awake()
    {
        if (drawOneButton != null)
        {
            drawOneButton.onClick.AddListener(HandleDrawOneClicked);
        }

        if (drawTenButton != null)
        {
            drawTenButton.onClick.AddListener(HandleDrawTenClicked);
        }
    }

    private void OnEnable()
    {
        ApplyUiFont();
        SubscribeManagerEvents();
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeManagerEvents();
    }

    private void OnDestroy()
    {
        if (drawOneButton != null)
        {
            drawOneButton.onClick.RemoveListener(HandleDrawOneClicked);
        }

        if (drawTenButton != null)
        {
            drawTenButton.onClick.RemoveListener(HandleDrawTenClicked);
        }
    }

    // 씬에서 사용할 UpgradeManager를 코드로 연결할 때 사용합니다.
    public void SetUpgradeManager(UpgradeManager manager)
    {
        if (upgradeManager == manager)
        {
            Refresh();
            return;
        }

        UnsubscribeManagerEvents();
        upgradeManager = manager;
        SubscribeManagerEvents();
        Refresh();
    }

    // 패널과 동적으로 생성되는 모든 행에 사용할 TMP 폰트를 지정합니다.
    public void SetUiFont(TMP_FontAsset fontAsset)
    {
        uiFont = fontAsset;
        ApplyUiFont();
    }

    // 패널을 열거나 저장 데이터가 변경됐을 때 전체 표시를 갱신합니다.
    public void Refresh()
    {
        if (upgradeManager == null)
        {
            SetMissingManagerState();
            return;
        }

        RefreshGachaArea();
        RefreshResearchRows();
    }

    private void SubscribeManagerEvents()
    {
        if (!isActiveAndEnabled || upgradeManager == null)
        {
            return;
        }

        upgradeManager.stateChanged -= HandleStateChanged;
        upgradeManager.drawCompleted -= HandleDrawCompleted;
        upgradeManager.stateChanged += HandleStateChanged;
        upgradeManager.drawCompleted += HandleDrawCompleted;
    }

    private void UnsubscribeManagerEvents()
    {
        if (upgradeManager == null)
        {
            return;
        }

        upgradeManager.stateChanged -= HandleStateChanged;
        upgradeManager.drawCompleted -= HandleDrawCompleted;
    }

    private void HandleStateChanged()
    {
        Refresh();
    }

    private void HandleDrawCompleted(IReadOnlyList<GachaDrawResult> results)
    {
        ShowDrawResults(results);
        ShowUnlockNotices(results);
    }

    private void HandleDrawOneClicked()
    {
        if (upgradeManager == null || !upgradeManager.TryDrawOne(out _))
        {
            SetText(drawStatusText, "재화가 부족하거나 가챠 밸런스 설정을 확인해야 합니다.");
        }
    }

    private void HandleDrawTenClicked()
    {
        if (upgradeManager == null || !upgradeManager.TryDrawTen(out _))
        {
            SetText(drawStatusText, "재화가 부족하거나 가챠 밸런스 설정을 확인해야 합니다.");
        }
    }

    private void RefreshGachaArea()
    {
        UpgradeBalanceSettings balanceSettings = upgradeManager.BalanceSettings;
        int level = upgradeManager.GachaLevel;
        GachaLevelDefinition levelDefinition = balanceSettings == null ? null : balanceSettings.GetGachaLevel(level);
        GachaLevelDefinition nextLevelDefinition = balanceSettings == null ? null : balanceSettings.GetGachaLevel(level + 1);
        int oneDrawCost = upgradeManager.GetCurrentDrawCost();
        int tenDrawCost = upgradeManager.GetDrawCostForCount(10);

        SetText(currencyText, $"현재 재화: {upgradeManager.Currency:N0}");
        SetText(drawOneButtonText, $"1회 뽑기 · {oneDrawCost:N0}");
        SetText(drawTenButtonText, $"10회 뽑기 · {tenDrawCost:N0}");

        if (levelDefinition == null)
        {
            SetText(gachaProgressText, $"Lv.{level} · 설정 없음");
        }
        else if (levelDefinition.drawsToNextLevel <= 0 || nextLevelDefinition == null)
        {
            SetText(gachaProgressText, $"Lv.{level} · MAX");
        }
        else
        {
            SetText(gachaProgressText, $"Lv.{level} · {upgradeManager.DrawsAtCurrentLevel} / {levelDefinition.drawsToNextLevel}");
        }

        List<UpgradeStatType> unlockedStats = upgradeManager.GetUnlockedStats();
        SetText(unlockedStatsText, BuildUnlockedStatsText(unlockedStats, balanceSettings));

        bool balanceAvailable = balanceSettings != null && levelDefinition != null && unlockedStats.Count > 0;
        bool canDrawOne = balanceAvailable && upgradeManager.Currency >= oneDrawCost;
        bool canDrawTen = balanceAvailable && upgradeManager.Currency >= tenDrawCost;

        if (drawOneButton != null)
        {
            drawOneButton.interactable = canDrawOne;
        }

        if (drawTenButton != null)
        {
            drawTenButton.interactable = canDrawTen;
        }

        if (!balanceAvailable)
        {
            SetText(drawStatusText, "가챠 밸런스 또는 해금 스탯 설정을 확인해 주세요.");
        }
        else if (!canDrawOne)
        {
            SetText(drawStatusText, $"재화 부족 · 1회 뽑기에 {oneDrawCost:N0} 필요");
        }
        else if (!canDrawTen)
        {
            SetText(drawStatusText, $"10회 뽑기에는 총 {tenDrawCost:N0} 필요");
        }
        else
        {
            SetText(drawStatusText, string.Empty);
        }
    }

    private void RefreshResearchRows()
    {
        foreach (KeyValuePair<UpgradeStatType, UpgradeStatRowView> pair in researchRows)
        {
            if (pair.Value != null)
            {
                pair.Value.gameObject.SetActive(false);
            }
        }

        List<UpgradeStatType> unlockedStats = upgradeManager.GetUnlockedStats();
        foreach (UpgradeStatType statType in unlockedStats)
        {
            UpgradeStatRowView row = GetOrCreateResearchRow(statType);
            if (row == null)
            {
                continue;
            }

            row.gameObject.SetActive(true);
            row.Bind(upgradeManager, statType);
        }
    }

    private UpgradeStatRowView GetOrCreateResearchRow(UpgradeStatType statType)
    {
        if (researchRows.TryGetValue(statType, out UpgradeStatRowView row) && row != null)
        {
            return row;
        }

        if (statRowPrefab == null || researchRowsRoot == null)
        {
            return null;
        }

        row = Instantiate(statRowPrefab, researchRowsRoot);
        row.name = $"{statType}Row";
        row.SetFont(uiFont);
        researchRows[statType] = row;
        return row;
    }

    private void ShowDrawResults(IReadOnlyList<GachaDrawResult> results)
    {
        int visibleCount = results == null ? 0 : Mathf.Min(results.Count, maxVisibleDrawResults);
        EnsureDrawResultViews(visibleCount);

        for (var index = 0; index < drawResultViews.Count; index++)
        {
            UpgradeDrawResultView view = drawResultViews[index];
            bool visible = index < visibleCount;
            view.gameObject.SetActive(visible);
            if (visible)
            {
                view.Bind(upgradeManager.BalanceSettings, results[index]);
            }
        }
    }

    private void EnsureDrawResultViews(int count)
    {
        if (drawResultPrefab == null || drawResultsRoot == null)
        {
            return;
        }

        while (drawResultViews.Count < count)
        {
            UpgradeDrawResultView view = Instantiate(drawResultPrefab, drawResultsRoot);
            view.name = $"DrawResult_{drawResultViews.Count + 1}";
            view.SetFont(uiFont);
            drawResultViews.Add(view);
        }
    }

    private void ApplyUiFont()
    {
        if (uiFont == null)
        {
            return;
        }

        TMP_Text[] panelTexts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in panelTexts)
        {
            text.font = uiFont;
        }

        foreach (UpgradeDrawResultView resultView in drawResultViews)
        {
            if (resultView != null) resultView.SetFont(uiFont);
        }

        foreach (KeyValuePair<UpgradeStatType, UpgradeStatRowView> pair in researchRows)
        {
            if (pair.Value != null) pair.Value.SetFont(uiFont);
        }
    }

    private void ShowUnlockNotices(IReadOnlyList<GachaDrawResult> results)
    {
        if (results == null || upgradeManager == null)
        {
            return;
        }

        announcedLevels.Clear();
        textBuilder.Clear();

        foreach (GachaDrawResult result in results)
        {
            if (!result.LevelIncreased || !announcedLevels.Add(result.NewLevel))
            {
                continue;
            }

            if (textBuilder.Length > 0)
            {
                textBuilder.AppendLine();
            }

            textBuilder.Append("Lv.").Append(result.NewLevel).Append(" 달성");
            GachaLevelDefinition definition = upgradeManager.BalanceSettings == null
                ? null
                : upgradeManager.BalanceSettings.GetGachaLevel(result.NewLevel);

            if (definition?.newlyUnlockedStats == null || definition.newlyUnlockedStats.Length == 0)
            {
                continue;
            }

            textBuilder.Append(" · 신규 해금: ");
            AppendStatNames(textBuilder, definition.newlyUnlockedStats, upgradeManager.BalanceSettings);
        }

        if (textBuilder.Length > 0)
        {
            SetText(unlockNoticeText, textBuilder.ToString());
        }
    }

    private string BuildUnlockedStatsText(IReadOnlyList<UpgradeStatType> stats, UpgradeBalanceSettings balanceSettings)
    {
        textBuilder.Clear();
        textBuilder.Append("해금 스탯: ");

        if (stats == null || stats.Count == 0)
        {
            textBuilder.Append("없음");
            return textBuilder.ToString();
        }

        for (var index = 0; index < stats.Count; index++)
        {
            if (index > 0)
            {
                textBuilder.Append(", ");
            }

            textBuilder.Append(GetStatDisplayName(stats[index], balanceSettings));
        }

        return textBuilder.ToString();
    }

    private static void AppendStatNames(StringBuilder builder, IReadOnlyList<UpgradeStatType> stats, UpgradeBalanceSettings balanceSettings)
    {
        for (var index = 0; index < stats.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(", ");
            }

            builder.Append(GetStatDisplayName(stats[index], balanceSettings));
        }
    }

    private static string GetStatDisplayName(UpgradeStatType statType, UpgradeBalanceSettings balanceSettings)
    {
        UpgradeStatDefinition definition = balanceSettings == null ? null : balanceSettings.GetStat(statType);
        return definition == null || string.IsNullOrWhiteSpace(definition.displayName)
            ? statType.ToString()
            : definition.displayName;
    }

    private void SetMissingManagerState()
    {
        SetText(currencyText, "현재 재화: -");
        SetText(gachaProgressText, "UpgradeManager 연결 필요");
        SetText(unlockedStatsText, "해금 스탯: -");
        SetText(drawStatusText, "Inspector에서 씬의 UpgradeManager를 연결해 주세요.");

        if (drawOneButton != null)
        {
            drawOneButton.interactable = false;
        }

        if (drawTenButton != null)
        {
            drawTenButton.interactable = false;
        }
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
