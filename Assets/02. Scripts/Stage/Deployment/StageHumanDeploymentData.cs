using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StageHumanDeploymentData
{
    [SerializeField] private List<HumanPopulationData> populationData;
    [SerializeField] private List<HumanRepeatRuleData> repeatRules;
    [SerializeField] private List<HumanTimedWaveData> timedWaves;

    public List<HumanPopulationData> PopulationRules => populationData;
    public List<HumanRepeatRuleData> RepeatRules => repeatRules;
    public List<HumanTimedWaveData> TimedWaves => timedWaves;
    
    public StageHumanDeploymentData CreateScaled(int additionalPopulation)
    {
        StageHumanDeploymentData result = new StageHumanDeploymentData();
        result.populationData = new List<HumanPopulationData>();

        if (populationData != null)
        {
            foreach (HumanPopulationData population in populationData)
            {
                if (population == null)
                    continue;

                result.populationData.Add(population.CreateScaled(additionalPopulation));
            }
        }
        
        result.repeatRules = repeatRules != null ? new List<HumanRepeatRuleData>(repeatRules) : new List<HumanRepeatRuleData>();
        result.timedWaves = timedWaves != null ? new List<HumanTimedWaveData>(timedWaves) : new List<HumanTimedWaveData>();

        return result;
    }
    
    public void Merge(StageHumanDeploymentData other)
    {
        if (other == null)
            return;

        populationData ??= new List<HumanPopulationData>();
        repeatRules ??= new List<HumanRepeatRuleData>();
        timedWaves ??= new List<HumanTimedWaveData>();

        if (other.populationData != null)
            populationData.AddRange(other.populationData);

        if (other.repeatRules != null)
            repeatRules.AddRange(other.repeatRules);

        if (other.timedWaves != null)
            timedWaves.AddRange(other.timedWaves);
    }
}
