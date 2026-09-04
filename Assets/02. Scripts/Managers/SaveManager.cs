using System;
using System.Collections.Generic;
using UnityEngine;

// Save Provider 등록과 전체 저장·복원 순서를 관리하는 통합 매니저입니다.
[DefaultExecutionOrder(-10000)]
public sealed class SaveManager : Singleton<SaveManager>
{
    private const float AutoSaveInterval = 30f; // Dirty 데이터를 디스크에 반영할 자동 저장 주기
    private readonly Dictionary<string, ISaveDataProvider> _providers = new(StringComparer.Ordinal); // 키별 등록 Provider
    private SaveFileService _fileService; // 실제 JSON 파일 I/O 서비스
    private GameSaveData _saveData; // 메모리에 올라온 전체 저장 데이터
    private bool _isDirty; // 디스크보다 런타임 데이터가 최신인지 여부
    private bool _loadedFromBackup; // 정상 백업을 보존해야 하는 복구 직후 상태
    private bool _startupCompleted; // 모든 씬 Awake 이후 초기 저장이 가능한지 여부
    private bool _isShuttingDown; // 종료 중 중복 생성과 등록을 막는 상태
    private bool _isSaving; // SaveGame 재진입을 차단하는 상태
    private float _autoSaveTimer; // Dirty 상태에서 누적된 자동 저장 대기 시간
    private int _lastLifecycleSaveFrame = -1; // 같은 프레임의 Pause와 Focus 중복 저장을 막는 프레임

    public event Action SaveLoaded; // 전체 로드와 Provider 복원 완료 이벤트
    public event Action SaveReset; // 전체 초기화 완료 이벤트
    public string CurrentSaveId => _saveData == null ? string.Empty : _saveData.saveId; // 고객지원과 프로필 식별에 사용할 실제 저장 ID

    // 씬 배치 여부와 관계없이 SaveManager 인스턴스를 보장합니다.
    public static SaveManager EnsureInstance()
    {
        SaveManager existing = FindAnyObjectByType<SaveManager>(); // 씬 또는 DontDestroy 영역의 기존 인스턴스
        if (existing != null) return existing;

        GameObject root = new GameObject(nameof(SaveManager)); // 자동 생성할 저장 매니저 오브젝트
        return root.AddComponent<SaveManager>();
    }

