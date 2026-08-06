using UnityEngine;

public sealed class UnitModel
{
    public UnitStats Stats { get; }

    public float CurrentHealth { get; private set; }
    public float AttackCooldown { get; private set; }

    public bool IsDead => CurrentHealth <= 0f;
    public bool CanAttack => !IsDead && AttackCooldown <= 0f;

    public UnitModel(UnitStats stats)
    {
        Stats = stats;

        CurrentHealth = stats.MaxHealth;
        AttackCooldown = 0f;
    }

    public void TakeDamage(float damage)
    {
        if (IsDead)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        Debug.Log(CurrentHealth);
    }

    public void Heal(float amount)
    {
        if (IsDead)
            return;

        CurrentHealth = Mathf.Min(Stats.MaxHealth, CurrentHealth + amount);
    }

    public void TickAttackCooldown(float deltaTime)
    {
        if (deltaTime <= 0f || AttackCooldown <= 0f) return;
        
        AttackCooldown = Mathf.Max(0f, AttackCooldown - deltaTime);
    }

    public void ResetAttackCooldown()
    {
        AttackCooldown = Stats.AttackInterval;
    }
}
