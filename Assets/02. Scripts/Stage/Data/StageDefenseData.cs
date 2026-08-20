using System;
using UnityEngine;

[Serializable]
public class StageDefenseData
{
    [SerializeField] private StructureController prefab;
    [SerializeField] private Vector3 spawnOffset;
    [SerializeField, Min(0.01f)] private float healthMultiplier = 1f;

    public StructureController Prefab => prefab;
    public Vector3 SpawnOffset => spawnOffset;
    public float HealthMultiplier => healthMultiplier;
}
