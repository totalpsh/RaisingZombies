using System;
using System.Text;

// 신체 장비 원본과 인스턴스를 UI 문자열로 변환합니다.
public static class BodyEquipmentUIFormatter
{
    // 장비 인스턴스에서 레어도와 장비 원형을 함께 조회합니다.
    public static bool TryGetDefinitions(BodyEquipmentManager manager, BodyEquipmentInstance equipment,
        out BodyEquipmentDefinitionSO definition, out BodyRarityDefinitionSO rarity)
    {
        definition = null;
        rarity = null;
        return manager != null && equipment != null && manager.Database != null &&
               manager.Database.TryGetEquipment(equipment.definitionId, out definition) &&
               manager.Database.TryGetRarity(equipment.rarityTier, out rarity);
    }

    // 스탯 Roll을 한국어 표시명과 Inspector 형식으로 변환합니다.
    public static string FormatStat(BodyEquipmentManager manager, EquipmentStatRoll roll)
    {
        if (manager == null || roll == null || manager.Database == null ||
            !manager.Database.TryGetStat(roll.statType, out EquipmentStatDefinitionSO stat)) return "-";
        string value; // Inspector에서 지정한 수치 형식을 적용한 결과
        try { value = string.Format(stat.UiFormat, roll.value); }
        catch (FormatException) { value = roll.value.ToString("0.##"); }
        if (stat.ValueKind == EquipmentStatValueKind.Percent && !value.Contains("%", StringComparison.Ordinal)) value += "%";
        return $"{stat.DisplayName} {value}";
    }

    // 보조 스탯을 줄 단위 목록으로 만듭니다.
    public static string FormatSubStats(BodyEquipmentManager manager, BodyEquipmentInstance equipment)
    {
        if (equipment == null || equipment.subStats == null || equipment.subStats.Count == 0) return "보조 옵션 없음";
        StringBuilder builder = new(); // 한 카드 갱신에서만 재사용할 보조 옵션 문자열
        for (int index = 0; index < equipment.subStats.Count; index++)
        {
            if (index > 0) builder.AppendLine();
            builder.Append(FormatStat(manager, equipment.subStats[index]));
        }
        return builder.ToString();
    }

    // 같은 슬롯에 장착된 장비와 새 장비의 주 스탯 차이를 간단히 표시합니다.
    public static string FormatComparison(BodyEquipmentManager manager, BodyEquipmentInstance equipment)
    {
        if (!TryGetDefinitions(manager, equipment, out BodyEquipmentDefinitionSO definition, out _) || equipment.mainStat == null) return string.Empty;
        BodyEquipmentInstance equipped = manager.GetEquipped(definition.Slot); // 같은 슬롯의 현재 장착 장비
        if (equipped == null || equipped.uniqueId == equipment.uniqueId || equipped.mainStat == null) return string.Empty;
        if (equipped.mainStat.statType != equipment.mainStat.statType) return $"현재: {FormatStat(manager, equipped.mainStat)}";
        float difference = equipment.mainStat.value - equipped.mainStat.value; // 같은 주 스탯의 교체 증감량
        return $"현재: {FormatStat(manager, equipped.mainStat)}\n차이: {difference:+0.##;-0.##;0}";
    }

    // 초 단위 시간을 일·시·분·초 형태로 표시합니다.
    public static string FormatDuration(double seconds)
    {
        long totalSeconds = Math.Max(0L, (long)Math.Ceiling(seconds)); // UI에 표시할 음수가 아닌 남은 초
        TimeSpan duration = TimeSpan.FromSeconds(totalSeconds); // 읽기 쉬운 시간 단위 변환값
        if (duration.TotalDays >= 1d) return $"{(int)duration.TotalDays}일 {duration.Hours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
        return $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}";
    }

    // 0이 아닌 극소 확률도 0%로 오해되지 않게 표시합니다.
    public static string FormatProbability(float probability)
    {
        double percent = Math.Max(0d, probability) * 100d; // 0부터 100 사이로 표시할 실제 퍼센트
        if (percent <= 0d) return "0%";
        if (percent < 0.000001d) return "<0.000001%";
        if (percent < 0.01d) return percent.ToString("0.######") + "%";
        return percent.ToString("0.###") + "%";
    }
}
