using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 실제 지급된 오프라인 보상 한 건을 재지급 없이 표시합니다.
public sealed class OfflineCurrencyRewardPopup : MonoBehaviour
{
    [SerializeField] private UpgradeManager upgradeManager; // 보상 결과를 가져올 매니저
    [SerializeField] private TMP_Text actualTimeText; // 실제 오프라인 시간
    [SerializeField] private TMP_Text appliedTimeText; // 상한 적용 시간
    [SerializeField] private TMP_Text efficiencyText; // 적용 효율
    [SerializeField] private TMP_Text rewardText; // 최종 획득 재화
    [SerializeField] private Button confirmButton; // 팝업 닫기 버튼

    // 확인 버튼을 한 번 연결합니다.
    private void Awake()
    {
        if (confirmButton != null) confirmButton.onClick.AddListener(Close);
    }

    // 대기 중인 실제 보상이 있을 때만 팝업을 표시합니다.
    private void OnEnable()
    {
        if (upgradeManager == null || !upgradeManager.TryConsumeOfflineReward(out OfflineCurrencyReward reward))
        {
            gameObject.SetActive(false);
            return;
        }

        SetText(actualTimeText, $"실제 오프라인: {FormatDuration(reward.ActualSeconds)}");
        SetText(appliedTimeText, $"적립 적용 시간: {FormatDuration(reward.AppliedSeconds)}");
        SetText(efficiencyText, $"적립 효율: {reward.Efficiency * 100f:0.##}%");
        SetText(rewardText, $"획득 재화: {reward.EarnedCurrency:N0}");
    }

    // 확인 버튼 이벤트를 해제합니다.
    private void OnDestroy()
    {
        if (confirmButton != null) confirmButton.onClick.RemoveListener(Close);
    }

    // 이미 지급된 보상 팝업만 닫습니다.
    public void Close()
    {
        gameObject.SetActive(false);
    }

    // 초 단위 시간을 시·분 형태로 표시합니다.
    private static string FormatDuration(double seconds)
    {
        double safeSeconds = System.Math.Max(0d, seconds); // 표시할 음수 아닌 초
        int hours = (int)(safeSeconds / 3600d); // 전체 시간
        int minutes = (int)(safeSeconds % 3600d / 60d); // 남은 분
        return $"{hours}시간 {minutes}분";
    }

    // TMP 참조가 있을 때만 문자열을 설정합니다.
    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
