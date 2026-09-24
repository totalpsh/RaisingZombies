using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도전 던전 데이터의 보상 아이콘과 수량을 기존 Challenge UI에 표시한다.
public sealed class ChallengeDungeonRewardView : MonoBehaviour
{
    [SerializeField] private ChallengeDungeonRewardSO reward; // 현재 표시할 도전 던전 보상 데이터
    [SerializeField] private Image rewardIcon; // 보상 데이터에 지정한 아이콘을 표시한다
    [SerializeField] private TMP_Text rewardAmountText; // 보상 수량만 표시한다

    // 현재 Inspector 보상 데이터로 최초 표시를 갱신한다.
    private void OnEnable()
    {
        Bind(reward);
    }

    // 다른 던전이 선택되면 그 던전의 데이터로 표시를 바꾼다.
    public void Bind(ChallengeDungeonRewardSO definition)
    {
        reward = definition;
        if (rewardIcon != null)
        {
            rewardIcon.sprite = reward == null ? null : reward.Icon;
            rewardIcon.enabled = reward != null && reward.Icon != null;
        }
        if (rewardAmountText != null) rewardAmountText.text = reward == null ? string.Empty : reward.Amount.ToString();
    }
}
