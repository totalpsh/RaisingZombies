using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 공통 Top UI의 닉네임, 실제 재화, 설정 진입점을 표시합니다.
public sealed class TopUIController : MonoBehaviour
{
    private const string MissingNicknameText = "—"; // 닉네임 원본이 아직 없을 때 표시할 비데이터 문구

    [Header("플레이어 정보")]
    [SerializeField] private TMP_Text nicknameText; // 현재 플레이어 닉네임을 표시하는 텍스트

    [Header("재화")]
    [SerializeField] private TMP_Text currencyText; // 현재 보유 재화를 표시하는 텍스트

    [Header("설정")]
    [SerializeField] private Button settingsButton; // 설정 Placeholder를 여는 버튼
    [SerializeField] private GameObject settingsRoot; // 실제 설정 UI가 생길 때 교체할 Placeholder Root

    private UpgradeManager upgradeManager; // 실제 재화 원본과 변경 이벤트를 제공하는 기존 매니저
    private bool settingsListenerRegistered; // 설정 버튼 Listener 중복 등록 방지 상태

    // 기존 UpgradeManager를 재화 표시 원본으로 연결합니다.
    public void Initialize(UpgradeManager manager)
    {
        UnsubscribeCurrencyEvents();
        upgradeManager = manager;

        if (isActiveAndEnabled)
        {
            SubscribeCurrencyEvents();
        }

        SetActive(settingsRoot, false);
        EnsureNicknamePlaceholder();
        RefreshCurrency();
    }

    // 향후 실제 닉네임 시스템이 제공하는 값을 표시 전용으로 반영합니다.
    public void SetNickname(string nickname)
    {
        if (nicknameText != null)
        {
            nicknameText.text = string.IsNullOrWhiteSpace(nickname) ? MissingNicknameText : nickname;
        }
    }

    // 활성화될 때 설정 입력과 재화 변경 이벤트를 연결합니다.
    private void OnEnable()
    {
        RegisterSettingsListener();
        SubscribeCurrencyEvents();
        EnsureNicknamePlaceholder();
        RefreshCurrency();
    }

    // 비활성화될 때 설정 입력과 재화 변경 이벤트를 해제합니다.
    private void OnDisable()
    {
        UnregisterSettingsListener();
        UnsubscribeCurrencyEvents();
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

    // 재화 변경 시 Top UI 숫자를 즉시 갱신합니다.
    private void HandleCurrencyChanged()
    {
        RefreshCurrency();
    }

    // 기존 UpgradeManager의 상태 변경 이벤트를 중복 없이 구독합니다.
    private void SubscribeCurrencyEvents()
    {
        if (upgradeManager == null)
        {
            return;
        }

        upgradeManager.stateChanged -= HandleCurrencyChanged;
        upgradeManager.stateChanged += HandleCurrencyChanged;
    }

    // 기존 UpgradeManager의 상태 변경 이벤트 구독을 해제합니다.
    private void UnsubscribeCurrencyEvents()
    {
        if (upgradeManager != null)
        {
            upgradeManager.stateChanged -= HandleCurrencyChanged;
        }
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
