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
}
