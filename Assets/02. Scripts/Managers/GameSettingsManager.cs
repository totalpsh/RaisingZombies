using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

// 저장 가능한 게임 환경설정 값을 표현한다.
[Serializable]
public sealed class GameSettingsSaveData
{
    public bool bgmEnabled = true; // BGM 출력 활성 상태
    public bool sfxEnabled = true; // SFX와 UI 효과음 출력 활성 상태
    public bool hapticEnabled = true; // 선택적 진동 요청 허용 상태
    public string languageCode = "en"; // Unity Localization에 적용할 Locale 코드
}

// 환경설정의 실제 상태와 저장 및 시스템 적용을 한 곳에서 관리한다.
[DefaultExecutionOrder(-9900)]
public sealed class GameSettingsManager : Singleton<GameSettingsManager>, ISaveDataProvider
{
    private const string SettingsSaveKey = "GameSettings"; // 통합 저장에서 사용할 환경설정 구역 키
    private const string DefaultLanguageCode = "en"; // 저장값이 없을 때 사용할 프로젝트 기본 언어
    private GameSettingsSaveData settings = new(); // 저장 및 UI가 함께 참조하는 환경설정 원본
    private Coroutine localeRoutine; // Localization 초기화를 기다리는 현재 적용 작업
    private bool localeEventRegistered; // Locale 변경 이벤트 중복 구독 방지 상태

    public string SaveKey => SettingsSaveKey; // 통합 저장에서 사용할 환경설정 구역 키
    public Type SaveDataType => typeof(GameSettingsSaveData); // 저장 복원에 사용할 DTO 형식
    public bool BgmEnabled => settings.bgmEnabled; // 현재 저장 원본의 BGM 활성 상태
    public bool SfxEnabled => settings.sfxEnabled; // 현재 저장 원본의 SFX 활성 상태
    public bool HapticEnabled => settings.hapticEnabled; // 현재 저장 원본의 진동 활성 상태
    public string LanguageCode => settings.languageCode; // 현재 저장 원본의 Locale 코드
    public event Action SettingsChanged; // 환경설정 원본이 변경된 직후 발생하는 이벤트

    // 씬 배치 여부와 관계없이 환경설정 매니저 인스턴스를 보장한다.
    public static GameSettingsManager EnsureInstance()
    {
        GameSettingsManager existing = FindAnyObjectByType<GameSettingsManager>(); // 씬 또는 DontDestroy 영역의 기존 인스턴스
        if (existing != null) return existing;

        GameObject root = new GameObject(nameof(GameSettingsManager)); // 자동 생성할 환경설정 매니저 오브젝트
        return root.AddComponent<GameSettingsManager>();
    }

