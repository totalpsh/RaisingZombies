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
    [SerializeField] private TMP_Text drawOneCostText; // 1회 버튼의 재화 아이콘 옆 실제 비용
    [SerializeField] private Button drawTenButton; // 10회 뽑기 버튼
    [SerializeField] private TMP_Text drawTenButtonText; // 정확한 10회 총비용 문구
    [SerializeField] private TMP_Text drawTenCostText; // 10회 버튼의 재화 아이콘 옆 실제 총비용

    [Header("새 StatUpgrade UI")]
    [SerializeField] private TMP_Text gachaLevelText; // 실제 현재 가챠 레벨을 표시한다.
    [SerializeField] private TMP_Text nextLevelProgressText; // 다음 레벨까지의 실제 뽑기 진행도를 표시한다.
    [SerializeField] private Slider gachaLevelSlider; // 현재 레벨의 실제 뽑기 진행도를 표시한다.
    [SerializeField] private StatUpgradeCardView[] statCards; // 고정 카드와 실제 StatType을 연결한 View 목록

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
    private readonly HashSet<StatUpgradeCardView> affectedStatCards = new(); // 한 번의 10회 뽑기에서 중복 Effect를 막을 카드 집합

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

        upgradeManager = UpgradeManager.Instance;
        SetUpgradeManager(upgradeManager);
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
            RefreshStatCards();
            return;
        }

        RefreshGachaArea();
        RefreshStatCards();
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
        RefreshDrawnStatCards(results);
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
        SetText(drawOneCostText, $"{oneDrawCost:N0}");
        SetText(drawTenButtonText, $"10회 뽑기 · {tenDrawCost:N0}");
        SetText(drawTenCostText, $"{tenDrawCost:N0}");

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

        RefreshGachaProgress(level, levelDefinition, nextLevelDefinition);

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

    // 실제 가챠 레벨과 다음 레벨 진행도를 새 Text와 Slider에 반영합니다.
    private void RefreshGachaProgress(int level, GachaLevelDefinition levelDefinition, GachaLevelDefinition nextLevelDefinition)
    {
        SetText(gachaLevelText, $"Lv.{level}");
        if (levelDefinition == null)
        {
            SetText(nextLevelProgressText, "Next - / -");
            SetSliderProgress(0, 1);
            return;
        }

        if (levelDefinition.drawsToNextLevel <= 0 || nextLevelDefinition == null)
        {
            SetText(nextLevelProgressText, "MAX");
            SetSliderProgress(1, 1);
            return;
        }

        int requiredDraws = Mathf.Max(1, levelDefinition.drawsToNextLevel); // 현재 레벨에서 다음 레벨까지 필요한 실제 횟수
        int currentDraws = Mathf.Clamp(upgradeManager.DrawsAtCurrentLevel, 0, requiredDraws); // 저장 데이터의 현재 진행 횟수
        SetText(nextLevelProgressText, $"Next {currentDraws} / {requiredDraws}");
        SetSliderProgress(currentDraws, requiredDraws);
    }

    // 새 Slider에 실제 횟수 범위와 현재값을 직접 적용합니다.
    private void SetSliderProgress(int current, int maximum)
    {
        if (gachaLevelSlider == null) return;
        int safeMaximum = Mathf.Max(1, maximum); // 0으로 인한 잘못된 Slider 범위를 막는 최대값
        gachaLevelSlider.wholeNumbers = true;
        gachaLevelSlider.minValue = 0f;
        gachaLevelSlider.maxValue = safeMaximum;
        gachaLevelSlider.value = Mathf.Clamp(current, 0, safeMaximum);
    }

    // 저장 데이터가 변경될 때 기존 카드 인스턴스를 유지한 채 수치만 갱신합니다.
    private void RefreshStatCards()
    {
        if (statCards == null) return;
        foreach (StatUpgradeCardView card in statCards) // 새 프리팹에 직렬화된 고정 카드
        {
            if (card != null) card.Bind(upgradeManager);
        }
    }

    // 뽑힌 스탯의 카드만 즉시 갱신하고 카드별 Effect를 한 번씩 실행합니다.
    private void RefreshDrawnStatCards(IReadOnlyList<GachaDrawResult> results)
    {
        if (results == null || statCards == null) return;
        affectedStatCards.Clear();
        foreach (GachaDrawResult result in results) // 기존 가챠 로직이 반환한 실제 뽑기 결과
        {
            foreach (StatUpgradeCardView card in statCards) // 결과와 연결된 카드를 찾기 위한 고정 View 목록
            {
                if (card == null || !card.ContainsStat(result.StatType)) continue;
                card.Refresh();
                affectedStatCards.Add(card);
                break;
            }
        }

        foreach (StatUpgradeCardView card in affectedStatCards) // 중복 결과를 합친 실제 선택 카드
        {
            card.PlayDrawEffect();
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
        SetText(gachaLevelText, "Lv.-");
        SetText(nextLevelProgressText, "Next - / -");
        SetSliderProgress(0, 1);
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
