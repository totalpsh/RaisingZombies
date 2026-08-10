using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class StageSpawner : MonoBehaviour
{
    [SerializeField] private Transform stageOrigin;
    
    private List<GameObject> _spawnedObjects = new();
    
    public List<GameObject> SpawnedObjects => _spawnedObjects;

    public async Task SpawnAsync(StageData stageData)
    {
        if (stageData == null)
        {
            Debug.LogError("데이터 없다 넣어라");
            return;
        }

        await SpawnEnemiesAsync(stageData);
        await SpwnDefenceAsync(stageData);
    }

    private async Task SpawnEnemiesAsync(StageData stageData)
    {
        throw new System.NotImplementedException();
    }

    private async Task SpwnDefenceAsync(StageData stageData)
    {
        throw new System.NotImplementedException();
    }
}
