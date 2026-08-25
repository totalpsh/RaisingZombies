using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 공통 Top UI의 닉네임, 전투력, 실제 재화, 설정 진입점을 표시합니다.
public sealed class TopUIController : MonoBehaviour
{
    private const string MissingNicknameText = "—"; // 닉네임 원본이 아직 없을 때 표시할 비데이터 문구

    [Header("플레이어 정보")]
    [SerializeField] private TMP_Text nicknameText; // 현재 플레이어 닉네임을 표시하는 텍스트

    [Header("재화")]
    [SerializeField] private TMP_Text currencyText; // 현재 보유 재화를 표시하는 텍스트

    [Header("전투력")]
    [SerializeField] private UnitData zombieData; // 실제 전투에서 사용하는 기본 좀비 Stat 원본
    [SerializeField] private CombatPowerBalanceSettings combatPowerBalance; // 전투력 환산 Weight와 기준값
    [SerializeField] private TMP_Text combatPowerText; // 현재 전체 전투력을 표시하는 텍스트
    [SerializeField] private Button combatInfoButton; // 상세 전투 스탯 패널을 전환하는 정보 버튼
    [SerializeField] private CombatStatInfoPanel combatInfoPanel; // 하단 메인 탭과 독립적으로 표시할 상세 전투 스탯 패널

    [Header("설정")]
    [SerializeField] private Button settingsButton; // 설정 Placeholder를 여는 버튼
    [SerializeField] private GameObject settingsRoot; // 실제 설정 UI가 생길 때 교체할 Placeholder Root

    private UpgradeManager upgradeManager; // 실제 재화 원본과 변경 이벤트를 제공하는 기존 매니저
    private CombatPowerSnapshot currentCombatPower; // 가장 최근 실제 최종 스탯으로 계산한 전투력 결과
    private bool settingsListenerRegistered; // 설정 버튼 Listener 중복 등록 방지 상태
    private bool combatInfoListenerRegistered; // 전투력 정보 버튼 Listener 중복 등록 방지 상태

    // 기존 UpgradeManager를 재화와 전투력 표시 원본으로 연결합니다.
    public void Initialize(UpgradeManager manager)
    {
        UnsubscribeUpgradeEvents();
        upgradeManager = manager;

        if (isActiveAndEnabled)
        {
            SubscribeUpgradeEvents();
        }

        SetActive(settingsRoot, false);
        if (combatInfoPanel != null) combatInfoPanel.Hide();
        EnsureNicknamePlaceholder();
        RefreshCurrency();
        RefreshCombatPower();
    }

    // 향후 실제 닉네임 시스템이 제공하는 값을 표시 전용으로 반영합니다.
    public void SetNickname(string nickname)
    {
        if (nicknameText != null)
        {
            nicknameText.text = string.IsNullOrWhiteSpace(nickname) ? MissingNicknameText : nickname;
        }
    }

    // 활성화될 때 버튼 입력과 Upgrade 상태 변경 이벤트를 연결합니다.
    private void OnEnable()
    {
        RegisterSettingsListener();
        RegisterCombatInfoListener();
        SubscribeUpgradeEvents();
        EnsureNicknamePlaceholder();
        RefreshCurrency();
        RefreshCombatPower();
    }

    // 비활성화될 때 버튼 입력과 Upgrade 상태 변경 이벤트를 해제합니다.
    private void OnDisable()
    {
        UnregisterSettingsListener();
        UnregisterCombatInfoListener();
        UnsubscribeUpgradeEvents();
    }

    // 닉네임 원본이 연결되지 않은 상태를 가짜 이름 없이 표시합니다.
    private void EnsureNicknamePlaceholder()
    {
        if (nicknameText != null && string.IsNullOrWhiteSpace(nicknameText.text))
        {
            nicknameText.text = MissingNicknameText;
        }
    }

