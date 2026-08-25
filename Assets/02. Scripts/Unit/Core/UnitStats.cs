using UnityEngine;

public class UnitStats
{
    public float MaxHealth { get; }
    public float HealthRegen { get; }
    
    public float AttackPower { get; }
    public float AttackInterval { get; }
    public float AttackRange { get; }
    public float MoveSpeed { get; }

    // 실제 좀비 생성과 전투력 UI가 동일한 Upgrade Snapshot으로 최종 스탯을 만들게 합니다.
    public static UnitStats CreateZombie(UnitData data, UpgradeManager upgradeManager)
    {
        if (data == null) return null;
        if (upgradeManager == null) return new UnitStats(data);

        UpgradeStatSnapshot healthUpgrade = upgradeManager.GetStatSnapshot(UpgradeStatType.Health); // 실제 최대 체력에 더할 최종 보너스
        UpgradeStatSnapshot attackUpgrade = upgradeManager.GetStatSnapshot(UpgradeStatType.Attack); // 실제 공격력에 더할 최종 보너스
        UpgradeStatSnapshot attackSpeedUpgrade = upgradeManager.GetStatSnapshot(UpgradeStatType.AttackSpeed); // 실제 공격 간격을 줄일 최종 보너스
        UpgradeStatSnapshot healthRegenUpgrade = upgradeManager.GetStatSnapshot(UpgradeStatType.Defense); // 현재 전투 코드에서 방어력으로 사용하는 초당 회복 보너스
        UpgradeStatSnapshot moveSpeedUpgrade = upgradeManager.GetStatSnapshot(UpgradeStatType.MoveSpeed); // 실제 이동속도에 곱할 최종 보너스
        return new UnitStats(data, healthUpgrade, attackUpgrade, attackSpeedUpgrade, healthRegenUpgrade, moveSpeedUpgrade);
    }

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
        HealthRegen = data.HealthRegen + healthRegenUpgrade.FinalBonus;

        AttackPower = data.AttackPower + attackUpgrade.FinalBonus;
        AttackInterval = CalculateAttackInterval(data.AttackInterval, attackSpeedUpgrade.FinalBonus);

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
    
    public UnitStats(
        float maxHealth,
        float healthRegen,
        float attackPower,
        float attackInterval,
        float attackRange,
        float moveSpeed)
    {
        MaxHealth = maxHealth;
        HealthRegen = healthRegen;
        AttackPower = attackPower;
        AttackInterval = attackInterval;
        AttackRange = attackRange;
        MoveSpeed = moveSpeed;
    }
    
    private static float CalculateAttackInterval(float baseInterval, float attackSpeedBonus)
    {
        return baseInterval / (1f + attackSpeedBonus);
    }
}
