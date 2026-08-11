using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    [SerializeField] private List<StageData> stages;
    [SerializeField] private StageSpawner stageSpawner;
    [SerializeField] private ZombieSpawner zombieSpawner;
    
    private List<UnitController> _trackedEnemies = new();

    private int _currentStageIndex;
    private int _remainingEnemyCount;
    private bool _isStageCleared;
    
    public int CurrentStageNumber => stages[_currentStageIndex].StageNumber;
    private bool StageCleared => _isStageCleared;
    
    private async void Start()
    {
        await StartStageAsync(_currentStageIndex);
        zombieSpawner.StartProduction();
    }

    private async Task StartStageAsync(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= stages.Count)
        {
            Debug.Log("스테이지 인덱스랑 데이터 확인하세요");
            return;
        }

        ClearEnemySubscriptions();

        _isStageCleared = false;

        StageData stageData = stages[stageIndex];

        IReadOnlyList<UnitController> enemies =
            await stageSpawner.SpawnAsync(stageData);

        foreach (UnitController enemy in enemies)
        {
            enemy.Died += HandleEnemyDied;
            _trackedEnemies.Add(enemy);
        }

        _remainingEnemyCount = _trackedEnemies.Count;

        Debug.Log(
            $"[StageManager] Stage {stageData.StageNumber} 시작, " +
            $"남은 인간: {_remainingEnemyCount}"
        );

        CheckStageClear();
    }
    
    private void HandleEnemyDied(UnitController enemy)
    {
        enemy.Died -= HandleEnemyDied;

        if (!_trackedEnemies.Remove(enemy))
            return;

        _remainingEnemyCount = _trackedEnemies.Count;

        Debug.Log(
            $"[StageManager] 인간 사망, " +
            $"남은 인간: {_remainingEnemyCount}"
        );

        CheckStageClear();
    }

    private void CheckStageClear()
    {
        if (_isStageCleared)
            return;

        StageData stageData = stages[_currentStageIndex];

        if (stageData.ClearCondition !=
            StageClearCondition.DefeatAllEnemies)
        {
            return;
        }

        if (_remainingEnemyCount > 0)
            return;

        _isStageCleared = true;
        
        Debug.Log(
            $"[StageManager] Stage " +
            $"{stageData.StageNumber} 클리어"
        );

        _ = MoveToNextStageAsync();
    }

    private void ClearEnemySubscriptions()
    {
        foreach (UnitController enemy in _trackedEnemies)
        {
            if (enemy != null)
                enemy.Died -= HandleEnemyDied;
        }

        _trackedEnemies.Clear();
        _remainingEnemyCount = 0;
    }

    private void OnDestroy()
    {
        ClearEnemySubscriptions();
    }

    private async Task MoveToNextStageAsync()
    {
        zombieSpawner.StopProduction();
        zombieSpawner.ReleaseAllZombies();
        
        ClearEnemySubscriptions();
        
        _currentStageIndex++;
        
        if(_currentStageIndex >= stages.Count)
        {
            Debug.Log("모든 스테이지 완료");
            return;
        }
        
        await StartStageAsync(_currentStageIndex);
        
        zombieSpawner.StartProduction();
    }
}