    // 첫 씬의 다른 Awake보다 먼저 저장 파일을 메모리에 올립니다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    // 파일 서비스를 준비하고 메인 또는 백업 저장을 읽습니다.
    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        DontDestroyOnLoad(gameObject);
        _fileService = new SaveFileService();
        LoadFileIntoMemory();
    }

    // 모든 초기 Provider가 등록된 뒤 필요한 기본 저장을 한 번 기록합니다.
    private void Start()
    {
        _startupCompleted = true;
        if (_isDirty) SaveGame();
    }

    // Dirty 상태일 때만 unscaled 시간 기준으로 주기 저장을 시도합니다.
    private void Update()
    {
        if (!_isDirty)
        {
            _autoSaveTimer = 0f;
            return;
        }

        if (_isSaving) return;
        _autoSaveTimer += Time.unscaledDeltaTime;
        if (_autoSaveTimer < AutoSaveInterval) return;
        _autoSaveTimer = 0f;
        SaveGame();
    }

    // 앱이 백그라운드로 갈 때 변경된 데이터만 저장합니다.
    private void OnApplicationPause(bool paused)
    {
        if (paused) TryLifecycleSave();
    }

    // 앱 포커스를 잃을 때 Provider 상태를 갱신하고 변경 데이터를 저장합니다.
    private void OnApplicationFocus(bool focused)
    {
        if (!focused) TryLifecycleSave();
    }

    // 앱 종료 직전에 변경된 데이터만 저장합니다.
    private void OnApplicationQuit()
    {
        TryLifecycleSave();
        _isShuttingDown = true;
    }

    // Provider를 키로 등록하고 해당 구역 또는 기본값을 즉시 적용합니다.
    public bool RegisterProvider(ISaveDataProvider provider, bool keepCurrentDataWhenMissing = false)
    {
        return RegisterProvider(provider, keepCurrentDataWhenMissing, out _);
    }

    // Provider 등록과 함께 기존 저장 구역의 실제 복원 성공 여부를 반환합니다.
    public bool RegisterProvider(ISaveDataProvider provider, bool keepCurrentDataWhenMissing, out bool restoredFromSave)
    {
        restoredFromSave = false;
        if (_isShuttingDown || provider == null || string.IsNullOrWhiteSpace(provider.SaveKey)) return false;
        if (_providers.TryGetValue(provider.SaveKey, out ISaveDataProvider existing) && !ReferenceEquals(existing, provider))
        {
            Debug.LogError($"[Save] 중복 Provider 키입니다: {provider.SaveKey}");
            return false;
        }

        _providers[provider.SaveKey] = provider;
        restoredFromSave = RestoreProvider(provider);
        if (!restoredFromSave)
        {
            if (!keepCurrentDataWhenMissing && !TryResetProvider(provider)) return false;
            if (!keepCurrentDataWhenMissing)
            {
                _isDirty = true;
                Debug.Log($"[Save] Provider 기본값 적용: {provider.SaveKey}");
            }
            else
            {
                Debug.Log($"[Save] Provider 현재값 유지: {provider.SaveKey}");
            }
        }

        if (_startupCompleted && _isDirty && !keepCurrentDataWhenMissing) SaveGame();
        return true;
    }

    // 제거되는 시스템의 Provider 등록만 해제하고 저장 구역은 보존합니다.
    public void UnregisterProvider(ISaveDataProvider provider)
    {
        if (provider == null) return;
        if (_providers.TryGetValue(provider.SaveKey, out ISaveDataProvider existing) && ReferenceEquals(existing, provider))
            _providers.Remove(provider.SaveKey);
    }

    // 특정 Provider 키의 데이터가 현재 저장에 존재하는지 확인합니다.
    public bool HasProviderData(string saveKey)
    {
        return _saveData != null && _saveData.TryGetSection(saveKey, out _);
    }

    // 런타임 데이터가 바뀌었음을 표시하되 파일 I/O는 실행하지 않습니다.
    public void MarkDirty()
    {
        _isDirty = true;
    }

    // 모든 Provider의 현재 원본 데이터를 모아 하나의 JSON 파일로 저장합니다.
    public bool SaveGame()
    {
        return SaveGameInternal(true);
    }

    // 필요할 때만 Provider 준비를 실행하고 현재 전체 데이터를 디스크에 저장합니다.
    private bool SaveGameInternal(bool prepareProviders)
    {
        if (_fileService == null || _saveData == null || _isSaving) return false;

        _isSaving = true;
        try
        {
            if (prepareProviders)
            {
                bool providerDataChanged; // 저장 준비 과정에서 Provider 원본이 변경되었는지 여부
                if (!TryPrepareProvidersForSave(out providerDataChanged)) return false;
                if (providerDataChanged) _isDirty = true;
            }

            foreach (ISaveDataProvider provider in _providers.Values) // 저장할 등록 Provider
            {
                try
                {
                    object providerData = provider.CaptureSaveData(); // Provider가 제공한 직렬화 DTO
                    if (providerData == null) throw new InvalidOperationException("저장 DTO가 null입니다.");
                    string providerJson = JsonUtility.ToJson(providerData); // Provider DTO를 변환한 저장 JSON
                    if (string.IsNullOrWhiteSpace(providerJson)) throw new InvalidOperationException("직렬화된 저장 JSON이 비어 있습니다.");
                    _saveData.SetSection(provider.SaveKey, providerJson);
                }
                catch (Exception exception)
                {
                    Debug.LogError($"[Save] Provider 캡처 실패 ({provider.SaveKey}): {exception.Message}");
                    return false;
                }
            }

            _saveData.saveVersion = SaveMigrationService.CurrentSaveVersion;
            _saveData.savedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool saved = _fileService.TrySave(_saveData, _loadedFromBackup); // 실제 디스크 저장 성공 여부
            if (!saved) return false;

            _isDirty = false;
            _loadedFromBackup = false;
            _autoSaveTimer = 0f;
            Debug.Log("[Save] 저장을 완료했습니다.");
            return true;
        }
        finally
        {
            _isSaving = false;
        }
    }

    // 파일을 다시 읽고 현재 등록된 모든 Provider에 적용합니다.
    public bool LoadGame()
    {
        bool loaded = LoadFileIntoMemory(); // 메인 또는 백업 로드 성공 여부
        RestoreAllProviders();
        if (_isDirty) SaveGame();
        SaveLoaded?.Invoke();
        return loaded;
    }

    // 저장 파일과 런타임 Provider를 모두 새 게임 기본값으로 초기화합니다.
    public bool ResetSave()
    {
        if (_fileService == null) return false;
        bool deleted = _fileService.DeleteAll(); // 기존 메인·백업·임시 파일 삭제 결과
        _saveData = GameSaveData.CreateNew();
        _loadedFromBackup = false;
        bool providersReset = true; // 모든 Provider 초기화와 Legacy 정리 성공 여부

        foreach (ISaveDataProvider provider in _providers.Values) // 초기화할 등록 Provider
        {
            if (!TryResetProvider(provider)) providersReset = false;
            if (!TryClearLegacySaveData(provider)) providersReset = false;
        }

        _isDirty = true;
        bool saved = SaveGame(); // 기본값 저장 성공 여부
        SaveReset?.Invoke();
        return deleted && providersReset && saved;
    }

    // 새 저장을 만들기 위해 전체 초기화 경로를 재사용합니다.
    public bool CreateNewSave()
    {
        return ResetSave();
    }

    // 메인 또는 백업 저장 파일이 존재하는지 확인합니다.
    public bool HasSave()
    {
        return _fileService != null && _fileService.HasSave();
    }

    // 종료 시 현재 인스턴스의 정적 참조를 정리합니다.
    protected override void OnDestroy()
    {
        _isShuttingDown = true;
        base.OnDestroy();
    }

    // 디스크 저장을 메모리에 읽고 실패하면 새 기본 컨테이너를 준비합니다.
    private bool LoadFileIntoMemory()
    {
        GameSaveData loadedData = null; // 파일에서 읽은 전체 저장 데이터
        bool fromBackup = false; // 백업 파일에서 복구했는지 여부
        bool loaded = _fileService != null && _fileService.TryLoad(out loadedData, out fromBackup); // 저장 파일 로드 성공 여부
        _saveData = loaded ? loadedData : GameSaveData.CreateNew();
        _loadedFromBackup = loaded && fromBackup;
        _isDirty = !loaded || fromBackup;

        if (loaded) Debug.Log($"[Save] 버전 {_saveData.saveVersion} 저장을 불러왔습니다.");
        else Debug.Log("[Save] 저장 파일이 없어 기본 저장을 준비했습니다.");
        return loaded;
    }

    // 현재 저장 구역을 등록된 모든 Provider에 적용합니다.
    private void RestoreAllProviders()
    {
        foreach (ISaveDataProvider provider in _providers.Values) // 복원할 등록 Provider
        {
            if (RestoreProvider(provider)) continue;
            if (TryResetProvider(provider)) _isDirty = true;
        }
    }

    // Provider 기본값 적용 중 예외가 발생해도 다른 시스템의 로드를 계속합니다.
    private static bool TryResetProvider(ISaveDataProvider provider)
    {
        try
        {
            provider.ResetSaveData();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Save] Provider 기본값 적용 실패 ({provider.SaveKey}): {exception.Message}");
            return false;
        }
    }

    // 한 Provider의 JSON 구역을 해당 DTO 형식으로 복원합니다.
    private bool RestoreProvider(ISaveDataProvider provider)
    {
        if (_saveData == null || !_saveData.TryGetSection(provider.SaveKey, out SaveDataSection section)) return false;
        if (string.IsNullOrWhiteSpace(section.json))
        {
            Debug.LogWarning($"[Save] Provider JSON이 비어 있어 기본값을 적용합니다: {provider.SaveKey}");
            return false;
        }

        try
        {
            object data = JsonUtility.FromJson(section.json, provider.SaveDataType); // Provider 형식으로 복원한 DTO
            if (data == null) return false;
            provider.RestoreSaveData(data);
            Debug.Log($"[Save] Provider 복원: {provider.SaveKey}");
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Save] Provider 복원 실패 ({provider.SaveKey}): {exception.Message}");
            return false;
        }
    }

    // 저장 직전 Provider 준비를 실행하고 전체 성공 여부와 변경 여부를 반환합니다.
    private bool TryPrepareProvidersForSave(out bool changed)
    {
        changed = false;
        foreach (ISaveDataProvider provider in _providers.Values) // 저장 직전 상태를 준비할 Provider
        {
            if (provider is not ISaveDataPreparation preparation) continue; // 저장 직전 준비 기능을 제공하는 Provider
            try
            {
                if (preparation.PrepareSaveData()) changed = true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Save] Provider 저장 준비 실패 ({provider.SaveKey}): {exception.Message}");
                return false;
            }
        }

        return true;
    }

    // 전체 Reset에서 Provider가 가진 기존 저장 매체를 안전하게 정리합니다.
    private static bool TryClearLegacySaveData(ISaveDataProvider provider)
    {
        if (provider is not ISaveResetCleanup cleanup) return true; // Legacy 저장 정리 기능을 제공하는 Provider
        try
        {
            cleanup.ClearLegacySaveData();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[Save] Provider Legacy 정리 실패 ({provider.SaveKey}): {exception.Message}");
            return false;
        }
    }

    // Pause와 Focus Loss가 같은 프레임에 발생해도 한 번만 저장합니다.
    private void TryLifecycleSave()
    {
        if (_isSaving || _lastLifecycleSaveFrame == Time.frameCount) return;
        _lastLifecycleSaveFrame = Time.frameCount;
        bool providerDataChanged; // 수명주기 저장 준비에서 Provider 원본이 변경되었는지 여부
        if (!TryPrepareProvidersForSave(out providerDataChanged)) return;
        if (providerDataChanged) _isDirty = true;
        if (_isDirty) SaveGameInternal(false);
    }
}
