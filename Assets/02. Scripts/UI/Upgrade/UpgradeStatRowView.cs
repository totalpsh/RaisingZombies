using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>해금된 스탯 하나의 누적값, 연구 효율, 최종 보너스를 표시합니다.</summary>
public sealed class UpgradeStatRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text statNameText; // 스탯 표시명
    [SerializeField] private TMP_Text rawValueText; // 증폭 전 원본 누적 수치
    [SerializeField] private TMP_Text effectiveValueText; // 수치 증가 적용 후 유효 누적 수치
    [SerializeField] private TMP_Text researchLevelText; // 현재 연구 레벨
    [SerializeField] private TMP_Text perPointEfficiencyText; // 수치 1당 현재 효율
    [SerializeField] private TMP_Text finalBonusText; // 실제 적용할 최종 보너스
    [SerializeField] private TMP_Text descriptionText; // 전역 증폭 스탯 설명
    [SerializeField] private Button researchButton; // 연구 강화 실행 버튼
    [SerializeField] private TMP_Text researchButtonText; // 다음 연구 비용이 포함된 버튼 문구

    private UpgradeManager upgradeManager;
    private UpgradeStatType statType;

    private void Awake()
    {
        if (researchButton != null)
        {
            researchButton.onClick.AddListener(HandleResearchClicked);
        }
    }

    private void OnDestroy()
    {
        if (researchButton != null)
        {
            researchButton.onClick.RemoveListener(HandleResearchClicked);
        }
    }

    /// <summary>이 행이 표시할 매니저와 스탯을 연결합니다.</summary>
    public void Bind(UpgradeManager manager, UpgradeStatType type)
    {
        upgradeManager = manager;
        statType = type;
        Refresh();
    }

    /// <summary>패널에서 지정한 TMP 폰트를 연구 행 전체에 적용합니다.</summary>
    public void SetFont(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null)
        {
            return;
        }

        if (statNameText != null) statNameText.font = fontAsset;
        if (rawValueText != null) rawValueText.font = fontAsset;
        if (effectiveValueText != null) effectiveValueText.font = fontAsset;
        if (researchLevelText != null) researchLevelText.font = fontAsset;
        if (perPointEfficiencyText != null) perPointEfficiencyText.font = fontAsset;
        if (finalBonusText != null) finalBonusText.font = fontAsset;
        if (descriptionText != null) descriptionText.font = fontAsset;
        if (researchButtonText != null) researchButtonText.font = fontAsset;
    }

    /// <summary>현재 저장 상태를 기준으로 연구 관련 표시만 갱신합니다.</summary>
    public void Refresh()
    {
        UpgradeBalanceSettings balanceSettings = upgradeManager == null ? null : upgradeManager.BalanceSettings;
        UpgradeStatDefinition definition = balanceSettings == null ? null : balanceSettings.GetStat(statType);

        if (upgradeManager == null || definition == null)
        {
            SetUnavailableState();
            return;
        }

        UpgradeStatSnapshot snapshot = upgradeManager.GetStatSnapshot(statType);
        int researchCost = upgradeManager.GetResearchCost(statType);

        SetText(statNameText, definition.displayName);
        SetText(rawValueText, $"원본 누적: {snapshot.RawAccumulatedValue}");
        SetText(effectiveValueText, $"유효 누적: {snapshot.EffectiveAccumulatedValue:0.##}");
        SetText(researchLevelText, $"연구 Lv.{snapshot.ResearchLevel}");
        SetText(perPointEfficiencyText, FormatPerPointEfficiency(definition, snapshot.PerPointEfficiency));
        SetText(finalBonusText, FormatFinalBonus(definition, snapshot.FinalBonus));
        SetText(descriptionText, definition.resultKind == UpgradeResultKind.GlobalAmplifier
            ? "다른 스탯의 유효 누적을 증폭하며, 자기 자신에는 적용되지 않습니다."
            : string.Empty);
        SetText(researchButtonText, $"연구 강화 · {researchCost:N0}");

        if (researchButton != null)
        {
            researchButton.interactable = upgradeManager.IsUnlocked(statType) && upgradeManager.Currency >= researchCost;
        }
    }

    private void HandleResearchClicked()
    {
        if (upgradeManager != null)
        {
            upgradeManager.TryUpgradeResearch(statType);
        }
    }

    private void SetUnavailableState()
    {
        SetText(statNameText, statType.ToString());
        SetText(rawValueText, "밸런스 정의 없음");
        SetText(effectiveValueText, string.Empty);
        SetText(researchLevelText, string.Empty);
        SetText(perPointEfficiencyText, string.Empty);
        SetText(finalBonusText, string.Empty);
        SetText(descriptionText, string.Empty);
        SetText(researchButtonText, "연구 불가");

        if (researchButton != null)
        {
            researchButton.interactable = false;
        }
    }

    private static string FormatPerPointEfficiency(UpgradeStatDefinition definition, float efficiency)
    {
        if (definition.resultKind == UpgradeResultKind.Integer)
        {
            return $"수치 1당 효율: {efficiency:0.##}";
        }

        return $"수치 1당 효율: {efficiency * 100f:0.##}%";
    }

    private static string FormatFinalBonus(UpgradeStatDefinition definition, float finalBonus)
    {
        if (definition.resultKind == UpgradeResultKind.Integer)
        {
            return $"최종 보너스: {definition.displayName} +{Mathf.FloorToInt(finalBonus)}";
        }

        if (definition.resultKind == UpgradeResultKind.GlobalAmplifier)
        {
            return $"최종 전역 증폭: +{finalBonus * 100f:0.##}%";
        }

        return $"최종 보너스: {definition.displayName} +{finalBonus * 100f:0.##}%";
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }
}
