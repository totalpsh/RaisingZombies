using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 기존 Settings Prefab의 표시와 사용자 입력을 실제 프로젝트 시스템에 연결한다.
public sealed class SettingsController : MonoBehaviour
{
    [Header("사운드와 진동")]
    [SerializeField] private Button bgmButton; // BGM 설정을 반대로 변경하는 기존 스위치 버튼
    [SerializeField] private GameObject bgmOnObject; // BGM 활성 상태를 표시하는 기존 On 오브젝트
    [SerializeField] private GameObject bgmOffObject; // BGM 비활성 상태를 표시하는 기존 Off 오브젝트
    [SerializeField] private Button sfxButton; // SFX와 UI 효과음 설정을 반대로 변경하는 기존 스위치 버튼
    [SerializeField] private GameObject sfxOnObject; // SFX 활성 상태를 표시하는 기존 On 오브젝트
    [SerializeField] private GameObject sfxOffObject; // SFX 비활성 상태를 표시하는 기존 Off 오브젝트
    [SerializeField] private Button hapticButton; // 선택적 진동 설정을 반대로 변경하는 기존 스위치 버튼
    [SerializeField] private GameObject hapticOnObject; // 진동 활성 상태를 표시하는 기존 On 오브젝트
    [SerializeField] private GameObject hapticOffObject; // 진동 비활성 상태를 표시하는 기존 Off 오브젝트

    [Header("언어")]
    [SerializeField] private Button languageButton; // UIManager 언어 선택 팝업을 여는 기존 버튼
    [SerializeField] private TMP_Text languageText; // 현재 실제 Locale을 표시하는 기존 텍스트

    [Header("외부 기능")]
    [SerializeField] private Button rateButton; // 플랫폼별 앱 평가 페이지를 여는 기존 버튼
    [SerializeField] private Button signInButton; // 실제 Auth 시스템 연결 전까지 비활성으로 유지할 기존 버튼
    [SerializeField] private Button supportButton; // 설정된 지원 URL 또는 이메일을 여는 기존 버튼
    [SerializeField] private Button accountDeleteButton; // 계정 삭제 가능 여부를 확인 팝업으로 안내하는 기존 버튼
    [SerializeField] private string androidStoreId = string.Empty; // Android 스토어에 등록된 실제 패키지 ID
    [SerializeField] private string iosAppId = string.Empty; // App Store에 등록된 실제 숫자 앱 ID
    [SerializeField] private string supportUrl = string.Empty; // 실제 고객지원 웹 페이지 URL
    [SerializeField] private string supportEmail = string.Empty; // 웹 페이지가 없을 때 사용할 실제 고객지원 이메일

    [Header("하단 정보")]
    [SerializeField] private Button playerIdCopyButton; // 실제 Save ID를 Clipboard에 복사하는 기존 UID 영역 버튼
    [SerializeField] private TMP_Text playerIdText; // 실제 Save ID를 표시하는 기존 텍스트
    [SerializeField] private TMP_Text versionText; // Application.version을 표시하는 기존 텍스트
    [SerializeField] private Button privacyButton; // 실제 개인정보 처리방침 URL을 여는 기존 텍스트 버튼
    [SerializeField] private Button termsButton; // 실제 이용약관 URL을 여는 기존 텍스트 버튼
    [SerializeField] private string privacyUrl = string.Empty; // 실제 개인정보 처리방침 URL
    [SerializeField] private string termsUrl = string.Empty; // 실제 이용약관 URL
    [SerializeField] private Button closeButton; // Settings 화면을 닫는 기존 X 버튼

    private GameSettingsManager settingsManager; // 저장 원본과 실제 시스템 적용을 제공하는 환경설정 매니저
    private bool listenersRegistered; // 버튼 Listener 중복 등록 방지 상태
    private bool settingsEventRegistered; // 설정 변경 이벤트 중복 구독 방지 상태
    public event Action CloseRequested; // TopUI가 Settings 부모 Root를 닫도록 요청하는 이벤트

