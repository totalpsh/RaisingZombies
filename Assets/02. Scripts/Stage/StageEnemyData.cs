using System;
using UnityEngine;

[Serializable]
public class StageEnemyData
{
    [SerializeField] private string enemyKey;
    [SerializeField, Min(1)] private int count = 1;
    [SerializeField] private Vector3 spawnOffset;
    [SerializeField] private float spawnSpacing = 1f;
    
    public string EnemyKey => enemyKey;
    public int Count => count;
    public Vector3 SpawnPosition => spawnOffset;
    public float SpawnSpacing => spawnSpacing;
}
