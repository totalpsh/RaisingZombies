using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 현재 등급 확률과 가챠 레벨별 상세 페이지를 표시합니다.
public sealed class GachaProbabilityView : MonoBehaviour
{
    [SerializeField] private UpgradeManager upgradeManager; // 실제 가챠 상태와 밸런스를 제공하는 매니저

    [Header("현재 레벨 등급 확률")]
    [SerializeField] private TMP_Text normalRateText; // 노말 확률 텍스트
    [SerializeField] private TMP_Text uncommonRateText; // 언커먼 확률 텍스트
    [SerializeField] private TMP_Text rareRateText; // 레어 확률 텍스트
    [SerializeField] private TMP_Text epicRateText; // 에픽 확률 텍스트
    [SerializeField] private TMP_Text uniqueRateText; // 유니크 확률 텍스트
    [SerializeField] private TMP_Text legendaryRateText; // 레전더리 확률 텍스트

    [Header("상세 확률 팝업")]
    [SerializeField] private Button probabilityButton; // 상세 확률 팝업을 여는 버튼
    [SerializeField] private GameObject probabilityPopupRoot; // 켜고 끌 상세 확률 팝업 루트
    [SerializeField] private Button closeButton; // 상세 확률 팝업을 닫는 버튼

    [Header("레벨별 페이지")]
    [SerializeField] private GameObject[] levelPages; // 가챠 레벨 순서대로 배치한 페이지 루트
    [SerializeField] private TMP_Text[] pageDetailTexts; // 각 페이지의 기본 확률 안내 텍스트
    [SerializeField] private Button previousPageButton; // 이전 레벨 페이지 버튼
    [SerializeField] private Button nextPageButton; // 다음 레벨 페이지 버튼
    [SerializeField] private TMP_Text pageIndicatorText; // 현재 페이지 번호 텍스트

    private readonly StringBuilder _builder = new(512); // 페이지 문자열 재사용 버퍼
    private readonly List<UpgradeStatType> _unlockedStats = new(10); // 레벨별 누적 해금 스탯 목록
    private readonly HashSet<UpgradeStatType> _unlockedStatSet = new(); // 해금 스탯 중복 방지 집합
    private int _currentPageIndex; // 현재 표시 중인 페이지 배열 순번

    // 버튼과 상태 변경 이벤트를 연결하고 현재 확률을 표시합니다.
    private void OnEnable()
    {
        ResolveManager();
        AddButtonListener(probabilityButton, OpenProbabilityPopup);
        AddButtonListener(closeButton, CloseProbabilityPopup);
        AddButtonListener(previousPageButton, ShowPreviousPage);
        AddButtonListener(nextPageButton, ShowNextPage);
        SubscribeManager();
        CloseProbabilityPopup();
        RefreshAllContent();
    }

    // 비활성화될 때 버튼과 상태 변경 이벤트를 해제합니다.
    private void OnDisable()
    {
        RemoveButtonListener(probabilityButton, OpenProbabilityPopup);
        RemoveButtonListener(closeButton, CloseProbabilityPopup);
        RemoveButtonListener(previousPageButton, ShowPreviousPage);
        RemoveButtonListener(nextPageButton, ShowNextPage);
        UnsubscribeManager();
    }

    // 런타임 또는 테스트에서 사용할 매니저를 교체합니다.
    public void SetUpgradeManager(UpgradeManager manager)
    {
        UnsubscribeManager();
        upgradeManager = manager;
        SubscribeManager();
        RefreshAllContent();
    }

    // 현재 가챠 레벨의 여섯 등급 확률을 각각 표시합니다.
    public void RefreshCurrentRates()
    {
        UpgradeBalanceSettings balance = upgradeManager == null ? null : upgradeManager.BalanceSettings; // 현재 실제 가챠 밸런스
        int level = upgradeManager == null ? 1 : upgradeManager.GachaLevel; // 화면에 표시할 현재 가챠 레벨
        SetRateText(normalRateText, balance, level, GachaRarity.Normal);
        SetRateText(uncommonRateText, balance, level, GachaRarity.Uncommon);
        SetRateText(rareRateText, balance, level, GachaRarity.Rare);
        SetRateText(epicRateText, balance, level, GachaRarity.Epic);
        SetRateText(uniqueRateText, balance, level, GachaRarity.Unique);
        SetRateText(legendaryRateText, balance, level, GachaRarity.Legendary);
    }

    // 팝업을 열고 현재 가챠 레벨에 해당하는 페이지를 먼저 표시합니다.
    public void OpenProbabilityPopup()
    {
        if (probabilityPopupRoot == null) return;
        ResolveManager();
        SubscribeManager();
        RefreshPageDetails();
        probabilityPopupRoot.SetActive(true);
        ShowCurrentLevelPage();
    }

    // 상세 확률 팝업을 닫습니다.
    public void CloseProbabilityPopup()
    {
        if (probabilityPopupRoot != null) probabilityPopupRoot.SetActive(false);
    }

