using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 재화 강화 한 종류의 상태와 구매 버튼을 표시합니다.
public sealed class CurrencyUpgradeRowView : MonoBehaviour
{
    [SerializeField] private Image iconImage; // 선택적으로 표시할 강화 아이콘
    [SerializeField] private TMP_Text nameText; // 강화 이름
    [SerializeField] private TMP_Text descriptionText; // 강화 설명
    [SerializeField] private TMP_Text levelText; // 현재 및 최대 레벨
    [SerializeField] private TMP_Text currentEffectText; // 현재 효과
    [SerializeField] private TMP_Text nextEffectText; // 다음 효과
    [SerializeField] private TMP_Text costText; // 비용 또는 최대 레벨 문구
    [SerializeField] private Button upgradeButton; // 직접 구매 버튼
    private UpgradeManager _manager; // 연결된 업그레이드 매니저
    private CurrencyUpgradeType _type; // 이 행이 표시하는 강화 종류

    // 구매 버튼 이벤트를 한 번 연결합니다.
    private void Awake()
    {
        if (upgradeButton != null) upgradeButton.onClick.AddListener(HandleUpgradeClicked);
    }

    // 구매 버튼 이벤트를 해제합니다.
    private void OnDestroy()
    {
        if (upgradeButton != null) upgradeButton.onClick.RemoveListener(HandleUpgradeClicked);
    }

    // 행에 매니저와 강화 종류를 연결하고 최신 상태를 표시합니다.
    public void Bind(UpgradeManager manager, CurrencyUpgradeType type)
    {
        _manager = manager;
        _type = type;
        Refresh();
    }

    // 계산은 매니저에 맡기고 현재 표시와 버튼 상태만 갱신합니다.
    public void Refresh()
    {
        if (_manager == null)
        {
            if (upgradeButton != null) upgradeButton.interactable = false;
            return;
        }

        CurrencyUpgradeSnapshot snapshot = _manager.GetCurrencyUpgradeSnapshot(_type); // 표시할 계산 완료 강화 상태
        SetText(nameText, snapshot.DisplayName);
        SetText(descriptionText, snapshot.Description);
        SetText(levelText, $"Lv.{snapshot.CurrentLevel} / {snapshot.MaxLevel}");
        SetText(currentEffectText, $"현재 효과: {FormatEffect(snapshot.Type, snapshot.CurrentEffect)}");
        SetText(nextEffectText, snapshot.IsMaxLevel ? "다음 효과: -" : $"다음 효과: {FormatEffect(snapshot.Type, snapshot.NextEffect)}");
        SetText(costText, snapshot.IsMaxLevel ? "최대 레벨" : $"강화 · {snapshot.NextCost:N0}");
        if (upgradeButton != null) upgradeButton.interactable = !snapshot.IsMaxLevel && _manager.Currency >= snapshot.NextCost;
        if (iconImage != null) iconImage.enabled = iconImage.sprite != null;
    }

    // 강화 버튼 클릭을 매니저의 원자적 구매 함수로 전달합니다.
    private void HandleUpgradeClicked()
    {
        if (_manager != null) _manager.TryUpgradeCurrency(_type);
    }

    // 강화 종류에 맞는 단위로 누적 효과를 표시합니다.
    private static string FormatEffect(CurrencyUpgradeType type, float value)
    {
        return type switch
        {
            CurrencyUpgradeType.CurrencyPerSecond => $"초당 {value:0.##}",
            CurrencyUpgradeType.HumanKillBonus => $"처치당 +{value:0.##}",
            CurrencyUpgradeType.OfflineMaxTime => $"최대 {value:0.##}시간",
            CurrencyUpgradeType.OfflineEfficiency => $"{value * 100f:0.##}%",
            _ => value.ToString("0.##")
        };
    }

    // TMP 참조가 있을 때만 문자열을 설정합니다.
    private static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
