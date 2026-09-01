using System.Collections.Generic;
using UnityEngine;

public class UnitTargeting : MonoBehaviour
{
    [SerializeField, Min(0f)] private float frontLineTolerance = 0.5f;
    
    private UnitController _owner;
    private BattleArea _battleArea;

    public ICombatTarget CurrentTarget { get; private set; }
    
    public void Initialize(UnitController owner, BattleArea battleArea)
    {
        _owner = owner;
        _battleArea = battleArea;

        CurrentTarget = null;
    }

    public ICombatTarget FindTarget()
    {
        if (_owner == null || _battleArea == null)
            return null;
        
        UnitController unitTarget = FindUnitTarget();

        if (unitTarget != null)
        {
            CurrentTarget = unitTarget;
            return CurrentTarget;
        }
        
        StructureController structureTarget = FindStructureTarget();

        CurrentTarget = structureTarget;
        return CurrentTarget;
    }
    
    private UnitController FindUnitTarget()
    {
        List<UnitController> enemies = _battleArea.GetEnemies(_owner.Team);

        float frontX = FindFrontX(enemies);

        if (float.IsInfinity(frontX))
            return null;

        return FindClosestOnFront(enemies, frontX);
    }

    private float FindFrontX(List<UnitController> enemies)
    {
        float frontX = _owner.Team == UnitTeam.Zombie ? float.PositiveInfinity : float.NegativeInfinity;

        foreach (UnitController enemy in enemies)
        {
            if (!IsValidUnit(enemy))
                continue;

            float x = enemy.transform.position.x;

            if (_owner.Team == UnitTeam.Zombie)
                frontX = Mathf.Min(frontX, x);
            else
                frontX = Mathf.Max(frontX, x);
        }

        return frontX;
    }

    private UnitController FindClosestOnFront(List<UnitController> enemies, float frontX)
    {
        UnitController bestTarget = null;
        float nearestY = float.MaxValue;

        foreach (UnitController enemy in enemies)
        {
            if (!IsValidUnit(enemy))
                continue;

            float xDifference = Mathf.Abs(enemy.transform.position.x - frontX);

            if (xDifference > frontLineTolerance)
                continue;

            float yDifference = Mathf.Abs(enemy.transform.position.y - _owner.transform.position.y);

            if (yDifference >= nearestY)
                continue;

            bestTarget = enemy;
            nearestY = yDifference;
        }

        return bestTarget;
    }
    
    private StructureController FindStructureTarget()
    {
        List<StructureController> structures = _battleArea.GetEnemyStructures(_owner.Team);
        StructureController defenseLine = FindNearestStructure(structures, StructureType.DefenseLine);

        if (defenseLine != null)
            return defenseLine;

        StructureType baseType = _owner.Team == UnitTeam.Zombie ? StructureType.HumanFortress : StructureType.ZombieCamp;

        return FindNearestStructure(structures, baseType);
    }
    
    private StructureController FindNearestStructure(List<StructureController> structures, StructureType type)
    {
        StructureController bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (StructureController structure in structures)
        {
            if (!IsValidStructure(structure))
                continue;

            if (structure.StructureType != type)
                continue;

            if (!IsAhead(structure))
                continue;

            float distance = Mathf.Abs(structure.transform.position.x - _owner.transform.position.x);

            if (distance >= bestDistance)
                continue;

            bestTarget = structure;
            bestDistance = distance;
        }

        return bestTarget;
    }

    private bool IsAhead(ICombatTarget target)
    {
        float offset = target.TargetTransform.position.x - _owner.transform.position.x;
        
        return _owner.Team == UnitTeam.Zombie ? offset > 0f : offset < 0f;
    }

    private static bool IsValidUnit(UnitController unit)
    {
        return unit != null && !unit.IsDead && unit.gameObject.activeInHierarchy;
    }

    private static bool IsValidStructure(StructureController structure)
    {
        return structure != null && !structure.IsDead && structure.gameObject.activeInHierarchy;
    }
}
