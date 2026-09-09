using System;

// 전투력과 상세 패널이 함께 사용하는 현재 최종 전투 스탯 결과입니다.
public readonly struct CombatPowerSnapshot
{
    public readonly long CombatPower; // 정수로 안전하게 변환된 최종 전투력
    public readonly float Attack; // 실제 좀비가 사용하는 최종 1회 공격력
    public readonly float AttackSpeed; // 실제 공격 간격을 초당 공격 횟수로 변환한 값
    public readonly float MaxHealth; // 실제 좀비가 사용하는 최종 최대 체력
    public readonly float HealthRegen; // Defense 업그레이드가 실제 적용되는 초당 체력 회복량
    public readonly float Defense; // 신체 장비가 제공하는 실제 방어력
    public readonly float DamageReduction; // 신체 장비가 제공하는 0부터 1 사이 피해 감소율
    public readonly float MoveSpeed; // 실제 좀비가 사용하는 최종 이동속도
    public readonly float CriticalChance; // 실제 공격에 사용하는 0부터 1 사이 치명타 확률
    public readonly float CriticalDamageMultiplier; // 실제 공격에 사용하는 치명타 피해 배율
    public readonly float LifeSteal; // 실제 피해에서 회복하는 0부터 1 사이 흡혈 비율
    public readonly double OffenseRatio; // 기본 좀비 대비 기대 DPS 비율
    public readonly double DefenseRatio; // 기본 좀비 대비 유효 생존량 비율
    public readonly double ArmyRatio; // 실제 물량 시스템이 없어서 현재 중립값으로 유지되는 비율
    public readonly double MobilityRatio; // 기본 좀비 대비 이동속도 비율

    // 계산 완료된 전투력과 상세 표시값을 변경 불가능한 결과로 묶습니다.
    public CombatPowerSnapshot(
        long combatPower,
        float attack,
        float attackSpeed,
        float maxHealth,
        float healthRegen,
        float defense,
        float damageReduction,
        float moveSpeed,
        float criticalChance,
        float criticalDamageMultiplier,
        float lifeSteal,
        double offenseRatio,
        double defenseRatio,
        double armyRatio,
        double mobilityRatio)
    {
        CombatPower = combatPower;
        Attack = attack;
        AttackSpeed = attackSpeed;
        MaxHealth = maxHealth;
        HealthRegen = healthRegen;
        Defense = defense;
        DamageReduction = damageReduction;
        MoveSpeed = moveSpeed;
        CriticalChance = criticalChance;
        CriticalDamageMultiplier = criticalDamageMultiplier;
        LifeSteal = lifeSteal;
        OffenseRatio = offenseRatio;
        DefenseRatio = defenseRatio;
        ArmyRatio = armyRatio;
        MobilityRatio = mobilityRatio;
    }
}

// 실제 좀비 UnitStats를 기준 좀비와 비교해 표시 전용 전투력으로 환산합니다.
public static class CombatPowerCalculator
{
    private const double MinimumInterval = 0.0001d; // 0초 공격 간격으로 인한 무한 공격속도를 막는 최소값

    // 현재 UpgradeManager로 생성되는 실제 좀비 스탯을 전투력으로 계산합니다.
    public static CombatPowerSnapshot Calculate(
        UnitData zombieData,
        UpgradeManager upgradeManager,
        CombatPowerBalanceSettings balanceSettings)
    {
        BodyEquipmentManager equipmentManager = BodyEquipmentManager.HasInstance ? BodyEquipmentManager.Instance : null; // 현재 장착 장비 원본
        return Calculate(zombieData, equipmentManager, balanceSettings);
    }

    // 현재 신체 장비로 생성되는 실제 좀비 스탯을 전투력으로 계산합니다.
    public static CombatPowerSnapshot Calculate(
        UnitData zombieData,
        BodyEquipmentManager equipmentManager,
        CombatPowerBalanceSettings balanceSettings)
    {
        if (zombieData == null || balanceSettings == null) return default;

        UnitStats baseStats = new UnitStats(zombieData); // 정규화 기준으로 사용할 실제 좀비 기본 스탯
        UnitStats currentStats = UnitStats.CreateZombie(zombieData, equipmentManager); // 전투 생성 코드와 동일하게 만든 현재 최종 스탯
        double baseAttackSpeed = GetAttackSpeed(baseStats.AttackInterval); // 기본 초당 공격 횟수
        double currentAttackSpeed = GetAttackSpeed(currentStats.AttackInterval); // 현재 초당 공격 횟수
        double baseExpectedDps = GetExpectedDps(baseStats, baseAttackSpeed); // 치명타 기대값까지 포함한 기본 DPS
        double currentExpectedDps = GetExpectedDps(currentStats, currentAttackSpeed); // 장비 치명타 기대값까지 포함한 현재 DPS
        double sustainSeconds = SanitizeNonNegative(balanceSettings.DefenseSustainSeconds); // 체력 재생을 평가할 설정 시간
        double baseDefensePower = GetEffectiveDefensePower(baseStats, baseExpectedDps, sustainSeconds); // 기본 체력과 실제 방어 계열 기반 생존값
        double currentDefensePower = GetEffectiveDefensePower(currentStats, currentExpectedDps, sustainSeconds); // 장비 방어, 감소, 회복과 흡혈 기반 생존값
        double offenseRatio = GetSafeRatio(currentExpectedDps, baseExpectedDps); // 기본 대비 공격 성능 비율
        double defenseRatio = GetSafeRatio(currentDefensePower, baseDefensePower); // 기본 대비 생존 성능 비율
        double armyRatio = 1d; // 생산 수와 최대 수가 전투에 미적용이므로 중복 없이 유지할 중립 비율
        double mobilityRatio = GetSafeRatio(currentStats.MoveSpeed, baseStats.MoveSpeed); // 기본 대비 이동 성능 비율
        double combinedPower =
            Math.Pow(offenseRatio, SanitizeNonNegative(balanceSettings.OffenseWeight)) *
            Math.Pow(defenseRatio, SanitizeNonNegative(balanceSettings.DefenseWeight)) *
            Math.Pow(armyRatio, SanitizeNonNegative(balanceSettings.ArmyWeight)) *
            Math.Pow(mobilityRatio, SanitizeNonNegative(balanceSettings.MobilityWeight)); // 네 성능 비율을 가중 곱으로 합친 값
        long combatPower = ToSafeLong(SanitizeNonNegative(balanceSettings.BaseCombatPower) * SanitizeNonNegative(combinedPower)); // 오버플로를 막은 최종 표시 전투력

        return new CombatPowerSnapshot(
            combatPower,
            ToSafeFloat(currentStats.AttackPower),
            ToSafeFloat(currentAttackSpeed),
            ToSafeFloat(currentStats.MaxHealth),
            ToSafeFloat(currentStats.HealthRegen),
            ToSafeFloat(currentStats.Defense),
            ToSafeFloat(currentStats.DamageReduction),
            ToSafeFloat(currentStats.MoveSpeed),
            ToSafeFloat(currentStats.CriticalChance),
            ToSafeFloat(currentStats.CriticalDamageMultiplier),
            ToSafeFloat(currentStats.LifeSteal),
            offenseRatio,
            defenseRatio,
            armyRatio,
            mobilityRatio);
    }

