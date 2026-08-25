using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 현재 등급 확률과 레벨별 상세 확률 팝업을 표시합니다.
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
    [SerializeField] private TMP_Text probabilityDetailText; // 모든 가챠 레벨의 상세 내용을 표시할 텍스트
    [SerializeField] private Button closeButton; // 상세 확률 팝업을 닫는 버튼

    private readonly StringBuilder _builder = new(1024); // 팝업 문자열 재사용 버퍼
    private readonly List<UpgradeStatType> _unlockedStats = new(10); // 레벨별 누적 해금 스탯 목록
    private readonly HashSet<UpgradeStatType> _unlockedStatSet = new(); // 해금 스탯 중복 방지 집합

    // 버튼과 상태 변경 이벤트를 연결하고 현재 확률을 표시합니다.
    private void OnEnable()
    {
        ResolveManager();
        AddButtonListener(probabilityButton, OpenProbabilityPopup);
        AddButtonListener(closeButton, CloseProbabilityPopup);
        SubscribeManager();
        CloseProbabilityPopup();
        RefreshCurrentRates();
    }

    // 비활성화될 때 버튼과 상태 변경 이벤트를 해제합니다.
    private void OnDisable()
    {
        RemoveButtonListener(probabilityButton, OpenProbabilityPopup);
        RemoveButtonListener(closeButton, CloseProbabilityPopup);
        UnsubscribeManager();
    }

    // 런타임 또는 테스트에서 사용할 매니저를 교체합니다.
    public void SetUpgradeManager(UpgradeManager manager)
    {
        UnsubscribeManager();
        upgradeManager = manager;
        SubscribeManager();
        RefreshCurrentRates();
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

    // 모든 가챠 레벨의 해금 스탯과 등급 확률을 작성하고 팝업을 엽니다.
    public void OpenProbabilityPopup()
    {
        if (probabilityPopupRoot == null) return;
        if (probabilityDetailText != null) probabilityDetailText.text = BuildProbabilityDetails();
        probabilityPopupRoot.SetActive(true);
    }

    // 상세 확률 팝업을 닫습니다.
    public void CloseProbabilityPopup()
    {
        if (probabilityPopupRoot != null) probabilityPopupRoot.SetActive(false);
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
        upgradeManager.stateChanged -= RefreshCurrentRates;
        upgradeManager.stateChanged += RefreshCurrentRates;
    }

    // 연결된 상태 변경 이벤트를 해제합니다.
    private void UnsubscribeManager()
    {
        if (upgradeManager != null) upgradeManager.stateChanged -= RefreshCurrentRates;
    }

    // 레벨 순서대로 누적 해금 스탯과 정규화된 확률을 문자열로 만듭니다.
    private string BuildProbabilityDetails()
    {
        _builder.Clear();
        _unlockedStats.Clear();
        _unlockedStatSet.Clear();

        UpgradeBalanceSettings balance = upgradeManager == null ? null : upgradeManager.BalanceSettings; // 팝업에 사용할 실제 밸런스
        if (balance == null || balance.GachaLevels == null || balance.GachaLevels.Count == 0)
        {
            return "가챠 밸런스가 연결되지 않았습니다.";
        }

        for (int index = 0; index < balance.GachaLevels.Count; index++) // 표시할 가챠 레벨 배열 순번
        {
            GachaLevelDefinition levelDefinition = balance.GachaLevels[index]; // 현재 작성 중인 레벨 정의
            if (levelDefinition == null) continue;
            AddNewlyUnlockedStats(levelDefinition);
            if (_builder.Length > 0) _builder.AppendLine().AppendLine();
            AppendLevelDetails(balance, levelDefinition);
        }

        return _builder.ToString();
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
