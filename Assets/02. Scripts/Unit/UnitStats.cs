using UnityEngine;

public class UnitStats
{
    public float MaxHealth { get; }
    public float HealthRegen { get; }
    
    public float AttackPower { get; }
    public float AttackInterval { get; }
    public float AttackRange { get; }
    public float MoveSpeed { get; }

    // 좀비용
    public UnitStats(UnitData data, 
        UpgradeStatSnapshot healthUpgrade,
        UpgradeStatSnapshot attackUpgrade,
        UpgradeStatSnapshot attackSpeedUpgrade,
        UpgradeStatSnapshot healthRegenUpgrade,
        UpgradeStatSnapshot moveSpeedUpgrade
    )
    {
        MaxHealth = data.MaxHealth + healthUpgrade.FinalBonus;
        Debug.Log("추가된" + healthUpgrade.FinalBonus);
        HealthRegen = data.HealthRegen + healthRegenUpgrade.FinalBonus;

        AttackPower = data.AttackPower + attackUpgrade.FinalBonus;
        AttackInterval = CalculateAttackInterval(
            data.AttackInterval,
            attackSpeedUpgrade.FinalBonus);

        AttackRange = data.AttackRange;
        MoveSpeed = data.MoveSpeed * (1f + moveSpeedUpgrade.FinalBonus);
    }

    // 인간용
    public UnitStats(UnitData data)
    {
        MaxHealth = data.MaxHealth;
        HealthRegen = data.HealthRegen;

        AttackPower = data.AttackPower;
        AttackInterval = data.AttackInterval;
        AttackRange = data.AttackRange;

        MoveSpeed = data.MoveSpeed;
    }
    
    private static float CalculateAttackInterval(
        float baseInterval,
        float attackSpeedBonus)
    {
        return baseInterval / (1f + attackSpeedBonus);
    }
}
