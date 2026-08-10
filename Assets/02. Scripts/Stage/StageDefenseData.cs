using System;
using UnityEngine;

[Serializable]
public class StageDefenseData
{
    [SerializeField] private bool enabled;
    [SerializeField] private string defenseKey;
    [SerializeField] private Vector3 spawnOffset;

    public bool Enabled => enabled;
    public string DefenseKey => defenseKey;
    public Vector3 SpawnPosition => spawnOffset;
}
