using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(StructureController))]
public class DefenseSlots : MonoBehaviour
{
    [SerializeField, Range(1, 20)]
    private int slotCount = 5;

    [SerializeField, Min(0f)]
    private float rearOffset = 0.6f;

    [SerializeField, Min(0.1f)]
    private float yGap = 0.5f;

    private StructureController _defenseLine;
    private UnitController[] _owners;

    private readonly Dictionary<UnitController, int> _slotByUnit =
        new();

    private void Awake()
    {
        _defenseLine =
            GetComponent<StructureController>();

        _owners =
            new UnitController[Mathf.Max(1, slotCount)];
    }

    public bool HasAvailableSlot(UnitController unit)
    {
        if (!CanUse(unit))
            return false;

        CleanInvalidReservations();

        if (_slotByUnit.ContainsKey(unit))
            return true;

        for (int i = 0; i < _owners.Length; i++)
        {
            if (_owners[i] == null)
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

        _owners[slotIndex] = unit;
        _slotByUnit.Add(unit, slotIndex);

        return true;
    }

    public bool TryGetPosition(
        UnitController unit,
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

        position = CalculatePosition(slotIndex);
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

        if (slotIndex < 0 ||
            slotIndex >= _owners.Length)
        {
            return;
        }

        if (_owners[slotIndex] == unit)
            _owners[slotIndex] = null;
    }

    private bool CanUse(UnitController unit)
    {
        return unit != null &&
               isActiveAndEnabled &&
               _defenseLine != null &&
               !_defenseLine.IsDead &&
               unit.Team == _defenseLine.Team;
    }

    private int FindClosestAvailableSlot(
        UnitController unit)
    {
        int closestSlot = -1;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < _owners.Length; i++)
        {
            if (_owners[i] != null)
                continue;

            Vector3 slotPosition =
                CalculatePosition(i);

            float distance =
                (slotPosition -
                 unit.transform.position)
                .sqrMagnitude;

            if (distance >= closestDistance)
                continue;

            closestSlot = i;
            closestDistance = distance;
        }

        return closestSlot;
    }

    private Vector3 CalculatePosition(
        int slotIndex)
    {
        float teamDirection =
            _defenseLine.Team == UnitTeam.Zombie
                ? -1f
                : 1f;

        int signedIndex =
            GetSignedIndex(slotIndex);

        Vector3 offset = new(
            teamDirection * rearOffset,
            signedIndex * yGap,
            0f);

        return transform.position + offset;
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
        for (int i = 0; i < _owners.Length; i++)
        {
            UnitController owner = _owners[i];

            if (owner != null &&
                !owner.IsDead &&
                owner.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!ReferenceEquals(owner, null))
                _slotByUnit.Remove(owner);

            _owners[i] = null;
        }
    }

    private void OnDisable()
    {
        if (_owners != null)
        {
            System.Array.Clear(
                _owners,
                0,
                _owners.Length);
        }

        _slotByUnit.Clear();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        StructureController defenseLine =
            _defenseLine != null
                ? _defenseLine
                : GetComponent<StructureController>();

        if (defenseLine == null)
            return;

        _defenseLine = defenseLine;

        int count = Mathf.Max(1, slotCount);

        Gizmos.color = Color.yellow;

        for (int i = 0; i < count; i++)
        {
            Gizmos.DrawWireSphere(
                CalculatePosition(i),
                0.08f);
        }
    }
#endif
}
