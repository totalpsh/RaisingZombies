using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 현재 실제 좀비 전투 스탯을 작은 오버레이 패널에 표시합니다.
public sealed class CombatStatInfoPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text detailsText; // 전투력과 실제 적용 중인 최종 스탯을 줄 단위로 표시할 텍스트
    [SerializeField] private Button closeButton; // 정보 패널을 닫는 버튼

    private bool closeListenerRegistered; // 닫기 버튼 Listener 중복 등록 방지 상태

    public bool IsOpen => gameObject.activeSelf; // 현재 정보 패널 표시 여부

    // 비활성 프리팹에서도 닫기 버튼 Listener를 한 번 연결합니다.
    private void Awake()
    {
        RegisterCloseListener();
    }

    // 패널이 활성화될 때 닫기 버튼 연결을 보장합니다.
    private void OnEnable()
    {
        RegisterCloseListener();
    }

    // 패널이 제거될 때 닫기 버튼 Listener를 해제합니다.
    private void OnDestroy()
    {
        UnregisterCloseListener();
    }

    // 최신 전투 스탯을 반영한 뒤 패널을 엽니다.
    public void Show(CombatPowerSnapshot snapshot)
    {
        Refresh(snapshot);
        gameObject.SetActive(true);
    }

    // 현재 정보 패널을 닫습니다.
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // 패널이 열려 있으면 최신 실제 전투 스탯으로 내용을 갱신합니다.
    public void RefreshIfOpen(CombatPowerSnapshot snapshot)
    {
        if (IsOpen) Refresh(snapshot);
    }

    // 계산 완료된 실제 최종 스탯을 읽기 쉬운 형식으로 표시합니다.
    private void Refresh(CombatPowerSnapshot snapshot)
    {
        if (detailsText == null) return;
        detailsText.text =
            $"좀비 능력치\n\n전투력  {snapshot.CombatPower.ToString("N0", CultureInfo.InvariantCulture)}\n\n" +
            $"공격력  {snapshot.Attack.ToString("N2", CultureInfo.InvariantCulture)}\n" +
            $"체력  {snapshot.MaxHealth.ToString("N2", CultureInfo.InvariantCulture)}\n" +
            $"방어력(초당 회복)  {snapshot.HealthRegen.ToString("N2", CultureInfo.InvariantCulture)}\n" +
            $"공격속도  {snapshot.AttackSpeed.ToString("N2", CultureInfo.InvariantCulture)}/s\n" +
            $"이동속도  {snapshot.MoveSpeed.ToString("N2", CultureInfo.InvariantCulture)}";
    }

    // 닫기 버튼 Listener를 중복 없이 연결합니다.
    private void RegisterCloseListener()
    {
        if (closeListenerRegistered || closeButton == null) return;
        closeButton.onClick.RemoveListener(Hide);
        closeButton.onClick.AddListener(Hide);
        closeListenerRegistered = true;
    }

    // 닫기 버튼 Listener를 안전하게 해제합니다.
    private void UnregisterCloseListener()
    {
        if (!closeListenerRegistered || closeButton == null) return;
        closeButton.onClick.RemoveListener(Hide);
        closeListenerRegistered = false;
    }
}
