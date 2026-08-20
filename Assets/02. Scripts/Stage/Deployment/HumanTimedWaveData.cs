using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class HumanTimedWaveData
{
    [SerializeField, Min(0f)] private float triggerTime;
    [SerializeField] private List<HumanFormationEntryData> formation;

    public float TriggerTime => triggerTime;
    public List<HumanFormationEntryData> Formation => formation;
}
