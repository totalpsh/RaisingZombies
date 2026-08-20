using UnityEngine;

[CreateAssetMenu(fileName = "StageProgressionData", menuName = "Game/Stage/Stage Progression")]
public class StageProgressionData : ScriptableObject
{
    [SerializeField, Min(1)] private int stagesPerGrowthStep = 5;
    [SerializeField, Min(1f)] private float healthGrowth = 1.15f;
    [SerializeField, Min(1f)] private float attackGrowth = 1.1f;
    [SerializeField, Min(0f)] private float moveSpeedGrowth = 0.02f;
    [SerializeField, Min(1f)] private float maximumMoveSpeedMultiplier = 1.3f;
    [SerializeField, Min(0f)] private float attackSpeedGrowth = 0.02f;
    [SerializeField, Min(1f)] private float maximumAttackSpeedMultiplier = 1.5f;
    [SerializeField, Min(0)] private int populationGrowthPerStep = 1;
    [SerializeField, Min(0)] private int maximumAdditionalPopulation = 10;
    
    
    public StageDifficultyData CreateDifficulty(int stageNumber)
    {
        int safeStageNumber = Mathf.Max(1, stageNumber);
        int growthStep = (safeStageNumber - 1) / stagesPerGrowthStep;
        float healthMultiplier = Mathf.Pow(healthGrowth, growthStep);
        float attackMultiplier = Mathf.Pow(attackGrowth, growthStep);
        float moveSpeedMultiplier = Mathf.Min(1f + moveSpeedGrowth * growthStep, maximumMoveSpeedMultiplier);
        float attackSpeedMultiplier = Mathf.Min(1f + attackSpeedGrowth * growthStep, maximumAttackSpeedMultiplier);

        return new StageDifficultyData(
            healthMultiplier,
            attackMultiplier,
            moveSpeedMultiplier,
            attackSpeedMultiplier);
    }
    
    public int GetAdditionalPopulation(int stageNumber)
    {
        int safeStageNumber = Mathf.Max(1, stageNumber);
        int growthStep = (safeStageNumber - 1) / stagesPerGrowthStep;
        int additionalPopulation = growthStep * populationGrowthPerStep;
        return Mathf.Min(additionalPopulation, maximumAdditionalPopulation);
    }
}