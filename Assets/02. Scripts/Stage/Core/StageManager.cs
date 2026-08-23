using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// Stage 전투 진행과 영구 진행 데이터의 Save Provider 연결을 관리합니다.
public class StageManager : MonoBehaviour, ISaveDataProvider
{
    private const string ProviderKey = "stage_progress"; // 통합 저장에서 사용할 Stage 진행 Provider 키
    private const int CurrentStageSaveVersion = 1; // Stage Provider 내부 데이터 형식 버전
    private const int DefaultStageNumber = 1; // 유효한 Stage 정의가 없을 때 사용할 최소 번호

    [SerializeField] private StageCatalogData stageCatalog;
    [SerializeField] private StructureController zombieCamp;
    [SerializeField] private StructureController humanFortress;
    [SerializeField] private ZombieSpawner zombieSpawner;
    [SerializeField] private HumanSpawner humanSpawner;
    
    [SerializeField] private List<StageData> stages;
    [SerializeField] private StageSpawner stageSpawner;
    
    [SerializeField, Min(0f)] private float retryDelay = 1.5f;
    
    private StageGenerator _stageGenerator;
    private StageRuntimeData _currentStageData;

    private int _currentStageNumber = DefaultStageNumber;
    
    // private int _currentStageIndex;
    private bool _isStageRunning;
    private bool _isTransitioning;
    private bool _hasStarted; // Stage Runtime 초기화가 시작됐는지 여부
    public int CurrentStageNumber => _currentStageNumber;

    private bool _allStagesCompleted; // 현재 등록된 모든 Stage를 완료했는지 여부
    string ISaveDataProvider.SaveKey => ProviderKey; // 통합 저장에 노출하는 Stage Provider 키
    Type ISaveDataProvider.SaveDataType => typeof(StageProgressState); // Stage 진행 저장 DTO 형식

    // Stage Provider를 통합 SaveManager에 등록합니다.
    private void Awake()
    {
        _stageGenerator = new StageGenerator(stageCatalog);
        
        SaveManager saveManager = SaveManager.EnsureInstance(); // Stage Provider를 등록할 통합 저장 매니저
        
        if (!saveManager.RegisterProvider(this)) 
            Debug.LogError("[StageManager] Stage Progress Provider 등록에 실패했습니다.", this);
    }

    // 복원된 진행 상태를 기준으로 현재 Stage Runtime을 시작합니다.
    private async void Start()
    {
        _hasStarted = true;
        
        if (_allStagesCompleted)
        {
            Debug.Log("[StageManager] 저장된 진행도에서 모든 스테이지 완료 상태를 복원했습니다.");
            return;
        }

        await StartStageAsync(_currentStageNumber);
    }

    private async Task StartStageAsync(int stageNumber)
    {
        // if (!IsValidStageIndex(stageNumber))
        // {
        //     Debug.LogError($"잘못된 스테이지 인덱스: " + $"Stage{stageNumber}");
        //     return;
        // }

        if (!ValidateReferences())
            return;

        StopBattle();
        ClearObjectiveSubscriptions();

        _currentStageData = _stageGenerator.Generate(stageNumber);

        if (_currentStageData == null)
        {
            Debug.LogError(
                $"[StageManager] Stage {stageNumber} " +
                "생성에 실패했습니다.");

            _isStageRunning = false;
            _isTransitioning = false;
            return;
        }
        
        _currentStageNumber = _currentStageData.StageNumber;
        
        // StageData stageData = stages[stageNumber];

        zombieCamp.gameObject.SetActive(true);
        humanFortress.gameObject.SetActive(true);
        
        zombieCamp.Initialize();
        humanFortress.Initialize();

        zombieCamp.Destroyed += HandleZombieCampDestroyed;
        humanFortress.Destroyed += HandleHumanFortressDestroyed;

        stageSpawner.Spawn(_currentStageData);

        zombieSpawner.SetSpawnOrigin(zombieCamp.SpawnPoint);
        humanSpawner.SetSpawnOrigin(humanFortress.SpawnPoint);

        _isStageRunning = true;
        _isTransitioning = false;

        humanSpawner.StartProduction(_currentStageData);
        zombieSpawner.StartProduction();

        Debug.Log($"Stage " + $"{_currentStageData.StageNumber} 시작");

        await Task.Yield();
    }

