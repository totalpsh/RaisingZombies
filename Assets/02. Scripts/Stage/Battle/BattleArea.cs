using System.Collections.Generic;
using UnityEngine;

public class BattleArea : MonoBehaviour
{
    [Header("전장 영역")]
    [SerializeField] private Vector2 minBounds;
    [SerializeField] private Vector2 maxBounds;
    
    private readonly List<UnitController> _zombies = new();
    private readonly List<UnitController> _humans = new();
    private readonly List<StructureController> _structures = new();
    
    public void Register(UnitController unit)
    {
        if (unit == null)
            return;

        List<UnitController> units = GetUnits(unit.Team);

        if (!units.Contains(unit))
            units.Add(unit);
    }
    
    public void Unregister(UnitController unit)
    {
        if (unit == null)
            return;

        GetUnits(unit.Team).Remove(unit);
    }

    public List<UnitController> GetEnemies(UnitTeam team)
    {
        return team == UnitTeam.Zombie ? _humans : _zombies;
    }

    public void RegisterStructure(StructureController structure)
    {
        if (structure == null)
            return;

        if (!_structures.Contains(structure))
            _structures.Add(structure);
    }
    
    public void UnregisterStructure(StructureController structure)
    {
        if (structure == null)
            return;

        _structures.Remove(structure);
    }
    
    public List<StructureController> GetEnemyStructures(UnitTeam team)
    {
        return _structures.FindAll(structure => structure != null && !structure.IsDead && structure.Team != team);
    }
    
    private List<UnitController> GetUnits(UnitTeam team)
    {
        return team == UnitTeam.Zombie ? _zombies : _humans;
    }
    
    public Vector3 ClampPosition(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, minBounds.x, maxBounds.x);
        position.y = Mathf.Clamp(position.y, minBounds.y, maxBounds.y);

        return position;
    }
    
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 center = new(
            (minBounds.x + maxBounds.x) * 0.5f,
            (minBounds.y + maxBounds.y) * 0.5f,
            transform.position.z);

        Vector3 size = new(
            maxBounds.x - minBounds.x,
            maxBounds.y - minBounds.y,
            0f);

        Gizmos.DrawWireCube(center, size);
    }
#endif
}