    // 첫 씬의 오디오와 UI가 준비되기 전에 저장된 환경설정을 복원한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        SaveManager.EnsureInstance();
        EnsureInstance();
    }

    // 저장 Provider를 등록하고 현재 환경설정을 실제 시스템에 적용한다.
    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        DontDestroyOnLoad(gameObject);
        RegisterLocaleEvent();
        SaveManager.EnsureInstance().RegisterProvider(this);
        ApplyAllSettings();
    }

    // 현재 저장 원본을 통합 저장 시스템에 전달한다.
    public object CaptureSaveData()
    {
        return new GameSettingsSaveData
        {
            bgmEnabled = settings.bgmEnabled,
            sfxEnabled = settings.sfxEnabled,
            hapticEnabled = settings.hapticEnabled,
            languageCode = settings.languageCode
        };
    }

    // 저장된 환경설정을 검증한 뒤 실제 시스템에 복원한다.
    public void RestoreSaveData(object data)
    {
        GameSettingsSaveData restored = data as GameSettingsSaveData; // 통합 저장에서 역직렬화된 환경설정
        settings = restored ?? CreateDefaultSettings();
        settings.languageCode = NormalizeLanguageCode(settings.languageCode);
        ApplyAllSettings();
        SettingsChanged?.Invoke();
    }

    // 환경설정을 새 게임 기본값으로 되돌리고 실제 시스템에 적용한다.
    public void ResetSaveData()
    {
        settings = CreateDefaultSettings();
        ApplyAllSettings();
        SettingsChanged?.Invoke();
    }

    // BGM 활성 상태를 변경하고 SoundManager 및 저장에 즉시 반영한다.
    public void SetBgmEnabled(bool enabled)
    {
        if (settings.bgmEnabled == enabled) return;
        settings.bgmEnabled = enabled;
        ApplySoundSettings();
        CommitChange();
    }

    // SFX와 UI 효과음 활성 상태를 함께 변경하고 저장한다.
    public void SetSfxEnabled(bool enabled)
    {
        if (settings.sfxEnabled == enabled) return;
        settings.sfxEnabled = enabled;
        ApplySoundSettings();
        CommitChange();
    }

    // 선택적 진동 요청의 허용 상태를 변경하고 저장한다.
    public void SetHapticEnabled(bool enabled)
    {
        if (settings.hapticEnabled == enabled) return;
        settings.hapticEnabled = enabled;
        CommitChange();
    }

    // 지원되는 Locale 코드를 저장하고 Unity Localization에 적용한다.
    public bool SetLanguage(string languageCode)
    {
        string normalizedCode = NormalizeLanguageCode(languageCode); // 한국어와 영어만 허용한 Locale 코드
        if (!IsSupportedLanguage(normalizedCode)) return false;

        bool changed = !string.Equals(settings.languageCode, normalizedCode, StringComparison.Ordinal); // 실제 저장값 변경 여부
        settings.languageCode = normalizedCode;
        ApplyLanguage();
        if (changed) CommitChange();
        return true;
    }

    // Haptic이 켜진 경우에만 모바일 진동 요청을 실행한다.
    public bool RequestHaptic()
    {
        if (!settings.hapticEnabled) return false;

#if UNITY_ANDROID || UNITY_IOS
        Handheld.Vibrate();
        return true;
#else
        return false;
#endif
    }

    // 저장 복원 이후 생성된 SoundManager에 현재 설정을 다시 적용한다.
    public void ApplyToSoundManager(SoundManager soundManager)
    {
        if (soundManager == null) return;
        soundManager.SetBGMEnabled(settings.bgmEnabled);
        soundManager.SetSFXEnabled(settings.sfxEnabled);
    }

    // 등록한 이벤트와 저장 Provider를 안전하게 해제한다.
    protected override void OnDestroy()
    {
        UnregisterLocaleEvent();
        if (SaveManager.HasInstance) SaveManager.Instance.UnregisterProvider(this);
        base.OnDestroy();
    }

    // 현재 환경설정을 오디오와 Localization 시스템에 모두 적용한다.
    private void ApplyAllSettings()
    {
        ApplySoundSettings();
        ApplyLanguage();
    }

    // SoundManager가 준비된 경우 현재 BGM과 SFX 상태를 적용한다.
    private void ApplySoundSettings()
    {
        if (SoundManager.HasInstance) ApplyToSoundManager(SoundManager.Instance);
    }

    // Localization 초기화 완료 후 저장된 Locale을 적용한다.
    private void ApplyLanguage()
    {
        if (localeRoutine != null) StopCoroutine(localeRoutine);
        localeRoutine = StartCoroutine(ApplyLanguageWhenReady(settings.languageCode));
    }

    // Localization 데이터가 준비될 때까지 기다린 뒤 실제 Locale을 변경한다.
    private IEnumerator ApplyLanguageWhenReady(string languageCode)
    {
        yield return LocalizationSettings.InitializationOperation;

        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(languageCode)); // 저장 코드에 대응하는 실제 Locale
        if (locale == null)
        {
            Debug.LogWarning($"[GameSettingsManager] 지원 Locale을 찾지 못했습니다: {languageCode}");
            localeRoutine = null;
            yield break;
        }

        if (LocalizationSettings.SelectedLocale != locale) LocalizationSettings.SelectedLocale = locale;
        localeRoutine = null;
    }

    // 다른 시스템에서 Locale을 변경해도 저장 원본과 UI가 같은 값을 사용하게 동기화한다.
    private void HandleSelectedLocaleChanged(Locale locale)
    {
        if (locale == null) return;
        string languageCode = NormalizeLanguageCode(locale.Identifier.Code); // 실제 선택된 Locale의 저장용 코드
        if (!IsSupportedLanguage(languageCode) || string.Equals(settings.languageCode, languageCode, StringComparison.Ordinal)) return;

        settings.languageCode = languageCode;
        CommitChange();
    }

    // Unity Localization의 Locale 변경 이벤트를 한 번만 구독한다.
    private void RegisterLocaleEvent()
    {
        if (localeEventRegistered) return;
        LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;
        localeEventRegistered = true;
    }

    // Unity Localization의 Locale 변경 이벤트 구독을 해제한다.
    private void UnregisterLocaleEvent()
    {
        if (!localeEventRegistered) return;
        LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
        localeEventRegistered = false;
    }

    // 변경 이벤트를 알리고 기존 통합 저장 파일에 즉시 기록한다.
    private void CommitChange()
    {
        SettingsChanged?.Invoke();
        SaveManager saveManager = SaveManager.EnsureInstance(); // 환경설정 변경을 기록할 기존 통합 저장 매니저
        saveManager.MarkDirty();
        saveManager.SaveGame();
    }

    // 프로젝트 기본값으로 사용할 새 환경설정 데이터를 만든다.
    private static GameSettingsSaveData CreateDefaultSettings()
    {
        return new GameSettingsSaveData
        {
            bgmEnabled = true,
            sfxEnabled = true,
            hapticEnabled = true,
            languageCode = DefaultLanguageCode
        };
    }

    // Locale 코드를 한국어 또는 영어의 짧은 코드로 정규화한다.
    private static string NormalizeLanguageCode(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode)) return DefaultLanguageCode;
        if (languageCode.StartsWith("ko", StringComparison.OrdinalIgnoreCase)) return "ko";
        if (languageCode.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return "en";
        return languageCode;
    }

    // 현재 프로젝트에서 실제 제공하는 한국어와 영어인지 확인한다.
    private static bool IsSupportedLanguage(string languageCode)
    {
        return string.Equals(languageCode, "ko", StringComparison.Ordinal) ||
               string.Equals(languageCode, "en", StringComparison.Ordinal);
    }
}