    // 현재 페이지의 바로 이전 레벨 페이지를 표시합니다.
    public void ShowPreviousPage()
    {
        ShowPage(_currentPageIndex - 1);
    }

    // 현재 페이지의 바로 다음 레벨 페이지를 표시합니다.
    public void ShowNextPage()
    {
        ShowPage(_currentPageIndex + 1);
    }

    // 저장된 현재 가챠 레벨과 같은 페이지를 표시합니다.
    public void ShowCurrentLevelPage()
    {
        int currentLevel = upgradeManager == null ? 1 : upgradeManager.GachaLevel; // 처음 열 페이지의 실제 가챠 레벨
        ShowPage(FindPageIndex(currentLevel));
    }

    // 씬의 싱글턴 매니저가 이미 준비됐다면 자동으로 참조합니다.
    private void ResolveManager()
    {
        if (upgradeManager == null && UpgradeManager.HasInstance) upgradeManager = UpgradeManager.Instance;
    }

    // 상태 변경 이벤트를 중복 없이 연결합니다.
    private void SubscribeManager()
    {
        if (!isActiveAndEnabled || upgradeManager == null) return;
        upgradeManager.stateChanged -= HandleStateChanged;
        upgradeManager.stateChanged += HandleStateChanged;
    }

    // 연결된 상태 변경 이벤트를 해제합니다.
    private void UnsubscribeManager()
    {
        if (upgradeManager != null) upgradeManager.stateChanged -= HandleStateChanged;
    }

    // 상태가 변경되면 현재 확률과 페이지 내용을 갱신합니다.
    private void HandleStateChanged()
    {
        RefreshAllContent();
    }

    // 현재 확률과 모든 페이지의 기본 내용을 함께 갱신합니다.
    private void RefreshAllContent()
    {
        RefreshCurrentRates();
        RefreshPageDetails();
    }

    // 각 페이지에 해당 레벨 하나의 해금 스탯과 등급 확률을 작성합니다.
    private void RefreshPageDetails()
    {
        if (pageDetailTexts == null || pageDetailTexts.Length == 0) return;

        _unlockedStats.Clear();
        _unlockedStatSet.Clear();

        UpgradeBalanceSettings balance = upgradeManager == null ? null : upgradeManager.BalanceSettings; // 페이지에 사용할 실제 밸런스
        if (balance == null || balance.GachaLevels == null || balance.GachaLevels.Count == 0)
        {
            SetAllPageTexts("가챠 밸런스가 연결되지 않았습니다.");
            return;
        }

        for (int index = 0; index < pageDetailTexts.Length; index++) // 내용을 갱신할 페이지 순번
        {
            TMP_Text pageText = pageDetailTexts[index]; // 현재 페이지의 기본 안내 텍스트
            if (index >= balance.GachaLevels.Count)
            {
                if (pageText != null) pageText.text = "가챠 레벨 정보가 없습니다.";
                continue;
            }

            GachaLevelDefinition levelDefinition = balance.GachaLevels[index]; // 현재 작성 중인 레벨 정의
            if (levelDefinition == null)
            {
                if (pageText != null) pageText.text = "가챠 레벨 정보가 없습니다.";
                continue;
            }

            AddNewlyUnlockedStats(levelDefinition);
            if (pageText == null) continue;
            _builder.Clear();
            AppendLevelDetails(balance, levelDefinition);
            pageText.text = _builder.ToString();
        }
    }

    // 지정한 순번의 페이지만 켜고 페이지 버튼 상태를 갱신합니다.
    private void ShowPage(int pageIndex)
    {
        int pageCount = levelPages == null ? 0 : levelPages.Length; // Inspector에 연결된 전체 페이지 수
        if (pageCount == 0)
        {
            _currentPageIndex = 0;
            UpdatePageNavigation(0);
            return;
        }

        _currentPageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
        for (int index = 0; index < pageCount; index++) // 페이지 활성 상태를 바꿀 배열 순번
        {
            if (levelPages[index] != null) levelPages[index].SetActive(index == _currentPageIndex);
        }

        UpdatePageNavigation(pageCount);
    }

    // 실제 가챠 레벨과 일치하는 페이지 배열 순번을 찾습니다.
    private int FindPageIndex(int level)
    {
        UpgradeBalanceSettings balance = upgradeManager == null ? null : upgradeManager.BalanceSettings; // 레벨과 페이지 순서를 연결할 밸런스
        if (balance != null && balance.GachaLevels != null)
        {
            for (int index = 0; index < balance.GachaLevels.Count; index++) // 현재 레벨을 찾을 밸런스 배열 순번
            {
                GachaLevelDefinition definition = balance.GachaLevels[index]; // 비교할 가챠 레벨 정의
                if (definition != null && definition.level == level) return index;
            }
        }

        return Mathf.Max(0, level - 1);
    }

