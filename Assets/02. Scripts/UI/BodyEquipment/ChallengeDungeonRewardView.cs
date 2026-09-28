using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 도전 던전 데이터의 보상 아이콘과 수량을 기존 Challenge UI에 표시한다.
public sealed class ChallengeDungeonRewardView : MonoBehaviour
{
    [SerializeField] private ChallengeDungeonClearBridge clearBridge; // 실제 지급과 동일한 던전 보상표 원본
    [SerializeField] private string dungeonId; // 현재 표시할 던전의 안정적인 ID
    [SerializeField] private Image rewardIcon; // 보상 데이터에 지정한 아이콘을 표시한다
    [SerializeField] private TMP_Text rewardAmountText; // 보상 수량만 표시한다

    // 현재 Inspector 보상 데이터로 최초 표시를 갱신한다.
    private void OnEnable()
    {
        BindDungeon(dungeonId);
    }

    // 다른 던전이 선택되면 실제 지급 보상표에서 그 던전의 데이터를 조회한다.
    public void BindDungeon(string selectedDungeonId)
    {
        dungeonId = selectedDungeonId;
        ChallengeDungeonRewardSO reward = null; // 브리지가 지급에도 사용하는 보상 에셋
        if (clearBridge != null) clearBridge.TryGetReward(dungeonId, out reward);
        if (rewardIcon != null)
        {
            rewardIcon.sprite = reward == null ? null : reward.Icon;
            rewardIcon.enabled = reward != null && reward.Icon != null;
        }
        if (rewardAmountText != null) rewardAmountText.text = reward == null ? string.Empty : reward.Amount.ToString();
    }
}
