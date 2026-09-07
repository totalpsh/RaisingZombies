using System.Collections.Generic;
using UnityEngine;

public class RangedFormationSlots : MonoBehaviour
{
    [SerializeField, Range(1, 20)]
    private int slotCountPerTeam = 5;

    [SerializeField, Min(0f)]
    private float distanceFromFrontLine = 1f;

    [SerializeField, Min(0.1f)]
    private float yGap = 0.5f;

    private UnitController[] _zombieOwners;
    private UnitController[] _humanOwners;

    private readonly Dictionary<UnitController, int> _slotByUnit =
        new();

    private void Awake()
    {
        int count = Mathf.Max(1, slotCountPerTeam);

        _zombieOwners = new UnitController[count];
        _humanOwners = new UnitController[count];
    }

    public bool HasAvailableSlot(UnitController unit)
    {
        if (!CanUse(unit))
            return false;

        CleanInvalidReservations();

        if (_slotByUnit.ContainsKey(unit))
            return true;

        UnitController[] owners =
            GetOwners(unit.Team);

        for (int i = 0; i < owners.Length; i++)
        {
            if (owners[i] == null)
                return true;
        }

        return false;
    }

    public bool TryReserve(
        UnitController unit,
        out int slotIndex)
    {
        slotIndex = -1;

        if (!CanUse(unit))
            return false;

        CleanInvalidReservations();

        if (_slotByUnit.TryGetValue(
                unit,
                out slotIndex))
        {
            return true;
        }

        slotIndex =
            FindClosestAvailableSlot(unit);

        if (slotIndex < 0)
            return false;

        UnitController[] owners =
            GetOwners(unit.Team);

        owners[slotIndex] = unit;
        _slotByUnit.Add(unit, slotIndex);

        return true;
    }

    public bool TryGetPosition(
        UnitController unit,
        float frontLineX,
        out Vector3 position)
    {
        position = transform.position;

        if (!CanUse(unit))
            return false;

        CleanInvalidReservations();

        if (!_slotByUnit.TryGetValue(
                unit,
                out int slotIndex))
        {
            return false;
        }

        position = CalculatePosition(
            unit.Team,
            slotIndex,
            frontLineX);

        return true;
    }

    public void Release(UnitController unit)
    {
        if (unit == null)
            return;

        if (!_slotByUnit.Remove(
                unit,
                out int slotIndex))
        {
            return;
        }

        UnitController[] owners =
            GetOwners(unit.Team);

        if (slotIndex < 0 ||
            slotIndex >= owners.Length)
        {
            return;
        }

        if (owners[slotIndex] == unit)
            owners[slotIndex] = null;
    }

    private bool CanUse(UnitController unit)
    {
        return unit != null &&
               isActiveAndEnabled;
    }

    private int FindClosestAvailableSlot(
        UnitController unit)
    {
        UnitController[] owners =
            GetOwners(unit.Team);

        int closestSlot = -1;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < owners.Length; i++)
        {
            if (owners[i] != null)
                continue;

            float slotY =
                transform.position.y +
                GetSignedIndex(i) * yGap;

            float distance = Mathf.Abs(
                slotY -
                unit.transform.position.y);

            if (distance >= closestDistance)
                continue;

            closestSlot = i;
            closestDistance = distance;
        }

        return closestSlot;
    }

    private Vector3 CalculatePosition(
        UnitTeam team,
        int slotIndex,
        float frontLineX)
    {
        float rearDirection =
            team == UnitTeam.Zombie
                ? -1f
                : 1f;

        int signedIndex =
            GetSignedIndex(slotIndex);

        return new Vector3(
            frontLineX +
            rearDirection * distanceFromFrontLine,
            transform.position.y +
            signedIndex * yGap,
            transform.position.z);
    }

    private UnitController[] GetOwners(UnitTeam team)
    {
        return team == UnitTeam.Zombie
            ? _zombieOwners
            : _humanOwners;
    }

    private static int GetSignedIndex(int index)
    {
        if (index == 0)
            return 0;

        int value = (index + 1) / 2;

        return index % 2 == 1
            ? value
            : -value;
    }

    private void CleanInvalidReservations()
    {
        CleanInvalidReservations(_zombieOwners);
        CleanInvalidReservations(_humanOwners);
    }

    private void CleanInvalidReservations(
        UnitController[] owners)
    {
        for (int i = 0; i < owners.Length; i++)
        {
            UnitController owner = owners[i];

            if (owner != null &&
                !owner.IsDead &&
                owner.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!ReferenceEquals(owner, null))
                _slotByUnit.Remove(owner);

            owners[i] = null;
        }
    }

    private void OnDisable()
    {
        if (_zombieOwners != null)
        {
            System.Array.Clear(
                _zombieOwners,
                0,
                _zombieOwners.Length);
        }

        if (_humanOwners != null)
        {
            System.Array.Clear(
                _humanOwners,
                0,
                _humanOwners.Length);
        }

        _slotByUnit.Clear();
    }
}
