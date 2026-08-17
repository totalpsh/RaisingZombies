using System;
using UnityEngine;

[Serializable]
public class HumanFormationEntryData
{
    [SerializeField] private string humanKey;
    [SerializeField, Min(1)] private int count = 1;
    [SerializeField] private Vector3 spawnOffset;
    [SerializeField, Min(0f)] private float spawnSpacing = 0.5f;

    public string HumanKey => humanKey;
    public int Count => count;
    public Vector3 SpawnOffset => spawnOffset;
    public float SpawnSpacing => spawnSpacing;
}
