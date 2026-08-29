using System;
using UnityEngine;

public enum StageRuleType
{
    Exact,
    Every,
    Range
}

[Serializable]
public class StageRuleData
{
    [SerializeField] private StageRuleType type;
    [SerializeField, Min(1)] private int startStage = 1;
    [SerializeField, Min(1)] private int endStage = 1;
    [SerializeField, Min(1)] private int interval = 5;
    [SerializeField] private StageHumanDeploymentData human;

    public StageHumanDeploymentData Human => human;

    public bool Matches(int stage)
    {
        if (stage < 1)
            return false;

        return type switch
        {
            StageRuleType.Exact => stage == startStage,
            StageRuleType.Every => stage >= startStage && (stage - startStage) % interval == 0,
            StageRuleType.Range => stage >= startStage && stage <= endStage,
            _ => false
        };
    }
}
