using UnityEngine;

public interface ICombatTarget
{
    UnitTeam Team { get; }
    bool IsDead { get; }
    Transform TargetTransform { get; }
    Collider2D TargetCollider { get; }
    
    void TakeDamage(float damage);
}
