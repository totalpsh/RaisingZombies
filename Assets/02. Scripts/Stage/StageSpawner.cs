using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class StageSpawner : MonoBehaviour
{
    [SerializeField] private Transform stageOrigin;
    
    private List<UnitController> _spawnedEnemies = new();
    private GameObject defense;
    public List<UnitController> SpawnedObjects => _spawnedEnemies;

    public async Task<List<UnitController>> SpawnAsync(StageData stageData)
    {
        _spawnedEnemies.Clear();
        
        if (stageData == null)
        {
            Debug.LogError($"스테이지 데이터가 엄서요");
            return _spawnedEnemies;
        }

        await SpawnEnemiesAsync(stageData);
        await SpawnDefenseAsync(stageData);
        
        return _spawnedEnemies;
    }

    private async Task SpawnEnemiesAsync(StageData stageData)
    {
        foreach (StageEnemyData enemyData in stageData.Enemies)
        {
            for (int i = 0; i < enemyData.Count; i++)
            {
                GameObject enemyObj = await PoolManager.Instance.GetAsync(enemyData.EnemyKey, activateOnGet: false);

                if (enemyObj == null)
                {
                    Debug.LogError("적 생성 안댐, 에너미가 널임");
                    continue;
                }

                Vector3 spacing = Vector3.right * enemyData.SpawnSpacing * i;
                Vector3 position = stageOrigin.position + enemyData.SpawnOffset + spacing;
                
                enemyObj.transform.SetPositionAndRotation(position, stageOrigin.rotation);
                
                UnitController enemy = enemyObj.GetComponent<UnitController>();
                enemy.Initialize(enemy.Data, new UnitStats(enemy.Data));
                
                _spawnedEnemies.Add(enemy);
                enemyObj.SetActive(true);
                
            }
        }
    }
    
    private async Task SpawnDefenseAsync(StageData stageData)
    {
        StageDefenseData defenseData = stageData.DefenseData;

        if (defenseData == null || !defenseData.Enabled)
            return;

        GameObject defenceObj = await PoolManager.Instance.GetAsync(defenseData.DefenseKey, activateOnGet: false);

        if (defenceObj == null)
        {
            Debug.LogError("디펜스 생성 모대");
            return;
        }

        Vector3 position = stageOrigin.position + defenseData.SpawnOffset;
        
        defenceObj.transform.SetPositionAndRotation(position, stageOrigin.rotation);
        
        // _spawnedEnemies.Add(defenceObj);
        defenceObj.SetActive(true);
    }
}
