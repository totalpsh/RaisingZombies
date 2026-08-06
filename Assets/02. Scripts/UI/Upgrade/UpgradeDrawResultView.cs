using TMPro;
using UnityEngine;

// 최근 가챠 결과 한 건의 스탯, 획득 수치, 누적 수치를 표시합니다.
public sealed class UpgradeDrawResultView : MonoBehaviour
{
    [SerializeField] private TMP_Text statNameText; // 획득한 스탯 표시명
    [SerializeField] private TMP_Text valueText; // 이번 뽑기 수치
    [SerializeField] private TMP_Text totalText; // 해당 스탯 누적 수치
    [SerializeField] private Color normalColor = Color.white; // 1~6 일반 색상
    [SerializeField] private Color rareColor = new Color(0.35f, 0.8f, 1f); // 7~8 희귀 색상
    [SerializeField] private Color jackpotColor = new Color(1f, 0.75f, 0.15f); // 9~10 잭팟 색상

    // 뽑기 결과를 현재 행에 표시합니다.
    public void Bind(UpgradeBalanceSettings balanceSettings, GachaDrawResult result)
    {
        UpgradeStatDefinition definition = balanceSettings == null ? null : balanceSettings.GetStat(result.StatType);
        string displayName = definition == null || string.IsNullOrWhiteSpace(definition.displayName)
            ? result.StatType.ToString()
            : definition.displayName;

        if (statNameText != null)
        {
            statNameText.text = displayName;
        }

        if (valueText != null)
        {
            valueText.text = $"+{result.Value}";
            valueText.color = GetValueColor(result.Value);
        }

        if (totalText != null)
        {
            totalText.text = $"누적 {result.Total}";
        }
    }

    // 패널에서 지정한 TMP 폰트를 결과 행 전체에 적용합니다. 
    public void SetFont(TMP_FontAsset fontAsset)
    {
        if (fontAsset == null)
        {
            return;
        }

        if (statNameText != null) statNameText.font = fontAsset;
        if (valueText != null) valueText.font = fontAsset;
        if (totalText != null) totalText.font = fontAsset;
    }

    private Color GetValueColor(int value)
    {
        if (value >= 9)
        {
            return jackpotColor;
        }

        return value >= 7 ? rareColor : normalColor;
    }
}
