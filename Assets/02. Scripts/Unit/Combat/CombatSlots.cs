using System.Collections.Generic;
using UnityEngine;

public class CombatSlots : MonoBehaviour
{
    public enum SlotShape
    {
        Arc,
        Front
    }
    
    [SerializeField, Range(1, 9)] private int slotCount = 5;
    [SerializeField, Min(0.1f)] private float radius = 0.6f;
    [SerializeField, Range(5f, 80f)] private float angleStep = 35f;
    [SerializeField] private SlotShape shape;
    [SerializeField, Min(0.1f)] private float yGap = 0.4f;
    
    private UnitController[] _owners;
    private readonly Dictionary<UnitController, int> _slotByUnit = new();
    
    private void Awake()
    {
        _owners = new UnitController[Mathf.Max(1, slotCount)];
    }

    public bool Reserve(UnitController unit, out Vector3 position)
    {
        position = transform.position;

        if (unit == null)
            return false;

        Clean();

        if (_slotByUnit.TryGetValue(unit, out int currentSlot))
        {
            position = GetPosition(unit.Team, currentSlot);
            return true;
        }

        int slot = FindClosestSlot(unit);

        if (slot < 0)
            return false;

        _owners[slot] = unit;
        _slotByUnit[unit] = slot;
        position = GetPosition(unit.Team, slot);

        return true;
    }

    public bool TryGetPosition(UnitController unit, out Vector3 position)
    {
        position = transform.position;

        if (unit == null || !_slotByUnit.TryGetValue(unit, out int slot))
            return false;

        position = GetPosition(unit.Team, slot);
        return true;
    }

    public void Release(UnitController unit)
    {
        if (unit == null || !_slotByUnit.Remove(unit, out int slot))
            return;

        if (slot >= 0 && slot < _owners.Length && _owners[slot] == unit)
            _owners[slot] = null;
    }

    private int FindClosestSlot(UnitController unit)
    {
        int result = -1;
        float nearest = float.MaxValue;

        for (int i = 0; i < _owners.Length; i++)
        {
            if (_owners[i] != null)
                continue;

            float distance = (GetPosition(unit.Team, i) - unit.transform.position).sqrMagnitude;

            if (distance >= nearest)
                continue;

            result = i;
            nearest = distance;
        }

        return result;
    }

    private Vector3 GetPosition(UnitTeam team, int index)
    {
        float side = team == UnitTeam.Zombie ? -1f : 1f;
        int signedIndex = GetSignedIndex(index);

        if (shape == SlotShape.Front)
        {
            Vector3 offset = new Vector3(side * radius, signedIndex * yGap, 0f);
            return transform.position + offset;
        }

        float baseAngle = team == UnitTeam.Zombie ? 180f : 0f;
        float angle = (baseAngle + signedIndex * angleStep) * Mathf.Deg2Rad;
        Vector3 arcOffset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;

        return transform.position + arcOffset;
    }

    private static int GetSignedIndex(int index)
    {
        if (index == 0)
            return 0;

        int value = (index + 1) / 2;
        return index % 2 == 1 ? value : -value;
    }

    private void Clean()
    {
        for (int i = 0; i < _owners.Length; i++)
        {
            UnitController owner = _owners[i];

            if (owner != null && !owner.IsDead)
                continue;

            if (owner != null)
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
            Gizmos.DrawWireSphere(GetPosition(UnitTeam.Zombie, i), 0.08f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(GetPosition(UnitTeam.Human, i), 0.08f);
        }
    }
#endif
}