    private void HandleHumanFortressDestroyed(StructureController fortress)
    {
        if (!_isStageRunning || _isTransitioning)
            return;

        _isStageRunning = false;
        _isTransitioning = true;

        Debug.Log($"tage " + $"{CurrentStageNumber} 승리");

        AdvanceStageProgressAndSave();
        _ = MoveToNextStageAsync();
    }

    private void HandleZombieCampDestroyed(StructureController camp)
    {
        if (!_isStageRunning || _isTransitioning)
            return;

        _isStageRunning = false;
        _isTransitioning = true;

        StopBattle();

        Debug.Log($"Stage " + $"{CurrentStageNumber} 패배");

        _ = RetryCurrentStageAsync();
    }

    private async Task RetryCurrentStageAsync()
    {
        if (retryDelay > 0f)
        {
            int delayMilliseconds = Mathf.RoundToInt(retryDelay * 1000f);
            await Task.Delay(delayMilliseconds);
        }

        if (this == null)
            return;

        // StageFadeUI fadeUI = await UIManager.Instance.GetOrCreateStageFadeUIAsync();

        // if (fadeUI != null)
        //     await fadeUI.FadeOutAsync();

        ClearCurrentStage();

        await StartStageAsync(_currentStageNumber);

        // if (fadeUI != null)
        //     await fadeUI.FadeInAsync();
    }

    private async Task MoveToNextStageAsync()
    {
        StopBattle();

        // StageFadeUI fadeUI = await UIManager.Instance.GetOrCreateStageFadeUIAsync();

        // if (fadeUI != null)
        //     await fadeUI.FadeOutAsync();

        ClearCurrentStage();

        await StartStageAsync(_currentStageNumber);

        // if (fadeUI != null)
        //     await fadeUI.FadeInAsync();
    }

    // Clear된 Stage 다음 진행 상태를 갱신하고 전환 전에 즉시 저장합니다.
    private void AdvanceStageProgressAndSave()
    {
        if (_currentStageNumber < int.MaxValue)
            _currentStageNumber++;
        
        // int nextStageIndex = FindNextValidStageIndex(_currentStageIndex); // Clear 후 진행할 다음 유효 Stage 인덱스
        //
        // if (nextStageIndex >= 0)
        // {
        //     _currentStageIndex = nextStageIndex;
        //     _allStagesCompleted = false;
        // }
        // else
        // {
        //     _allStagesCompleted = true;
        // }

        SaveManager saveManager = SaveManager.EnsureInstance(); // Stage Clear 진행도를 기록할 통합 저장 매니저
        saveManager.MarkDirty();
        bool saved = saveManager.SaveGame(); // 다음 Stage 시작 전 디스크 저장 성공 여부
        
        if (!saved) 
            Debug.LogWarning("[StageManager] Stage Clear 진행도 저장에 실패했습니다. 다음 저장에서 다시 시도합니다.", this);
    }

    // 현재 Stage 영구 진행 원본을 저장 DTO로 반환합니다.
    public object CaptureSaveData()
    {
        return new StageProgressState
        {
            version = CurrentStageSaveVersion,
            currentStageNumber = _currentStageNumber,
            // allStagesCompleted = _allStagesCompleted
            allStagesCompleted = false,
        };
    }

    // 저장된 Stage 진행도를 검증한 뒤 Runtime 진행 원본에 적용합니다.
    public void RestoreSaveData(object data)
    {
        StageProgressState restoredState = data as StageProgressState; // 통합 저장에서 역직렬화한 Stage 진행 DTO
        
        int storedVersion = restoredState == null ? 0 : restoredState.version; // 보정 전 Stage Provider 버전
        
        int storedStageNumber = restoredState == null ? DefaultStageNumber : restoredState.currentStageNumber; // 보정 전 진행 Stage 번호
        
        bool storedCompletion = restoredState != null && restoredState.allStagesCompleted; // 보정 전 전체 Stage 완료 여부
        
        if (!TryMigrateStageProgressState(restoredState)) 
            throw new InvalidOperationException("지원하지 않는 Stage Provider 저장 버전입니다.");

        _currentStageNumber = Mathf.Max(DefaultStageNumber, restoredState.currentStageNumber);
        
        // ApplyStageProgressState(restoredState);
        
        if (storedVersion != CurrentStageSaveVersion || storedStageNumber != _currentStageNumber) // || storedCompletion != _allStagesCompleted)
            SaveManager.Instance.MarkDirty();
        
        if (_hasStarted) 
            _ = RestartStageFromProgressAsync();
    }

