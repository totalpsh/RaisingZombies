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

    [Header("스테이지")]
    [SerializeField] private TMP_Text stageText; // 현재 진행 중인 스테이지 번호만 표시하는 텍스트

    [Header("전투력")]
    [SerializeField] private UnitData zombieData; // 실제 전투에서 사용하는 기본 좀비 Stat 원본
    [SerializeField] private CombatPowerBalanceSettings combatPowerBalance; // 전투력 환산 Weight와 기준값
    [SerializeField] private TMP_Text combatPowerText; // 현재 전체 전투력을 표시하는 텍스트
    [SerializeField] private Button combatInfoButton; // 상세 전투 스탯 패널을 전환하는 정보 버튼
    [SerializeField] private CombatStatInfoPanel combatInfoPanel; // 하단 메인 탭과 독립적으로 표시할 상세 전투 스탯 패널

    [Header("설정")]
    [SerializeField] private Button settingsButton; // 설정 Placeholder를 여는 버튼
    [SerializeField] private GameObject settingsRoot; // 실제 Settings Prefab을 포함하는 전체 화면 Root
    [SerializeField] private SettingsController settingsController; // Settings 닫기 요청과 열기 시 갱신을 담당하는 Controller

    private UpgradeManager upgradeManager; // 실제 재화 원본과 변경 이벤트를 제공하는 기존 매니저
    private BodyEquipmentManager bodyEquipmentManager; // 장착 변경과 실제 좀비 전투력 갱신 원본
    private StageManager stageManager; // 현재 씬에서 실제 Stage 진행값을 제공하는 매니저
    private CombatPowerSnapshot currentCombatPower; // 가장 최근 실제 최종 스탯으로 계산한 전투력 결과
    private bool settingsListenerRegistered; // 설정 버튼 Listener 중복 등록 방지 상태
    private bool combatInfoListenerRegistered; // 전투력 정보 버튼 Listener 중복 등록 방지 상태
    private bool settingsCloseEventRegistered; // Settings 닫기 이벤트 중복 구독 방지 상태

    // 기존 UpgradeManager를 재화와 전투력 표시 원본으로 연결합니다.
    public void Initialize(UpgradeManager manager)
    {
        UnsubscribeUpgradeEvents();
        upgradeManager = manager;

        if (isActiveAndEnabled)
        {
            SubscribeUpgradeEvents();
            SubscribeBodyEquipmentEvents();
            SubscribeStageEvents();
        }

        SetActive(settingsRoot, false);
        if (combatInfoPanel != null) combatInfoPanel.Hide();
        EnsureNicknamePlaceholder();
        RefreshCurrency();
        RefreshStage();
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
        RegisterSettingsCloseEvent();
        RegisterCombatInfoListener();
        SubscribeUpgradeEvents();
        SubscribeBodyEquipmentEvents();
        SubscribeStageEvents();
        EnsureNicknamePlaceholder();
        RefreshCurrency();
        RefreshStage();
        RefreshCombatPower();
    }

    // 비활성화될 때 버튼 입력과 Upgrade 상태 변경 이벤트를 해제합니다.
    private void OnDisable()
    {
        UnregisterSettingsListener();
        UnregisterSettingsCloseEvent();
        UnregisterCombatInfoListener();
        UnsubscribeUpgradeEvents();
        UnsubscribeBodyEquipmentEvents();
        UnsubscribeStageEvents();
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

    // 현재 StageManager의 실제 진행 번호를 숫자로만 표시합니다.
    private void RefreshStage()
    {
        if (stageText == null || stageManager == null) return;
        stageText.text = stageManager.CurrentStageNumber.ToString(CultureInfo.InvariantCulture);
    }

    // Stage 변경 이벤트가 전달한 번호를 다른 문구 없이 표시합니다.
    private void HandleStageChanged(int stageNumber)
    {
        if (stageText == null) return;
        stageText.text = stageNumber.ToString(CultureInfo.InvariantCulture);
    }

    // 씬 전환으로 활성 StageManager가 바뀌면 새 원본에 다시 연결합니다.
    private void HandleActiveStageManagerChanged(StageManager manager)
    {
        BindStageManager(manager);
    }

    // 활성 StageManager와 번호 변경 이벤트를 중복 없이 구독합니다.
    private void SubscribeStageEvents()
    {
        StageManager.ActiveInstanceChanged -= HandleActiveStageManagerChanged;
        StageManager.ActiveInstanceChanged += HandleActiveStageManagerChanged;
        BindStageManager(StageManager.ActiveInstance);
    }

    // 활성 StageManager와 번호 변경 이벤트 구독을 해제합니다.
    private void UnsubscribeStageEvents()
    {
        StageManager.ActiveInstanceChanged -= HandleActiveStageManagerChanged;
        BindStageManager(null);
    }

    // Stage 번호 원본을 교체하고 현재 값을 즉시 갱신합니다.
    private void BindStageManager(StageManager manager)
    {
        if (stageManager != null)
            stageManager.StageChanged -= HandleStageChanged;

        stageManager = manager;

        if (stageManager != null)
        {
            stageManager.StageChanged -= HandleStageChanged;
            stageManager.StageChanged += HandleStageChanged;
        }

        RefreshStage();
    }

    // 현재 실제 최종 좀비 스탯을 기준으로 전투력과 열린 정보 패널을 갱신합니다.
    private void RefreshCombatPower()
    {
        currentCombatPower = CombatPowerCalculator.Calculate(zombieData, bodyEquipmentManager, combatPowerBalance);
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

    // 신체 장비 Manager 생성과 장착 상태 변경을 전투력 갱신에 연결합니다.
    private void SubscribeBodyEquipmentEvents()
    {
        BodyEquipmentManager.AvailabilityChanged -= HandleBodyEquipmentAvailabilityChanged;
        BodyEquipmentManager.AvailabilityChanged += HandleBodyEquipmentAvailabilityChanged;
        BindBodyEquipmentManager(BodyEquipmentManager.HasInstance ? BodyEquipmentManager.Instance : null);
    }

    // 신체 장비 Manager의 정적 생성 알림과 상태 이벤트를 해제합니다.
    private void UnsubscribeBodyEquipmentEvents()
    {
        BodyEquipmentManager.AvailabilityChanged -= HandleBodyEquipmentAvailabilityChanged;
        BindBodyEquipmentManager(null);
    }

    // 장비 Manager가 재생성되면 실제 상태 이벤트 원본을 교체합니다.
    private void HandleBodyEquipmentAvailabilityChanged(BodyEquipmentManager manager)
    {
        BindBodyEquipmentManager(manager);
    }

    // 장비 상태 이벤트를 중복 없이 연결하고 현재 전투력을 즉시 갱신합니다.
    private void BindBodyEquipmentManager(BodyEquipmentManager manager)
    {
        if (bodyEquipmentManager != null) bodyEquipmentManager.EquippedStatsChanged -= RefreshCombatPower;
        bodyEquipmentManager = manager;
        if (bodyEquipmentManager != null)
        {
            bodyEquipmentManager.EquippedStatsChanged -= RefreshCombatPower;
            bodyEquipmentManager.EquippedStatsChanged += RefreshCombatPower;
        }
        RefreshCombatPower();
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

    // Settings Root의 표시 상태를 전환하고 열릴 때 실제 설정값을 다시 표시합니다.
    private void HandleSettingsClicked()
    {
        if (settingsRoot == null) return;
        bool open = !settingsRoot.activeSelf; // 버튼 입력 후 적용할 Settings 표시 상태
        settingsRoot.SetActive(open);
        if (open && settingsController != null) settingsController.RefreshUI();
    }

    // Settings 내부 X 버튼 요청으로 부모 Root만 닫아 현재 메인 탭을 유지합니다.
    private void HandleSettingsCloseRequested()
    {
        SetActive(settingsRoot, false);
    }

    // Settings Controller의 닫기 요청 이벤트를 한 번만 구독합니다.
    private void RegisterSettingsCloseEvent()
    {
        if (settingsCloseEventRegistered || settingsController == null) return;
        settingsController.CloseRequested += HandleSettingsCloseRequested;
        settingsCloseEventRegistered = true;
    }

    // Settings Controller의 닫기 요청 이벤트 구독을 해제합니다.
    private void UnregisterSettingsCloseEvent()
    {
        if (!settingsCloseEventRegistered || settingsController == null) return;
        settingsController.CloseRequested -= HandleSettingsCloseRequested;
        settingsCloseEventRegistered = false;
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
