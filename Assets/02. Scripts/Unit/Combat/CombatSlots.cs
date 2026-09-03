using System.Collections.Generic;
using UnityEngine;

public class CombatSlots : MonoBehaviour
{
    public enum SlotShape
    {
        Arc,
        Front
    }

    [SerializeField, Range(1, 9)]
    private int slotCount = 5;

    [SerializeField, Min(0.1f)]
    private float radius = 0.6f;

    [SerializeField, Range(5f, 80f)]
    private float angleStep = 35f;

    [SerializeField]
    private SlotShape shape;

    [SerializeField, Min(0.1f)]
    private float yGap = 0.4f;

    private UnitController[] _owners;

    private readonly Dictionary<UnitController, int> _slotByUnit =
        new();

    private void Awake()
    {
        _owners = new UnitController[Mathf.Max(1, slotCount)];
    }

    public bool HasAvailableSlot(UnitController unit)
    {
        if (unit == null || !isActiveAndEnabled)
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

        if (unit == null || !isActiveAndEnabled)
            return false;

        CleanInvalidReservations();

        if (_slotByUnit.TryGetValue(unit, out slotIndex))
            return true;

        slotIndex = FindClosestAvailableSlot(unit);

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

        if (unit == null)
            return false;

        CleanInvalidReservations();

        if (!_slotByUnit.TryGetValue(unit, out int slotIndex))
            return false;

        position = CalculatePosition(unit.Team, slotIndex);
        return true;
    }

    public void Release(UnitController unit)
    {
        if (unit == null)
            return;

        if (!_slotByUnit.Remove(unit, out int slotIndex))
            return;

        if (slotIndex < 0 || slotIndex >= _owners.Length)
            return;

        if (_owners[slotIndex] == unit)
            _owners[slotIndex] = null;
    }

    private int FindClosestAvailableSlot(UnitController unit)
    {
        int closestSlot = -1;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < _owners.Length; i++)
        {
            if (_owners[i] != null)
                continue;

            Vector3 position = CalculatePosition(unit.Team, i);
            float distance =
                (position - unit.transform.position).sqrMagnitude;

            if (distance >= closestDistance)
                continue;

            closestSlot = i;
            closestDistance = distance;
        }

        return closestSlot;
    }

    private Vector3 CalculatePosition(
        UnitTeam attackerTeam,
        int slotIndex)
    {
        int signedIndex = GetSignedIndex(slotIndex);

        if (shape == SlotShape.Front)
            return CalculateFrontPosition(attackerTeam, signedIndex);

        return CalculateArcPosition(attackerTeam, signedIndex);
    }

    private Vector3 CalculateFrontPosition(
        UnitTeam attackerTeam,
        int signedIndex)
    {
        float side =
            attackerTeam == UnitTeam.Zombie ? -1f : 1f;

        Vector3 offset = new(
            side * radius,
            signedIndex * yGap,
            0f);

        return transform.position + offset;
    }

    private Vector3 CalculateArcPosition(
        UnitTeam attackerTeam,
        int signedIndex)
    {
        float baseAngle =
            attackerTeam == UnitTeam.Zombie ? 180f : 0f;

        float angle =
            (baseAngle + signedIndex * angleStep) *
            Mathf.Deg2Rad;

        Vector3 offset = new(
            Mathf.Cos(angle) * radius,
            Mathf.Sin(angle) * radius,
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

            if (owner != null && !owner.IsDead)
                continue;

            if (!ReferenceEquals(owner, null))
                _slotByUnit.Remove(owner);

            _owners[i] = null;
        }
    }

    private void OnDisable()
    {
        if (_owners != null)
            System.Array.Clear(_owners, 0, _owners.Length);

        _slotByUnit.Clear();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        int count = Mathf.Max(1, slotCount);

        for (int i = 0; i < count; i++)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(
                CalculatePosition(UnitTeam.Zombie, i),
                0.08f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                CalculatePosition(UnitTeam.Human, i),
                0.08f);
        }
    }
#endif
}