    // 공격력, 공격속도와 치명타 기대값을 초당 피해로 환산합니다.
    private static double GetExpectedDps(UnitStats stats, double attackSpeed)
    {
        if (stats == null) return 0d;
        double chance = Math.Min(1d, SanitizeNonNegative(stats.CriticalChance)); // 안전한 치명타 확률
        double multiplier = Math.Max(1d, SanitizeNonNegative(stats.CriticalDamageMultiplier)); // 안전한 치명타 배율
        double criticalExpectation = 1d + chance * (multiplier - 1d); // 한 공격의 치명타 기대 배율
        return SanitizeNonNegative(stats.AttackPower) * SanitizeNonNegative(attackSpeed) * criticalExpectation;
    }

    // 체력, 재생, 흡혈, 방어력과 피해 감소를 같은 생존력 값으로 환산합니다.
    private static double GetEffectiveDefensePower(UnitStats stats, double expectedDps, double sustainSeconds)
    {
        if (stats == null) return 0d;
        double healing = (SanitizeNonNegative(stats.HealthRegen) + expectedDps * Math.Min(1d, SanitizeNonNegative(stats.LifeSteal))) * sustainSeconds; // 기준 시간의 재생과 흡혈량
        double rawHealth = SanitizeNonNegative(stats.MaxHealth) + healing; // 방어 적용 전 체력과 회복량
        double defenseConstant = Math.Max(1d, SanitizeNonNegative(stats.DefenseReductionConstant)); // 실제 UnitModel과 같은 방어 상수
        double defenseFactor = defenseConstant / (defenseConstant + SanitizeNonNegative(stats.Defense)); // 실제 UnitModel과 같은 방어 피해 배율
        double reductionFactor = 1d - Math.Min(1d, SanitizeNonNegative(stats.DamageReduction)); // 실제 UnitModel과 같은 추가 피해 배율
        double incomingFactor = Math.Max(0.000001d, defenseFactor * reductionFactor); // 0으로 나누지 않는 최종 받는 피해 배율
        return SanitizeNonNegative(rawHealth / incomingFactor);
    }

    // 실제 공격 간격을 초당 공격 횟수로 안전하게 변환합니다.
    private static double GetAttackSpeed(double attackInterval)
    {
        double safeInterval = SanitizeNonNegative(attackInterval); // NaN과 음수를 제거한 공격 간격
        return safeInterval <= MinimumInterval ? 0d : 1d / safeInterval;
    }

    // 기준값이 없을 때 0으로 나누지 않고 해당 성능을 중립 비율로 처리합니다.
    private static double GetSafeRatio(double current, double baseline)
    {
        double safeCurrent = SanitizeNonNegative(current); // 검증된 현재 성능값
        double safeBaseline = SanitizeNonNegative(baseline); // 검증된 기준 성능값
        if (safeBaseline <= 0d) return 1d;
        return SanitizeNonNegative(safeCurrent / safeBaseline);
    }

    // NaN, Infinity, 음수를 전투력 계산에 안전한 0으로 바꿉니다.
    private static double SanitizeNonNegative(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value) || value < 0d ? 0d : value;
    }

    // 상세 UI에 사용할 값을 유효한 float 범위로 변환합니다.
    private static float ToSafeFloat(double value)
    {
        double safeValue = SanitizeNonNegative(value); // float 변환 전에 검증한 상세 스탯 값
        return safeValue >= float.MaxValue ? float.MaxValue : (float)safeValue;
    }

    // 방치형 장기 성장값을 long 범위 안의 정수 전투력으로 변환합니다.
    private static long ToSafeLong(double value)
    {
        double safeValue = SanitizeNonNegative(value); // 반올림 전에 검증한 전투력 값
        if (safeValue >= long.MaxValue) return long.MaxValue;
        return (long)Math.Round(safeValue, MidpointRounding.AwayFromZero);
    }
}