    // 페이지 번호와 좌우 이동 버튼의 활성 상태를 갱신합니다.
    private void UpdatePageNavigation(int pageCount)
    {
        if (previousPageButton != null) previousPageButton.interactable = pageCount > 0 && _currentPageIndex > 0;
        if (nextPageButton != null) nextPageButton.interactable = pageCount > 0 && _currentPageIndex < pageCount - 1;
        if (pageIndicatorText == null) return;

        int level = GetPageLevel(_currentPageIndex); // 페이지 표시기에 보여줄 실제 가챠 레벨
        pageIndicatorText.text = pageCount == 0
            ? "페이지 없음"
            : $"Lv.{level} · {_currentPageIndex + 1} / {pageCount}";
    }

    // 페이지 배열 순번에 해당하는 실제 가챠 레벨을 반환합니다.
    private int GetPageLevel(int pageIndex)
    {
        UpgradeBalanceSettings balance = upgradeManager == null ? null : upgradeManager.BalanceSettings; // 페이지 레벨을 조회할 밸런스
        if (balance != null && balance.GachaLevels != null && pageIndex >= 0 && pageIndex < balance.GachaLevels.Count)
        {
            GachaLevelDefinition definition = balance.GachaLevels[pageIndex]; // 페이지와 연결된 가챠 레벨 정의
            if (definition != null) return definition.level;
        }

        return pageIndex + 1;
    }

    // 연결된 모든 페이지 텍스트에 같은 안내 문구를 설정합니다.
    private void SetAllPageTexts(string message)
    {
        for (int index = 0; index < pageDetailTexts.Length; index++) // 안내 문구를 설정할 페이지 텍스트 순번
        {
            if (pageDetailTexts[index] != null) pageDetailTexts[index].text = message;
        }
    }

    // 현재 레벨에서 새로 해금되는 스탯을 누적 목록에 추가합니다.
    private void AddNewlyUnlockedStats(GachaLevelDefinition levelDefinition)
    {
        if (levelDefinition.newlyUnlockedStats == null) return;
        foreach (UpgradeStatType statType in levelDefinition.newlyUnlockedStats) // 이번 레벨의 신규 해금 스탯
        {
            if (_unlockedStatSet.Add(statType)) _unlockedStats.Add(statType);
        }
    }

    // 한 레벨의 등장 스탯 확률과 여섯 등급 확률을 작성합니다.
    private void AppendLevelDetails(UpgradeBalanceSettings balance, GachaLevelDefinition levelDefinition)
    {
        _builder.Append("Lv.").Append(levelDefinition.level).AppendLine();
        _builder.AppendLine("등장 스탯");
        float statProbability = _unlockedStats.Count == 0 ? 0f : 100f / _unlockedStats.Count; // 해금 스탯 균등 선택 확률
        for (int index = 0; index < _unlockedStats.Count; index++) // 표시할 누적 해금 스탯 순번
        {
            UpgradeStatType statType = _unlockedStats[index]; // 표시할 스탯 종류
            UpgradeStatDefinition statDefinition = balance.GetStat(statType); // 한국어 표시명을 가진 스탯 정의
            string displayName = statDefinition == null || string.IsNullOrWhiteSpace(statDefinition.displayName)
                ? statType.ToString()
                : statDefinition.displayName; // 최종 스탯 표시명
            _builder.Append(displayName).Append(' ').Append(FormatPercent(statProbability)).AppendLine();
        }

        _builder.AppendLine("등급 확률");
        AppendRarityRate(balance, levelDefinition.level, "노말", GachaRarity.Normal);
        AppendRarityRate(balance, levelDefinition.level, "언커먼", GachaRarity.Uncommon);
        AppendRarityRate(balance, levelDefinition.level, "레어", GachaRarity.Rare);
        AppendRarityRate(balance, levelDefinition.level, "에픽", GachaRarity.Epic);
        AppendRarityRate(balance, levelDefinition.level, "유니크", GachaRarity.Unique);
        AppendRarityRate(balance, levelDefinition.level, "레전더리", GachaRarity.Legendary);
    }

    // 팝업에 한 등급의 실제 정규화 확률을 추가합니다.
    private void AppendRarityRate(UpgradeBalanceSettings balance, int level, string displayName, GachaRarity rarity)
    {
        float percentage = balance.GetRarityProbability(level, rarity) * 100f; // 팝업에 표시할 실제 퍼센트
        _builder.Append(displayName).Append(' ').Append(FormatPercent(percentage)).AppendLine();
    }

    // 현재 화면의 등급별 TMP 텍스트에 실제 정규화 확률을 설정합니다.
    private static void SetRateText(TMP_Text target, UpgradeBalanceSettings balance, int level, GachaRarity rarity)
    {
        if (target == null) return;
        float percentage = balance == null ? 0f : balance.GetRarityProbability(level, rarity) * 100f; // 현재 등급 표시 퍼센트
        target.text = FormatPercent(percentage);
    }

    // 확률을 불필요한 소수점 없이 퍼센트 문자열로 만듭니다.
    private static string FormatPercent(float percentage)
    {
        return $"{percentage:0.##}%";
    }

    // 버튼 이벤트를 중복 제거 후 연결합니다.
    private static void AddButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // 연결된 버튼 이벤트를 해제합니다.
    private static void RemoveButtonListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.RemoveListener(action);
    }
}
