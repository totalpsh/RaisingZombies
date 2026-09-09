using System.Collections.Generic;
using UnityEngine;

public class UnitTargeting : MonoBehaviour
{
    [SerializeField, Min(0f)]
    private float frontLineTolerance = 0.5f;

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

        return FindStructureTarget();
    }

    private UnitController FindUnitTarget(
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

        if (_unitCandidates.Count == 0)
            return null;

        _unitCandidates.Sort(
            CompareUnitCandidatesByX);

        float nearestX =
            GetForwardDistance(_unitCandidates[0]);

        int groupEnd = 1;

        while (groupEnd < _unitCandidates.Count)
        {
            float candidateX =
                GetForwardDistance(
                    _unitCandidates[groupEnd]);

            if (candidateX >
                nearestX + frontLineTolerance)
            {
                break;
            }

            groupEnd++;
        }

        _unitCandidates.Sort(
            0,
            groupEnd,
            Comparer<UnitController>.Create(
                CompareWithinFrontLine));

        return _unitCandidates[0];
    }

    private int CompareUnitCandidatesByX(
        UnitController first,
        UnitController second)
    {
        int xComparison = GetForwardDistance(first)
            .CompareTo(GetForwardDistance(second));

        if (xComparison != 0)
            return xComparison;

        return first.GetInstanceID()
            .CompareTo(second.GetInstanceID());
    }

    private int CompareWithinFrontLine(
        UnitController first,
        UnitController second)
    {
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

        int xComparison = GetForwardDistance(first)
            .CompareTo(GetForwardDistance(second));

        if (xComparison != 0)
            return xComparison;

        return first.GetInstanceID()
            .CompareTo(second.GetInstanceID());
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