    // 활성화될 때 실제 저장 상태를 읽고 버튼 및 이벤트를 연결한다.
    private void OnEnable()
    {
        settingsManager = GameSettingsManager.EnsureInstance();
        RegisterListeners();
        RegisterSettingsEvent();
        RefreshUI();
    }

    // 비활성화될 때 버튼과 설정 변경 이벤트 구독을 해제한다.
    private void OnDisable()
    {
        UnregisterListeners();
        UnregisterSettingsEvent();
    }

    // 현재 실제 설정과 프로젝트 정보를 기존 Prefab 오브젝트에 표시한다.
    public void RefreshUI()
    {
        if (settingsManager == null) settingsManager = GameSettingsManager.EnsureInstance();
        SetToggleObjects(bgmOnObject, bgmOffObject, settingsManager.BgmEnabled);
        SetToggleObjects(sfxOnObject, sfxOffObject, settingsManager.SfxEnabled);
        SetToggleObjects(hapticOnObject, hapticOffObject, settingsManager.HapticEnabled);

        if (languageText != null) languageText.text = GetLanguageDisplayName(settingsManager.LanguageCode);

        SaveManager saveManager = SaveManager.EnsureInstance(); // Player ID의 실제 원본을 제공하는 통합 저장 매니저
        if (playerIdText != null) playerIdText.text = $"<color=#BEB2AD>Player ID</color> #{saveManager.CurrentSaveId}";
        if (versionText != null) versionText.text = $"Version {Application.version}";

        if (signInButton != null) signInButton.interactable = false;
        if (rateButton != null) rateButton.interactable = HasRateConfiguration();
        if (supportButton != null) supportButton.interactable = HasSupportConfiguration();
        if (privacyButton != null) privacyButton.interactable = IsWebUrl(privacyUrl);
        if (termsButton != null) termsButton.interactable = IsWebUrl(termsUrl);
    }

    // 현재 BGM 설정을 반대로 변경한다.
    private void ToggleBgm()
    {
        settingsManager.SetBgmEnabled(!settingsManager.BgmEnabled);
    }

    // 현재 SFX와 UI 효과음 설정을 반대로 변경한다.
    private void ToggleSfx()
    {
        settingsManager.SetSfxEnabled(!settingsManager.SfxEnabled);
    }

    // 현재 진동 설정을 반대로 변경하고 켜진 경우 한 번 요청한다.
    private void ToggleHaptic()
    {
        bool enabled = !settingsManager.HapticEnabled; // 버튼 입력 후 적용할 새 진동 상태
        settingsManager.SetHapticEnabled(enabled);
        if (enabled) settingsManager.RequestHaptic();
    }

    // UIManager의 기존 PopUp 레이어에 언어 선택 팝업을 연다.
    private async void OpenLanguagePopup()
    {
        if (!UIManager.HasInstance)
        {
            Debug.LogWarning("[SettingsController] UIManager가 없어 언어 선택 팝업을 열 수 없습니다.", this);
            return;
        }

        SettingsLanguagePopupParameters parameters = new SettingsLanguagePopupParameters // 현재 언어와 선택 결과를 전달할 팝업 입력값
        {
            currentLanguageCode = settingsManager.LanguageCode,
            languageSelected = SelectLanguage
        };
        await UIManager.Instance.OpenUI<SettingsLanguagePopup>(parameters, UILayer.PopUp);
    }

    // 언어 팝업에서 선택한 Locale을 실제 Localization에 적용한다.
    private void SelectLanguage(string languageCode)
    {
        settingsManager.SetLanguage(languageCode);
    }

