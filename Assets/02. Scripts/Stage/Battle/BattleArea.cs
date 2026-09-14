using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

public class BattleArea : MonoBehaviour
{
    [Header("전장 영역")]
    [SerializeField] private Vector2 minBounds;
    [SerializeField] private Vector2 maxBounds;

    [Header("Y축 분산")]
    [SerializeField, Min(1)] private int yBandCount = 5;
    
    private readonly List<UnitController> _zombies = new();
    private readonly List<UnitController> _humans = new();
    private readonly Dictionary<UnitController, int> _unitBands = new();
    
    private readonly List<StructureController> _zombieStructures = new();
    private readonly List<StructureController> _humanStructures = new();

    private ReadOnlyCollection<UnitController> _zombieView;
    private ReadOnlyCollection<UnitController> _humanView;

    private ReadOnlyCollection<StructureController> _zombieStructureView;
    private ReadOnlyCollection<StructureController> _humanStructureView;
    
    public float RegisterUnit(UnitController unit)
    {
        if (unit == null)
            return GetBattleCenterY();

        if (_unitBands.TryGetValue(unit, out int assignedBand))
            return GetBandCenterY(assignedBand);

        List<UnitController> units = GetUnits(unit.Team);

        if (!units.Contains(unit))
            units.Add(unit);

        int bandIndex = SelectBand(unit.Team);
        _unitBands.Add(unit, bandIndex);

        return GetBandCenterY(bandIndex);
    }
    
    public void UnregisterUnit(UnitController unit)
    {
        if (unit == null)
            return;

        GetUnits(unit.Team).Remove(unit);
        _unitBands.Remove(unit);
    }

    private int SelectBand(UnitTeam team)
    {
        int bandCount = Mathf.Max(1, yBandCount);
        int[] occupancy = new int[bandCount];

        foreach (KeyValuePair<UnitController, int> assignment in _unitBands)
        {
            UnitController assignedUnit = assignment.Key;

            if (assignedUnit == null ||
                assignedUnit.Team != team ||
                assignment.Value < 0 ||
                assignment.Value >= bandCount)
            {
                continue;
            }

            occupancy[assignment.Value]++;
        }

        int selectedBand = 0;

        for (int bandIndex = 1; bandIndex < bandCount; bandIndex++)
        {
            if (occupancy[bandIndex] < occupancy[selectedBand] ||
                occupancy[bandIndex] == occupancy[selectedBand] &&
                IsCloserToCenter(bandIndex, selectedBand, bandCount))
            {
                selectedBand = bandIndex;
            }
        }

        return selectedBand;
    }

    private static bool IsCloserToCenter(
        int candidate,
        int current,
        int bandCount)
    {
        float centerIndex = (bandCount - 1) * 0.5f;
        float candidateDistance = Mathf.Abs(candidate - centerIndex);
        float currentDistance = Mathf.Abs(current - centerIndex);

        return candidateDistance < currentDistance;
    }

    private float GetBandCenterY(int bandIndex)
    {
        int bandCount = Mathf.Max(1, yBandCount);
        float bandHeight = (maxBounds.y - minBounds.y) / bandCount;

        return minBounds.y + bandHeight * (bandIndex + 0.5f);
    }

    private float GetBattleCenterY()
    {
        return (minBounds.y + maxBounds.y) * 0.5f;
    }

    public IReadOnlyList<UnitController> GetEnemyUnits(UnitTeam team)
    {
        return team == UnitTeam.Zombie ? GetHumanView() : GetZombieView();
    }

    public void RegisterStructure(StructureController structure)
    {
        if (structure == null)
            return;

        List<StructureController> structures = GetStructures(structure.Team);

        if (!structures.Contains(structure))
            structures.Add(structure);
    }
    
    public void UnregisterStructure(StructureController structure)
    {
        if (structure == null)
            return;

        GetStructures(structure.Team).Remove(structure);
    }
    
    public IReadOnlyList<StructureController> GetEnemyStructures(UnitTeam team)
    {
        return team == UnitTeam.Zombie ? GetHumanStructureView() : GetZombieStructureView();
    }

    public IReadOnlyList<StructureController> GetFriendlyStructures(
        UnitTeam team)
    {
        return team == UnitTeam.Zombie
            ? GetZombieStructureView()
            : GetHumanStructureView();
    }

    private List<UnitController> GetUnits(UnitTeam team)
    {
        return team == UnitTeam.Zombie ? _zombies : _humans;
    }
    
    private List<StructureController> GetStructures(UnitTeam team)
    {
        return team == UnitTeam.Zombie ? _zombieStructures : _humanStructures;
    }

    private IReadOnlyList<UnitController> GetZombieView()
    {
        return _zombieView ??= _zombies.AsReadOnly();
    }

    private IReadOnlyList<UnitController> GetHumanView()
    {
        return _humanView ??= _humans.AsReadOnly();
    }

    private IReadOnlyList<StructureController> GetZombieStructureView()
    {
        return _zombieStructureView ??= _zombieStructures.AsReadOnly();
    }

    private IReadOnlyList<StructureController> GetHumanStructureView()
    {
        return _humanStructureView ??= _humanStructures.AsReadOnly();
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
        Vector3 center = new((minBounds.x + maxBounds.x) * 0.5f, (minBounds.y + maxBounds.y) * 0.5f, transform.position.z);
        Vector3 size = new(maxBounds.x - minBounds.x, maxBounds.y - minBounds.y, 0f);

        Gizmos.DrawWireCube(center, size);
    }
#endif
}
