using System;
using UnityEngine;

// 장착된 신체 장비의 Flat과 Percent 스탯 합산 결과입니다.
public readonly struct EquipmentModifierSnapshot
{
    public readonly float Attack; // 고정 공격력 합
    public readonly float Health; // 고정 최대 체력 합
    public readonly float Defense; // 고정 방어력 합
    public readonly float AttackSpeedPercent; // 공격속도 증가 퍼센트 합
    public readonly float MoveSpeedPercent; // 이동속도 증가 퍼센트 합
    public readonly float LifeStealPercent; // 흡혈 퍼센트 합
    public readonly float HealthRegen; // 초당 체력 재생 합
    public readonly float DamagePercent; // 최종 공격력 증가 퍼센트 합
    public readonly float HealthPercent; // 최대 체력 증가 퍼센트 합
    public readonly float DefensePercent; // 방어력 증가 퍼센트 합
    public readonly float CriticalChancePercent; // 치명타 확률 퍼센트 합
    public readonly float CriticalDamagePercent; // 치명타 피해 퍼센트 합
    public readonly float DamageReductionPercent; // 받는 피해 감소 퍼센트 합

    // 안전하게 합산된 모든 장비 Modifier를 한 결과로 묶습니다.
    public EquipmentModifierSnapshot(float[] values)
    {
        Attack = Get(values, EquipmentStatType.Attack);
        Health = Get(values, EquipmentStatType.Health);
        Defense = Get(values, EquipmentStatType.Defense);
        AttackSpeedPercent = Get(values, EquipmentStatType.AttackSpeed);
        MoveSpeedPercent = Get(values, EquipmentStatType.MoveSpeed);
        LifeStealPercent = Get(values, EquipmentStatType.LifeSteal);
        HealthRegen = Get(values, EquipmentStatType.HealthRegen);
        DamagePercent = Get(values, EquipmentStatType.DamagePercent);
        HealthPercent = Get(values, EquipmentStatType.HealthPercent);
        DefensePercent = Get(values, EquipmentStatType.DefensePercent);
        CriticalChancePercent = Get(values, EquipmentStatType.CriticalChance);
        CriticalDamagePercent = Get(values, EquipmentStatType.CriticalDamage);
        DamageReductionPercent = Get(values, EquipmentStatType.DamageReduction);
    }

    // 배열 범위와 비정상 값을 검사한 스탯 값을 반환합니다.
    private static float Get(float[] values, EquipmentStatType type)
    {
        int index = (int)type; // Enum과 일치하는 합산 배열 위치
        if (values == null || index < 0 || index >= values.Length) return 0f;
        float value = values[index]; // 검사할 실제 합산값
        return float.IsNaN(value) || float.IsInfinity(value) || value < 0f ? 0f : value;
    }
}

// UI가 아닌 장비 원본에서 장착 스탯을 한 번 합산합니다.
public static class EquipmentStatAggregator
{
    // 현재 장착 ID와 Database 정의를 이용해 최종 Modifier Snapshot을 만듭니다.
    public static EquipmentModifierSnapshot Calculate(BodyEquipmentManager manager)
    {
        int statCount = Enum.GetValues(typeof(EquipmentStatType)).Length; // 지원하는 전체 스탯 개수
        float[] values = new float[statCount]; // 이번 장착 변경에만 사용할 합산 버퍼
        if (manager == null || manager.Database == null) return new EquipmentModifierSnapshot(values);
        foreach (BodyEquipmentSlot slot in Enum.GetValues(typeof(BodyEquipmentSlot))) // 슬롯마다 장착된 장비 한 개
        {
            BodyEquipmentInstance equipment = manager.GetEquipped(slot); // 합산할 장착 장비
            if (equipment == null) continue;
            AddRoll(values, manager.Database, equipment.mainStat);
            if (equipment.subStats == null) continue;
            foreach (EquipmentStatRoll roll in equipment.subStats) AddRoll(values, manager.Database, roll);
        }
        for (int index = 0; index < statCount; index++) // 스탯별 데이터 상한 적용
        {
            EquipmentStatType type = (EquipmentStatType)index; // 현재 상한을 조회할 스탯 종류
            if (!manager.Database.TryGetStat(type, out EquipmentStatDefinitionSO definition)) continue;
            if (definition.MaximumValue > 0f) values[index] = Mathf.Min(values[index], definition.MaximumValue);
        }
        return new EquipmentModifierSnapshot(values);
    }

    // 유효한 Roll 하나를 스탯 종류 배열에 더합니다.
    private static void AddRoll(float[] values, BodyEquipmentDatabaseSO database, EquipmentStatRoll roll)
    {
        if (roll == null || !database.TryGetStat(roll.statType, out _)) return;
        float value = roll.value; // NaN과 음수를 검사할 실제 Roll 값
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f) return;
        int index = (int)roll.statType; // Enum과 일치하는 합산 위치
        values[index] = Mathf.Min(float.MaxValue, values[index] + value);
    }
}
