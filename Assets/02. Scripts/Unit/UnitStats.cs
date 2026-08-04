using UnityEngine;

public readonly struct UnitStats
{
    public float MaxHealth { get; }
    public float HealthRegen { get; }
    public float AttackPower { get; }
    public float AttackInterval { get; }
    public float AttackRange { get; }
    public float MoveSpeed { get; }

    public UnitStats(UnitData data)
    {
        MaxHealth = data.MaxHealth;
        HealthRegen = data.HealthRegen;
        AttackPower = data.AttackPower;
        AttackInterval = data.AttackInterval;
        AttackRange = data.AttackRange;
        MoveSpeed = data.MoveSpeed;
    }
}
