using UnityEngine;

public abstract class UnitAction : MonoBehaviour
{
    public virtual bool CanTarget(UnitController owner, ICombatTarget target)
    {
        return owner != null &&
               target != null &&
               target.Team != owner.Team;
    }
    
    public abstract void Execute(UnitController owner, ICombatTarget target, float power);
}