    // Stage 진행도를 첫 번째 유효 Stage의 기본값으로 되돌립니다.
    public void ResetSaveData()
    {
        // int firstStageIndex = FindFirstValidStageIndex(); // 새 게임에서 시작할 첫 번째 유효 Stage 인덱스
        // _currentStageIndex = firstStageIndex >= 0 ? firstStageIndex : 0;
        // _allStagesCompleted = firstStageIndex < 0;

        _currentStageNumber = DefaultStageNumber;
        
        if (_hasStarted) _ = RestartStageFromProgressAsync();
    }

    // Provider 내부 버전을 현재 Stage 진행 형식으로 올릴 수 있는지 확인합니다.
    private static bool TryMigrateStageProgressState(StageProgressState state)
    {
        if (state == null) 
            return false;
        
        if (state.version > CurrentStageSaveVersion)
        {
            Debug.LogError($"[StageManager] 지원하지 않는 Stage Provider 버전입니다: {state.version}");
            return false;
        }

        // if (state.version <= 0) state.version = CurrentStageSaveVersion;
        // return state.version == CurrentStageSaveVersion;
        
        // 버전 1에서 모든 스테이지를 완료했다면
        // 마지막 번호의 다음 스테이지로 진행시킨다.
        if (state.version <= 1)
        {
            if (state.allStagesCompleted && state.currentStageNumber < int.MaxValue)
                state.currentStageNumber++;

            state.allStagesCompleted = false;
            state.version = 2;
        }

        return state.version == CurrentStageSaveVersion;
    }

    // // 저장된 Stage 번호와 완료 상태를 현재 Stage Definition 목록에 맞게 보정합니다.
    // private void ApplyStageProgressState(StageProgressState state)
    // {
    //     int restoredStageIndex = ResolveStageIndex(state.currentStageNumber); // 저장된 번호에 대응하는 현재 Stage 인덱스
    //     _currentStageIndex = restoredStageIndex >= 0 ? restoredStageIndex : 0;
    //     _allStagesCompleted = state.allStagesCompleted && restoredStageIndex >= 0;
    //
    //     if (!_allStagesCompleted) return;
    //     int addedStageIndex = FindNextValidStageIndex(_currentStageIndex); // 완료 저장 이후 새로 추가된 다음 Stage 인덱스
    //     if (addedStageIndex < 0) return;
    //     _currentStageIndex = addedStageIndex;
    //     _allStagesCompleted = false;
    // }

    // 현재 진행 번호와 가장 잘 맞는 유효 Stage Definition 인덱스를 찾습니다.
    private int ResolveStageIndex(int stageNumber)
    {
        int fallbackIndex = FindFirstValidStageIndex(); // 저장 번호보다 작은 Stage가 없을 때 사용할 첫 Stage 인덱스
        int closestIndex = -1; // 저장 번호 이하에서 가장 가까운 Stage 인덱스
        int closestNumber = int.MinValue; // 현재까지 찾은 가장 가까운 Stage 번호
        if (stages == null) return fallbackIndex;

        for (int index = 0; index < stages.Count; index++) // 저장 번호와 비교할 Stage Definition 인덱스
        {
            StageData stage = stages[index]; // 현재 검사할 Stage Definition
            if (stage == null) continue;
            if (stage.StageNumber == stageNumber) return index;
            if (stage.StageNumber > stageNumber || stage.StageNumber <= closestNumber) continue;
            closestIndex = index;
            closestNumber = stage.StageNumber;
        }

        return closestIndex >= 0 ? closestIndex : fallbackIndex;
    }

