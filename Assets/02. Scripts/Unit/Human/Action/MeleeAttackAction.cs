using UnityEngine;

public class MeleeAttackAction : UnitAction
{
    public override void Execute(UnitController owner, ICombatTarget target, float power)
    {
        if (target == null || target.IsDead)
            return;

        target.TakeDamage(power);
    }
}
