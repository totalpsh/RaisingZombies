using UnityEngine;

public class UnitStats
{
    public float MaxHealth { get; private set; }
    public float HealthRegen { get; private set; }
    
    public float AttackPower { get; private set; }
    public float AttackInterval { get; private set; }
    public float AttackRange { get; private set; }
    public float MoveSpeed { get; private set; }

    // 기존 호출 형식을 유지하며 구 가챠 대신 현재 장착 장비만 읽는다.
    public static UnitStats CreateZombie(UnitData data, UpgradeManager upgradeManager)
    {
        return CreateZombieWithEquipment(data, BodyEquipmentManager.HasInstance ? BodyEquipmentManager.Instance.CurrentModifiers : default);
    }

    // 전투 생성과 UI가 같은 장착 합산값으로 최종 스탯을 계산한다.
    public static UnitStats CreateZombieWithEquipment(UnitData data, EquipmentModifierSnapshot modifiers)
    {
        if (data == null) return null;
        UnitStats stats = new UnitStats(data); // 기본 전투 수치를 보존할 결과
        stats.ApplyZombieEquipment(data, modifiers);
        return stats;
    }

    // 매번 기본 데이터에서 재계산해 캐시에 보너스가 중복 누적되지 않게 한다.
    public void ApplyZombieEquipment(UnitData data, EquipmentModifierSnapshot modifiers)
    {
        if (data == null) return;
        MaxHealth = (data.MaxHealth + modifiers.Health) * (1f + modifiers.HealthPercent / 100f);
        HealthRegen = data.HealthRegen + modifiers.HealthRegen;
        AttackPower = (data.AttackPower + modifiers.Attack) * (1f + modifiers.DamagePercent / 100f);
        AttackInterval = CalculateAttackInterval(data.AttackInterval, modifiers.AttackSpeedPercent / 100f);
        AttackRange = data.AttackRange;
        MoveSpeed = data.MoveSpeed * (1f + modifiers.MoveSpeedPercent / 100f);
    }

    // 구 Snapshot 생성자는 호환성만 유지하며 모든 구 가챠 인수를 무시한다.
    [System.Obsolete("구 스탯 가챠 보너스는 적용하지 않습니다. CreateZombie를 사용하세요.")]
    public UnitStats(UnitData data, 
        UpgradeStatSnapshot healthUpgrade,
        UpgradeStatSnapshot attackUpgrade,
        UpgradeStatSnapshot attackSpeedUpgrade,
        UpgradeStatSnapshot healthRegenUpgrade,
        UpgradeStatSnapshot moveSpeedUpgrade
    ) : this(data)
    {
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
    
    // 공격속도 증가율을 기존 공격 간격에 반영한다.
    private static float CalculateAttackInterval(float baseInterval, float attackSpeedBonus)
    {
        return baseInterval / (1f + attackSpeedBonus);
    }
}
