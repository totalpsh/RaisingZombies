using UnityEngine;

public class MeleeAttackAction : UnitAction
{
    public override UnitCombatType CombatType =>
        UnitCombatType.Melee;

    public override void Execute(
        UnitController owner,
        ICombatTarget target,
        float power)
    {
        if (target == null || target.IsDead)
            return;

        target.TakeDamage(power);
    }
}