    // 현재 목록에서 첫 번째 유효 Stage Definition 인덱스를 찾습니다.
    private int FindFirstValidStageIndex()
    {
        if (stages == null) return -1;
        for (int index = 0; index < stages.Count; index++) // 첫 Stage를 찾기 위한 목록 인덱스
        {
            if (stages[index] != null) return index;
        }

        return -1;
    }

    // 지정한 Stage 뒤의 다음 유효 Stage Definition 인덱스를 찾습니다.
    private int FindNextValidStageIndex(int stageIndex)
    {
        if (stages == null) return -1;
        for (int index = stageIndex + 1; index < stages.Count; index++) // 다음 Stage를 찾기 위한 목록 인덱스
        {
            if (stages[index] != null) return index;
        }

        return -1;
    }

    // 현재 인덱스의 Stage 번호를 안전하게 반환합니다.
    // private int GetCurrentStageNumber()
    // {
    //     if (IsValidStageIndex(_currentStageIndex)) return stages[_currentStageIndex].StageNumber;
    //     int firstStageIndex = FindFirstValidStageIndex(); // 번호를 대신 제공할 첫 번째 유효 Stage 인덱스
    //     return firstStageIndex >= 0 ? stages[firstStageIndex].StageNumber : DefaultStageNumber;
    // }

    // Load 또는 Reset으로 변경된 진행 상태에서 Stage Runtime을 새로 구성합니다.
    private async Task RestartStageFromProgressAsync()
    {
        _isStageRunning = false;
        _isTransitioning = true;
        
        StopBattle();
        ClearCurrentStage();
        // ClearObjectiveSubscriptions();
        
        // if (zombieSpawner != null) zombieSpawner.ReleaseAllZombies();
        // if (humanSpawner != null) humanSpawner.ReleaseAllHumans();
        // if (stageSpawner != null) stageSpawner.Clear();

        // if (_allStagesCompleted)
        // {
        //     _isTransitioning = false;
        //     Debug.Log("[StageManager] 모든 스테이지 완료 상태를 적용했습니다.");
        //     return;
        // }

        await StartStageAsync(_currentStageNumber);
    }

    private void StopBattle()
    {
        if (zombieSpawner != null)
            zombieSpawner.StopProduction();
        
        if (humanSpawner != null)
            humanSpawner.StopProduction();
    }

    private void ClearObjectiveSubscriptions()
    {
        if (zombieCamp != null)
            zombieCamp.Destroyed -= HandleZombieCampDestroyed;

        if (humanFortress != null)
            humanFortress.Destroyed -= HandleHumanFortressDestroyed;
    }

    private bool IsValidStageIndex(int stageIndex)
    {
        return stages != null
               && stageIndex >= 0
               && stageIndex < stages.Count
               && stages[stageIndex] != null;
    }

    private bool ValidateReferences()
    {
        if (stageCatalog == null ||
            stageSpawner == null ||
            zombieCamp == null ||
            humanFortress == null ||
            zombieSpawner == null ||
            humanSpawner == null)
        {
            Debug.LogError("[StageManager] 참조가 누락되었습니다.");

            return false;
        }

        if (zombieCamp.SpawnPoint == null)
        {
            Debug.LogError("[StageManager] ZombieCamp의 " + "SpawnPoint가 없습니다.");
            return false;
        }

        if (humanFortress.SpawnPoint == null)
        {
            Debug.LogError("[StageManager] HumanFortress의 " + "SpawnPoint가 없습니다.");
            return false;
        }

        return true;
    }
    
    private void ClearCurrentStage()
    {
        ClearObjectiveSubscriptions();

        if (zombieSpawner != null)
            zombieSpawner.ReleaseAllZombies();

        if (humanSpawner != null)
            humanSpawner.ReleaseAllHumans();

        if (stageSpawner != null)
            stageSpawner.Clear();

        _currentStageData = null;
    }

    private void OnDestroy()
    {
        if (SaveManager.HasInstance) 
            SaveManager.Instance.UnregisterProvider(this);
        
        StopBattle();
        ClearObjectiveSubscriptions();
    }
}
