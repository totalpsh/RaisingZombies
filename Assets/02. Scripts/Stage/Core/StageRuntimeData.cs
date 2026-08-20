using System;
using System.Collections.Generic;

public class StageRuntimeData
{
    public int StageNumber { get; }
    public StageDifficultyData Difficulty { get; }
    public List<StageDefenseData> Defenses { get; }
    public StageHumanDeploymentData HumanDeployment { get; }

    public StageRuntimeData(
        int stageNumber,
        StageDifficultyData difficulty,
        List<StageDefenseData> defenses,
        StageHumanDeploymentData humanDeployment)
    {
        StageNumber = Math.Max(1, stageNumber);

        Difficulty = difficulty ?? new StageDifficultyData();

        Defenses = defenses ?? new List<StageDefenseData>();

        HumanDeployment = humanDeployment ?? new StageHumanDeploymentData();
    }
}
