using System.Collections.Generic;
using UnityEngine;

public class UnitTargeting : MonoBehaviour
{
    private readonly List<UnitController> _unitCandidates = new();
    private readonly List<StructureController> _structureCandidates = new();

    private UnitController _owner;
    private BattleArea _battleArea;

    public void Initialize(
        UnitController owner,
        BattleArea battleArea)
    {
        _owner = owner;
        _battleArea = battleArea;

        _unitCandidates.Clear();
        _structureCandidates.Clear();
    }

    public CombatTargetingStatus FindAssignment(
        out CombatTargetAssignment assignment)
    {
        assignment = default;

        if (_owner == null || _battleArea == null)
            return CombatTargetingStatus.NoTarget;

        IReadOnlyList<UnitController> enemies =
            _battleArea.GetEnemyUnits(_owner.Team);

        if (CollectUnitCandidates(enemies))
        {
            return TryAssignUnit(out assignment)
                ? CombatTargetingStatus.Assigned
                : CombatTargetingStatus.Blocked;
        }

        return FindStructureAssignment(out assignment);
    }

    private bool CollectUnitCandidates(
        IReadOnlyList<UnitController> enemies)
    {
        _unitCandidates.Clear();

        foreach (UnitController enemy in enemies)
        {
            if (!IsValidUnit(enemy))
                continue;

            if (GetForwardDistance(enemy) < 0f)
                continue;

            _unitCandidates.Add(enemy);
        }

        _unitCandidates.Sort(CompareUnitCandidates);

        return _unitCandidates.Count > 0;
    }

    private int CompareUnitCandidates(
        UnitController first,
        UnitController second)
    {
        int xComparison = GetForwardDistance(first)
            .CompareTo(GetForwardDistance(second));

        if (xComparison != 0)
            return xComparison;

        float firstYDistance = Mathf.Abs(
            first.transform.position.y -
            _owner.transform.position.y);

        float secondYDistance = Mathf.Abs(
            second.transform.position.y -
            _owner.transform.position.y);

        int yComparison =
            firstYDistance.CompareTo(secondYDistance);

        if (yComparison != 0)
            return yComparison;

        return first.GetInstanceID()
            .CompareTo(second.GetInstanceID());
    }

    private float GetForwardDistance(UnitController target)
    {
        float offset =
            target.transform.position.x -
            _owner.transform.position.x;

        return _owner.Team == UnitTeam.Zombie
            ? offset
            : -offset;
    }

    private bool TryAssignUnit(
        out CombatTargetAssignment assignment)
    {
        assignment = default;

        foreach (UnitController candidate in _unitCandidates)
        {
            if (TryReserve(candidate, out assignment))
                return true;
        }

        return false;
    }

    private CombatTargetingStatus FindStructureAssignment(
        out CombatTargetAssignment assignment)
    {
        assignment = default;

        IReadOnlyList<StructureController> structures =
            _battleArea.GetEnemyStructures(_owner.Team);

        if (CollectStructures(
                structures,
                StructureType.DefenseLine))
        {
            return TryAssignStructure(out assignment)
                ? CombatTargetingStatus.Assigned
                : CombatTargetingStatus.Blocked;
        }

        StructureType baseType =
            _owner.Team == UnitTeam.Zombie
                ? StructureType.HumanFortress
                : StructureType.ZombieCamp;

        if (!CollectStructures(structures, baseType))
            return CombatTargetingStatus.NoTarget;

        return TryAssignStructure(out assignment)
            ? CombatTargetingStatus.Assigned
            : CombatTargetingStatus.Blocked;
    }

    private bool CollectStructures(
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

        return _structureCandidates.Count > 0;
    }

    private int CompareStructures(
        StructureController first,
        StructureController second)
    {
        float firstDistance = Mathf.Abs(
            first.TargetTransform.position.x -
            _owner.transform.position.x);

        float secondDistance = Mathf.Abs(
            second.TargetTransform.position.x -
            _owner.transform.position.x);

        int distanceComparison =
            firstDistance.CompareTo(secondDistance);

        if (distanceComparison != 0)
            return distanceComparison;

        return first.GetInstanceID()
            .CompareTo(second.GetInstanceID());
    }

    private bool TryAssignStructure(
        out CombatTargetAssignment assignment)
    {
        assignment = default;

        foreach (StructureController candidate
                 in _structureCandidates)
        {
            if (TryReserve(candidate, out assignment))
                return true;
        }

        return false;
    }

    private bool TryReserve(
        ICombatTarget target,
        out CombatTargetAssignment assignment)
    {
        assignment = default;

        if (target is not MonoBehaviour targetObject)
            return false;

        if (!targetObject.TryGetComponent(out CombatSlots slots))
            return false;

        if (!slots.TryReserve(_owner, out int slotIndex))
            return false;

        assignment = new CombatTargetAssignment(
            target,
            slots,
            slotIndex);

        return true;
    }

    private bool IsAhead(ICombatTarget target)
    {
        float offset =
            target.TargetTransform.position.x -
            _owner.transform.position.x;

        return _owner.Team == UnitTeam.Zombie
            ? offset >= 0f
            : offset <= 0f;
    }

    private static bool IsValidUnit(UnitController unit)
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