    // UpgradeManager의 실제 현재 재화를 천 단위 구분 형식으로 표시합니다.
    private void RefreshCurrency()
    {
        if (currencyText != null)
        {
            int currency = upgradeManager == null ? 0 : upgradeManager.Currency; // 기존 UpgradeState에서 읽은 실제 현재 재화
            currencyText.text = currency.ToString("N0", CultureInfo.InvariantCulture);
        }
    }

    // 실제 Upgrade 상태가 바뀌면 재화와 전투력을 한 번씩 즉시 갱신합니다.
    private void HandleUpgradeStateChanged()
    {
        RefreshCurrency();
        RefreshCombatPower();
    }

    // 현재 실제 최종 좀비 스탯을 기준으로 전투력과 열린 정보 패널을 갱신합니다.
    private void RefreshCombatPower()
    {
        currentCombatPower = CombatPowerCalculator.Calculate(zombieData, upgradeManager, combatPowerBalance);
        if (combatPowerText != null)
        {
            string formattedPower = currentCombatPower.CombatPower.ToString("N0", CultureInfo.InvariantCulture); // 천 단위 구분 기호를 적용한 최종 전투력
            combatPowerText.text = $"전투력 {formattedPower}";
        }

        if (combatInfoPanel != null) combatInfoPanel.RefreshIfOpen(currentCombatPower);
    }

    // 기존 UpgradeManager의 상태 변경 이벤트를 중복 없이 구독합니다.
    private void SubscribeUpgradeEvents()
    {
        if (upgradeManager == null)
        {
            return;
        }

        upgradeManager.stateChanged -= HandleUpgradeStateChanged;
        upgradeManager.stateChanged += HandleUpgradeStateChanged;
    }

    // 기존 UpgradeManager의 상태 변경 이벤트 구독을 해제합니다.
    private void UnsubscribeUpgradeEvents()
    {
        if (upgradeManager != null)
        {
            upgradeManager.stateChanged -= HandleUpgradeStateChanged;
        }
    }

    // 전투력 정보 버튼으로 상세 패널의 표시 상태를 전환합니다.
    private void HandleCombatInfoClicked()
    {
        if (combatInfoPanel == null) return;
        if (combatInfoPanel.IsOpen)
        {
            combatInfoPanel.Hide();
            return;
        }

        RefreshCombatPower();
        combatInfoPanel.Show(currentCombatPower);
    }

    // 전투력 정보 버튼 Listener를 한 번만 등록합니다.
    private void RegisterCombatInfoListener()
    {
        if (combatInfoListenerRegistered || combatInfoButton == null) return;
        combatInfoButton.onClick.RemoveListener(HandleCombatInfoClicked);
        combatInfoButton.onClick.AddListener(HandleCombatInfoClicked);
        combatInfoListenerRegistered = true;
    }

    // 전투력 정보 버튼 Listener를 안전하게 해제합니다.
    private void UnregisterCombatInfoListener()
    {
        if (!combatInfoListenerRegistered || combatInfoButton == null) return;
        combatInfoButton.onClick.RemoveListener(HandleCombatInfoClicked);
        combatInfoListenerRegistered = false;
    }

    // 설정 버튼 Listener를 한 번만 등록합니다.
    private void RegisterSettingsListener()
    {
        if (settingsListenerRegistered || settingsButton == null)
        {
            return;
        }

        settingsButton.onClick.RemoveListener(HandleSettingsClicked);
        settingsButton.onClick.AddListener(HandleSettingsClicked);
        settingsListenerRegistered = true;
    }

    // 설정 버튼 Listener를 해제합니다.
    private void UnregisterSettingsListener()
    {
        if (!settingsListenerRegistered || settingsButton == null)
        {
            return;
        }

        settingsButton.onClick.RemoveListener(HandleSettingsClicked);
        settingsListenerRegistered = false;
    }

    // 기존 설정 UI가 없으므로 Placeholder Root의 표시 상태를 전환합니다.
    private void HandleSettingsClicked()
    {
        if (settingsRoot != null)
        {
            settingsRoot.SetActive(!settingsRoot.activeSelf);
        }
    }

    // 오브젝트가 존재할 때 활성 상태를 변경합니다.
    private static void SetActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }
}
