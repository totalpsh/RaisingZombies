using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [SerializeField] private List<StageData> stages;
    [SerializeField] private StageSpawner stageSpawner;
    [SerializeField] private StructureController zombieCamp;
    [SerializeField] private StructureController humanFortress;
    [SerializeField] private ZombieSpawner zombieSpawner;
    [SerializeField] private HumanSpawner humanSpawner;
    
    [SerializeField, Min(0f)] private float retryDelay = 1.5f;
    
    private int _currentStageIndex;
    private bool _isStageRunning;
    private bool _isTransitioning;

    public int CurrentStageNumber => stages[_currentStageIndex].StageNumber;

    private async void Start()
    {
        await StartStageAsync(_currentStageIndex);
    }

    private async Task StartStageAsync(int stageIndex)
    {
        if (!IsValidStageIndex(stageIndex))
        {
            Debug.LogError($"잘못된 스테이지 인덱스: " + $"Stage{stageIndex}");
            return;
        }

        if (!ValidateReferences())
            return;

        StopBattle();
        ClearObjectiveSubscriptions();

        StageData stageData = stages[stageIndex];

        zombieCamp.Initialize();
        humanFortress.Initialize();

        zombieCamp.Destroyed += HandleZombieCampDestroyed;
        humanFortress.Destroyed += HandleHumanFortressDestroyed;

        stageSpawner.Spawn(stageData);

        zombieSpawner.SetSpawnOrigin(zombieCamp.SpawnPoint);
        humanSpawner.SetSpawnOrigin(humanFortress.SpawnPoint);

        _isStageRunning = true;
        _isTransitioning = false;

        humanSpawner.StartProduction(stageData);
        zombieSpawner.StartProduction();

        Debug.Log($"Stage " + $"{stageData.StageNumber} 시작");

        await Task.Yield();
    }

    private void HandleHumanFortressDestroyed(StructureController fortress)
    {
        if (!_isStageRunning || _isTransitioning)
            return;

        _isStageRunning = false;
        _isTransitioning = true;

        Debug.Log($"tage " + $"{CurrentStageNumber} 승리");

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

        StageFadeUI fadeUI = await UIManager.Instance.GetOrCreateStageFadeUIAsync();

        if (fadeUI != null)
            await fadeUI.FadeOutAsync();

        ClearCurrentStage();

        await StartStageAsync(_currentStageIndex);

        if (fadeUI != null)
            await fadeUI.FadeInAsync();
    }

    private async Task MoveToNextStageAsync()
    {
        StopBattle();

        StageFadeUI fadeUI = await UIManager.Instance.GetOrCreateStageFadeUIAsync();

        if (fadeUI != null)
            await fadeUI.FadeOutAsync();

        ClearCurrentStage();

        _currentStageIndex++;

        if (_currentStageIndex >= stages.Count)
        {
            Debug.Log("[StageManager] 모든 스테이지 완료");

            if (fadeUI != null)
                await fadeUI.FadeInAsync();

            return;
        }

        await StartStageAsync(_currentStageIndex);

        if (fadeUI != null)
            await fadeUI.FadeInAsync();
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
        {
            zombieCamp.Destroyed -=
                HandleZombieCampDestroyed;
        }

        if (humanFortress != null)
        {
            humanFortress.Destroyed -=
                HandleHumanFortressDestroyed;
        }
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
        if (stageSpawner == null || zombieCamp == null || humanFortress == null || zombieSpawner == null)
        {
            Debug.LogError("참조 누락.");

            return false;
        }

        if (zombieCamp.SpawnPoint == null)
        {
            Debug.LogError("ZombieCamp의 " + "SpawnPoint가 엄서영");

            return false;
        }
        
        if (humanSpawner == null || humanFortress.SpawnPoint == null)
        {
            Debug.LogError("HumanSpawner/HumanFortress SpawnPoint가 엄서영");

            return false;
        }

        return true;
    }
    
    private void ClearCurrentStage()
    {
        ClearObjectiveSubscriptions();

        zombieSpawner.ReleaseAllZombies();
        humanSpawner.ReleaseAllHumans();
        stageSpawner.Clear();
    }

    private void OnDestroy()
    {
        StopBattle();
        ClearObjectiveSubscriptions();
    }
}
