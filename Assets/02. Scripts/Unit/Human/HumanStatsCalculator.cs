using UnityEngine;

public static class HumanStatsCalculator
{
    public static UnitStats Calculate(UnitData data, int stageNumber, HumanScalingData scalingData, StageDifficultyData difficulty)
    {
        int stageIndex = Mathf.Max(0, stageNumber - 1);
        float healthGrowth = Mathf.Pow(1f + scalingData.HealthGrowthPerStage, stageIndex);
        float attackGrowth = Mathf.Pow(1f + scalingData.AttackGrowthPerStage, stageIndex);
        float maxHealth = data.MaxHealth * healthGrowth * difficulty.HealthMultiplier;
        float healthRegen = data.HealthRegen * healthGrowth * difficulty.HealthMultiplier;
        float attackPower = data.AttackPower * attackGrowth * difficulty.AttackMultiplier;
        float attackInterval = data.AttackInterval / difficulty.AttackSpeedMultiplier;
        float moveSpeed = data.MoveSpeed * difficulty.MoveSpeedMultiplier;

        return new UnitStats(maxHealth,
            healthRegen,
            attackPower,
            attackInterval,
            data.AttackRange,
            moveSpeed);
    }
}