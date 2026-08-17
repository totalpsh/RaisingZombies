using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class HumanRepeatRuleData
{
    [SerializeField, Min(0f)] private float startDelay;
    [SerializeField, Min(0.1f)] private float repeatInterval = 10f;
    [SerializeField, Min(0)] private int repeatCount;
    [SerializeField] private List<HumanFormationEntryData> formation;

    public float StartDelay => startDelay;
    public float RepeatInterval => repeatInterval;
    public int RepeatCount => repeatCount;
    public bool IsInfinite => repeatCount == 0;
    public List<HumanFormationEntryData> Formation => formation;
}
