using UnityEngine;

public interface ICombatTarget
{
    UnitTeam Team { get; }
    bool IsDead { get; }
    Transform TargetTransform { get; }
    
    void TakeDamage(float damage);
}