    // 현재 플랫폼에 맞는 실제 Store 설정이 있을 때만 평가 페이지를 연다.
    private void OpenRatePage()
    {
#if UNITY_EDITOR
        Debug.Log("[SettingsController] Editor에서는 Store 평가 페이지를 열지 않습니다.", this);
#elif UNITY_ANDROID
        if (!string.IsNullOrWhiteSpace(androidStoreId)) Application.OpenURL($"market://details?id={androidStoreId}");
#elif UNITY_IOS
        if (!string.IsNullOrWhiteSpace(iosAppId)) Application.OpenURL($"https://apps.apple.com/app/id{iosAppId}?action=write-review");
#else
        Debug.LogWarning("[SettingsController] 현재 플랫폼의 Store 평가 경로가 설정되지 않았습니다.", this);
#endif
    }

    // 고객지원 URL을 우선 사용하고 없으면 설정된 이메일 작성 화면을 연다.
    private void OpenSupport()
    {
        if (IsWebUrl(supportUrl))
        {
            Application.OpenURL(supportUrl);
            return;
        }

        if (!string.IsNullOrWhiteSpace(supportEmail)) Application.OpenURL($"mailto:{supportEmail}");
    }

    // 실제 Auth 시스템이 없음을 가짜 로그인 처리 없이 알린다.
    private void HandleSignIn()
    {
        Debug.LogWarning("[SettingsController] 연결된 Auth 시스템이 없어 Sign In을 실행하지 않습니다.", this);
    }

    // 계정 시스템 부재를 설명하는 확인 팝업을 실제 삭제 없이 연다.
    private async void OpenAccountDeleteConfirmation()
    {
        if (!UIManager.HasInstance)
        {
            Debug.LogWarning("[SettingsController] UIManager가 없어 계정 삭제 안내 팝업을 열 수 없습니다.", this);
            return;
        }

        SettingsConfirmationPopupParameters parameters = new SettingsConfirmationPopupParameters // 실제 삭제 콜백을 주지 않는 안전한 안내 정보
        {
            title = "Account Delete",
            message = "연결된 계정/Auth 시스템이 없어 실제 계정 삭제를 실행할 수 없습니다. 로컬 저장 초기화와 계정 삭제는 서로 다른 기능입니다.",
            confirmLabel = "삭제 불가",
            cancelLabel = "닫기",
            confirmed = null
        };
        await UIManager.Instance.OpenUI<SettingsConfirmationPopup>(parameters, UILayer.PopUp);
    }

    // 현재 실제 Save ID를 운영체제 Clipboard에 복사한다.
    private void CopyPlayerId()
    {
        string playerId = SaveManager.EnsureInstance().CurrentSaveId; // Clipboard에 넣을 실제 저장 프로필 ID
        if (!string.IsNullOrWhiteSpace(playerId)) GUIUtility.systemCopyBuffer = playerId;
    }

    // 설정된 실제 개인정보 처리방침 URL을 연다.
    private void OpenPrivacy()
    {
        OpenWebUrl(privacyUrl, "Privacy");
    }

    // 설정된 실제 이용약관 URL을 연다.
    private void OpenTerms()
    {
        OpenWebUrl(termsUrl, "Terms of Service");
    }

    // TopUI에 부모 Settings Root 닫기를 요청한다.
    private void CloseSettings()
    {
        if (CloseRequested != null)
        {
            CloseRequested.Invoke();
            return;
        }

        gameObject.SetActive(false);
    }

    // 실제 설정 원본이 변경되면 매 프레임 검사 없이 UI를 갱신한다.
    private void HandleSettingsChanged()
    {
        RefreshUI();
    }

    // 모든 기존 버튼에 기능 Listener를 한 번만 등록한다.
    private void RegisterListeners()
    {
        if (listenersRegistered) return;
        AddListener(bgmButton, ToggleBgm);
        AddListener(sfxButton, ToggleSfx);
        AddListener(hapticButton, ToggleHaptic);
        AddListener(languageButton, OpenLanguagePopup);
        AddListener(rateButton, OpenRatePage);
        AddListener(signInButton, HandleSignIn);
        AddListener(supportButton, OpenSupport);
        AddListener(accountDeleteButton, OpenAccountDeleteConfirmation);
        AddListener(playerIdCopyButton, CopyPlayerId);
        AddListener(privacyButton, OpenPrivacy);
        AddListener(termsButton, OpenTerms);
        AddListener(closeButton, CloseSettings);
        listenersRegistered = true;
    }

