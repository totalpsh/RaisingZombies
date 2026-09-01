using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class StageSpawner : MonoBehaviour
{
    [SerializeField] private BattleArea battleArea;
    [SerializeField] private Transform stageOrigin;
    private List<StructureController> _spawnedDefenses = new();
    public List<StructureController> SpawnedDefenses => _spawnedDefenses;
    
    public IReadOnlyList<StructureController> Spawn(StageRuntimeData stageData)
    {
        Clear();

        if (stageData == null)
        {
            Debug.LogError("[StageSpawner] StageRuntimeData가 null입니다.");

            return _spawnedDefenses;
        }

        if (stageOrigin == null)
        {
            Debug.LogError("[StageSpawner] StageOrigin이 없습니다.");

            return _spawnedDefenses;
        }

        if (stageData.Defenses == null)
            return _spawnedDefenses;

        foreach (StageDefenseData defenseData in stageData.Defenses)
            SpawnDefense(defenseData);
        
        return _spawnedDefenses;
    }

    private void SpawnDefense(
        StageDefenseData defenseData)
    {
        if (defenseData == null)
            return;

        if (defenseData.Prefab == null)
        {
            Debug.LogError(
                "[StageSpawner] 방어시설 프리팹이 없습니다.");

            return;
        }

        Vector3 position =
            stageOrigin.position
            + defenseData.SpawnOffset;

        StructureController defense = Instantiate(defenseData.Prefab, position, stageOrigin.rotation);

        defense.Initialize(battleArea, defenseData.HealthMultiplier);

        _spawnedDefenses.Add(defense);
    }

    public void Clear()
    {
        foreach (StructureController defense
                 in _spawnedDefenses)
        {
            if (defense != null)
                Destroy(defense.gameObject);
        }

        _spawnedDefenses.Clear();
    }

    private void OnDestroy()
    {
        Clear();
    }
}
