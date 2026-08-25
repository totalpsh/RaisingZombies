using UnityEngine;

public abstract class UnitAction : MonoBehaviour
{
    public virtual bool RequiresTargetAhead => true;
    
    public virtual bool CanTarget(UnitController owner, ICombatTarget target)
    {
        return target != null && target.Team != owner.Team;
    }
    
    public abstract void Execute(UnitController owner, ICombatTarget target, float power);
}