    // 모든 기존 버튼의 기능 Listener를 안전하게 해제한다.
    private void UnregisterListeners()
    {
        if (!listenersRegistered) return;
        RemoveListener(bgmButton, ToggleBgm);
        RemoveListener(sfxButton, ToggleSfx);
        RemoveListener(hapticButton, ToggleHaptic);
        RemoveListener(languageButton, OpenLanguagePopup);
        RemoveListener(rateButton, OpenRatePage);
        RemoveListener(signInButton, HandleSignIn);
        RemoveListener(supportButton, OpenSupport);
        RemoveListener(accountDeleteButton, OpenAccountDeleteConfirmation);
        RemoveListener(playerIdCopyButton, CopyPlayerId);
        RemoveListener(privacyButton, OpenPrivacy);
        RemoveListener(termsButton, OpenTerms);
        RemoveListener(closeButton, CloseSettings);
        listenersRegistered = false;
    }

    // 환경설정 원본의 변경 이벤트를 한 번만 구독한다.
    private void RegisterSettingsEvent()
    {
        if (settingsEventRegistered || settingsManager == null) return;
        settingsManager.SettingsChanged += HandleSettingsChanged;
        settingsEventRegistered = true;
    }

    // 환경설정 원본의 변경 이벤트 구독을 해제한다.
    private void UnregisterSettingsEvent()
    {
        if (!settingsEventRegistered || settingsManager == null) return;
        settingsManager.SettingsChanged -= HandleSettingsChanged;
        settingsEventRegistered = false;
    }

    // 기존 On과 Off 오브젝트가 실제 활성 상태와 반대로 표시되지 않게 갱신한다.
    private static void SetToggleObjects(GameObject onObject, GameObject offObject, bool enabled)
    {
        if (onObject != null) onObject.SetActive(enabled);
        if (offObject != null) offObject.SetActive(!enabled);
    }

    // 저장된 Locale 코드를 Settings 행에 표시할 이름으로 바꾼다.
    private static string GetLanguageDisplayName(string languageCode)
    {
        return string.Equals(languageCode, "ko", StringComparison.Ordinal) ? "한국어" : "English";
    }

    // 현재 빌드 플랫폼에 필요한 Store 식별자가 입력되었는지 확인한다.
    private bool HasRateConfiguration()
    {
#if UNITY_ANDROID
        return !string.IsNullOrWhiteSpace(androidStoreId);
#elif UNITY_IOS
        return !string.IsNullOrWhiteSpace(iosAppId);
#else
        return !string.IsNullOrWhiteSpace(androidStoreId) || !string.IsNullOrWhiteSpace(iosAppId);
#endif
    }

    // 고객지원 웹 주소 또는 이메일 중 하나가 입력되었는지 확인한다.
    private bool HasSupportConfiguration()
    {
        return IsWebUrl(supportUrl) || !string.IsNullOrWhiteSpace(supportEmail);
    }

    // 문자열이 실제 HTTP 또는 HTTPS 절대 주소인지 확인한다.
    private static bool IsWebUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri parsedUri)) return false; // 검증된 절대 URI
        return parsedUri.Scheme == Uri.UriSchemeHttp || parsedUri.Scheme == Uri.UriSchemeHttps;
    }

    // 검증된 외부 웹 주소만 열고 누락된 설정을 Console에 알린다.
    private void OpenWebUrl(string url, string label)
    {
        if (!IsWebUrl(url))
        {
            Debug.LogWarning($"[SettingsController] {label} URL이 설정되지 않았습니다.", this);
            return;
        }

        Application.OpenURL(url);
    }

    // 버튼이 존재할 때 지정한 클릭 함수를 등록한다.
    private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    // 버튼이 존재할 때 지정한 클릭 함수를 해제한다.
    private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button != null) button.onClick.RemoveListener(action);
    }
}
