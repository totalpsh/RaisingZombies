public enum CombatTargetingStatus
{
    NoTarget,
    Assigned,
    Blocked
}

public readonly struct CombatTargetAssignment
{
    public ICombatTarget Target { get; }
    public CombatSlots Slots { get; }
    public int SlotIndex { get; }

    public bool IsAssigned =>
        Target != null &&
        Slots != null &&
        SlotIndex >= 0;

    public CombatTargetAssignment(
        ICombatTarget target,
        CombatSlots slots,
        int slotIndex)
    {
        Target = target;
        Slots = slots;
        SlotIndex = slotIndex;
    }
}
