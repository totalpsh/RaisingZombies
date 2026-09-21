using System.Collections.Generic;
using UnityEngine;

public class UnitTargeting : MonoBehaviour
{
    private readonly List<StructureController> _structureCandidates = new();

    private UnitController _owner;
    private BattleArea _battleArea;

    public void Initialize(
        UnitController owner,
        BattleArea battleArea)
    {
        _owner = owner;
        _battleArea = battleArea;

        _structureCandidates.Clear();
    }

    public ICombatTarget FindTarget()
    {
        if (_owner == null || _battleArea == null)
            return null;

        IReadOnlyList<UnitController> enemies =
            _battleArea.GetEnemyUnits(_owner.Team);

        UnitController unitTarget =
            FindUnitTarget(enemies);

        if (unitTarget != null)
            return unitTarget;

        ICombatTarget additionalTarget = FindAdditionalTarget();
        if (additionalTarget != null)
            return additionalTarget;

        return FindStructureTarget();
    }

    private ICombatTarget FindAdditionalTarget()
    {
        ICombatTarget selected = null;
        float nearestX = float.MaxValue;
        int selectedId = int.MaxValue;

        foreach (ICombatTarget target in _battleArea.GetAdditionalEnemyTargets(_owner.Team))
        {
            if (target is not MonoBehaviour targetObject || targetObject == null ||
                !targetObject.gameObject.activeInHierarchy || target.IsDead ||
                target.Team == _owner.Team || target.TargetTransform == null)
                continue;

            float distance = GetForwardDistance(target);
            if (distance < 0f || (selected != null && distance > nearestX))
                continue;

            int targetId = targetObject.GetInstanceID();
            if (selected == null || distance < nearestX ||
                Mathf.Approximately(distance, nearestX) && targetId < selectedId)
            {
                selected = target;
                nearestX = distance;
                selectedId = targetId;
            }
        }

        return selected;
    }

    private UnitController FindUnitTarget(
        IReadOnlyList<UnitController> enemies)
    {
        UnitController selected = null;
        float nearestX = float.MaxValue;

        foreach (UnitController enemy in enemies)
        {
            if (!IsValidUnit(enemy))
                continue;

            float xDistance =
                GetForwardDistance(enemy);

            if (xDistance < 0f)
                continue;

            if (selected != null &&
                xDistance > nearestX)
            {
                continue;
            }

            if (selected == null ||
                xDistance < nearestX ||
                Mathf.Approximately(
                    xDistance,
                    nearestX) &&
                enemy.GetInstanceID() <
                selected.GetInstanceID())
            {
                selected = enemy;
                nearestX = xDistance;
            }
        }

        return selected;
    }

    private StructureController FindStructureTarget()
    {
        IReadOnlyList<StructureController> structures =
            _battleArea.GetEnemyStructures(_owner.Team);

        StructureController defenseLine =
            FindFirstStructure(
                structures,
                StructureType.DefenseLine);

        if (defenseLine != null)
            return defenseLine;

        StructureType finalBaseType =
            _owner.Team == UnitTeam.Zombie
                ? StructureType.HumanFortress
                : StructureType.ZombieCamp;

        return FindFirstStructure(
            structures,
            finalBaseType);
    }

    private StructureController FindFirstStructure(
        IReadOnlyList<StructureController> structures,
        StructureType type)
    {
        _structureCandidates.Clear();

        foreach (StructureController structure in structures)
        {
            if (!IsValidStructure(structure))
                continue;

            if (structure.StructureType != type)
                continue;

            if (!IsAhead(structure))
                continue;

            _structureCandidates.Add(structure);
        }

        _structureCandidates.Sort(CompareStructures);

        return _structureCandidates.Count > 0
            ? _structureCandidates[0]
            : null;
    }

    private int CompareStructures(
        StructureController first,
        StructureController second)
    {
        int xComparison = GetForwardDistance(first)
            .CompareTo(GetForwardDistance(second));

        if (xComparison != 0)
            return xComparison;

        return first.GetInstanceID()
            .CompareTo(second.GetInstanceID());
    }

    private float GetForwardDistance(
        ICombatTarget target)
    {
        float offset =
            target.TargetTransform.position.x -
            _owner.transform.position.x;

        return _owner.Team == UnitTeam.Zombie
            ? offset
            : -offset;
    }

    private bool IsAhead(ICombatTarget target)
    {
        return GetForwardDistance(target) >= 0f;
    }

    private static bool IsValidUnit(
        UnitController unit)
    {
        return unit != null &&
               !unit.IsDead &&
               unit.gameObject.activeInHierarchy;
    }

    private static bool IsValidStructure(
        StructureController structure)
    {
        return structure != null &&
               !structure.IsDead &&
               structure.gameObject.activeInHierarchy;
    }
}
