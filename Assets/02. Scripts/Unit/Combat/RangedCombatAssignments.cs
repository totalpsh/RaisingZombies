public readonly struct RangedTargetResult
{
    public ICombatTarget Target { get; }

    public bool HasTarget =>
        Target != null;

    public RangedTargetResult(
        ICombatTarget target)
    {
        Target = target;
    }
}

public enum RangedPlacementType
{
    None,
    Defense,
    Formation
}

public readonly struct RangedPlacementAssignment
{
    public RangedPlacementType Type { get; }
    public DefenseSlots DefenseSlots { get; }
    public RangedFormationSlots FormationSlots { get; }
    public int SlotIndex { get; }

    public bool IsAssigned
    {
        get
        {
            if (SlotIndex < 0)
                return false;

            return Type switch
            {
                RangedPlacementType.Defense =>
                    DefenseSlots != null,

                RangedPlacementType.Formation =>
                    FormationSlots != null,

                _ => false
            };
        }
    }

    private RangedPlacementAssignment(
        RangedPlacementType type,
        DefenseSlots defenseSlots,
        RangedFormationSlots formationSlots,
        int slotIndex)
    {
        Type = type;
        DefenseSlots = defenseSlots;
        FormationSlots = formationSlots;
        SlotIndex = slotIndex;
    }

    public static RangedPlacementAssignment CreateDefense(
        DefenseSlots slots,
        int slotIndex)
    {
        if (slots == null || slotIndex < 0)
            return default;

        return new RangedPlacementAssignment(
            RangedPlacementType.Defense,
            slots,
            null,
            slotIndex);
    }

    public static RangedPlacementAssignment CreateFormation(
        RangedFormationSlots slots,
        int slotIndex)
    {
        if (slots == null || slotIndex < 0)
            return default;

        return new RangedPlacementAssignment(
            RangedPlacementType.Formation,
            null,
            slots,
            slotIndex);
    }
}
