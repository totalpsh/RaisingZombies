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
    private UnitCombat _combat;

    public void Initialize(
        UnitController owner,
        BattleArea battleArea,
        UnitCombat combat)
    {
        _owner = owner;
        _battleArea = battleArea;
        _combat = combat;

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

        StructureController defenseLine =
            FindEnemyDefenseLine();

        if (CollectUnitCandidates(
                enemies,
                defenseLine))
        {
            return TryAssignUnit(out assignment)
                ? CombatTargetingStatus.Assigned
                : CombatTargetingStatus.Blocked;
        }

        return FindStructureAssignment(out assignment);
    }

    public RangedTargetResult FindRangedTarget()
    {
        if (_owner == null ||
            _battleArea == null ||
            _combat == null)
        {
            return default;
        }

        IReadOnlyList<UnitController> enemies =
            _battleArea.GetEnemyUnits(_owner.Team);

        StructureController defenseLine =
            FindEnemyDefenseLine();

        if (CollectRangedUnitCandidates(
                enemies,
                defenseLine))
        {
            UnitController unitTarget =
                SelectFirstFrontLineCandidate();

            if (unitTarget != null)
            {
                return new RangedTargetResult(
                    unitTarget);
            }
        }

        StructureController structureTarget =
            FindRangedStructureTarget(defenseLine);

        return structureTarget != null
            ? new RangedTargetResult(structureTarget)
            : default;
    }

    public StructureController FindFriendlyDefenseLine()
    {
        if (_owner == null || _battleArea == null)
            return null;

        IReadOnlyList<StructureController> structures =
            _battleArea.GetFriendlyStructures(_owner.Team);

        _structureCandidates.Clear();

        foreach (StructureController structure in structures)
        {
            if (!IsValidStructure(structure))
                continue;

            if (structure.StructureType !=
                StructureType.DefenseLine)
            {
                continue;
            }

            _structureCandidates.Add(structure);
        }

        _structureCandidates.Sort(CompareStructures);

        return _structureCandidates.Count > 0
            ? _structureCandidates[0]
            : null;
    }

    private bool CollectUnitCandidates(
        IReadOnlyList<UnitController> enemies,
        StructureController defenseLine)
    {
        _unitCandidates.Clear();

        foreach (UnitController enemy in enemies)
        {
            if (!IsValidUnit(enemy))
                continue;

            if (GetForwardDistance(enemy) < 0f)
                continue;

            if (!IsExposedBeforeDefenseLine(
                    enemy,
                    defenseLine))
            {
                continue;
            }

            _unitCandidates.Add(enemy);
        }

        _unitCandidates.Sort(CompareUnitCandidatesByX);

        return _unitCandidates.Count > 0;
    }

    private bool CollectRangedUnitCandidates(
        IReadOnlyList<UnitController> enemies,
        StructureController defenseLine)
    {
        _unitCandidates.Clear();

        foreach (UnitController enemy in enemies)
        {
            if (!IsValidUnit(enemy))
                continue;

            if (GetForwardDistance(enemy) < 0f)
                continue;

            if (!IsExposedBeforeDefenseLine(
                    enemy,
                    defenseLine))
            {
                continue;
            }

            if (!_combat.IsInAttackRange(enemy))
                continue;

            _unitCandidates.Add(enemy);
        }

        _unitCandidates.Sort(
            CompareUnitCandidatesByX);

        return _unitCandidates.Count > 0;
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

    private float GetForwardDistance(ICombatTarget target)
    {
        float offset =
            target.TargetTransform.position.x -
            _owner.transform.position.x;

        return _owner.Team == UnitTeam.Zombie
            ? offset
            : -offset;
    }

    private bool TryAssignUnit(
        out CombatTargetAssignment assignment)
    {
        assignment = default;

        int groupStart = 0;

        while (groupStart < _unitCandidates.Count)
        {
            float nearestX =
                GetForwardDistance(_unitCandidates[groupStart]);

            int groupEnd = groupStart + 1;

            while (groupEnd < _unitCandidates.Count)
            {
                float candidateX =
                    GetForwardDistance(_unitCandidates[groupEnd]);

                if (candidateX >
                    nearestX + frontLineTolerance)
                {
                    break;
                }

                groupEnd++;
            }

            SortFrontLineGroup(
                groupStart,
                groupEnd - groupStart);

            for (int i = groupStart; i < groupEnd; i++)
            {
                if (TryReserve(
                        _unitCandidates[i],
                        out assignment))
                {
                    return true;
                }
            }

            groupStart = groupEnd;
        }

        return false;
    }

    private void SortFrontLineGroup(
        int index,
        int count)
    {
        _unitCandidates.Sort(
            index,
            count,
            Comparer<UnitController>.Create(
                CompareWithinFrontLine));
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

    private UnitController SelectFirstFrontLineCandidate()
    {
        if (_unitCandidates.Count == 0)
            return null;

        float nearestX =
            GetForwardDistance(_unitCandidates[0]);

        int groupEnd = 1;

        while (groupEnd < _unitCandidates.Count)
        {
            float candidateX =
                GetForwardDistance(_unitCandidates[groupEnd]);

            if (candidateX >
                nearestX + frontLineTolerance)
            {
                break;
            }

            groupEnd++;
        }

        SortFrontLineGroup(0, groupEnd);

        return _unitCandidates[0];
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

    private StructureController FindRangedStructureTarget(
        StructureController defenseLine)
    {
        if (defenseLine != null)
        {
            return _combat.IsInAttackRange(defenseLine)
                ? defenseLine
                : null;
        }

        IReadOnlyList<StructureController> structures =
            _battleArea.GetEnemyStructures(_owner.Team);

        StructureType baseType =
            _owner.Team == UnitTeam.Zombie
                ? StructureType.HumanFortress
                : StructureType.ZombieCamp;

        return FindFirstRangedStructure(
            structures,
            baseType);
    }

    private StructureController FindFirstRangedStructure(
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

            if (!_combat.IsInAttackRange(structure))
                continue;

            _structureCandidates.Add(structure);
        }

        _structureCandidates.Sort(CompareStructures);

        return _structureCandidates.Count > 0
            ? _structureCandidates[0]
            : null;
    }

    private StructureController FindEnemyDefenseLine()
    {
        IReadOnlyList<StructureController> structures =
            _battleArea.GetEnemyStructures(_owner.Team);

        _structureCandidates.Clear();

        foreach (StructureController structure in structures)
        {
            if (!IsValidStructure(structure))
                continue;

            if (structure.StructureType !=
                StructureType.DefenseLine)
            {
                continue;
            }

            if (!IsAhead(structure))
                continue;

            _structureCandidates.Add(structure);
        }

        _structureCandidates.Sort(CompareStructures);

        return _structureCandidates.Count > 0
            ? _structureCandidates[0]
            : null;
    }

    private bool IsExposedBeforeDefenseLine(
        UnitController enemy,
        StructureController defenseLine)
    {
        if (defenseLine == null)
            return true;

        return GetForwardDistance(enemy) <
               GetForwardDistance(defenseLine);
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
