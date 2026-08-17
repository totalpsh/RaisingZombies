using System;
using UnityEngine;

[Serializable]
public class HumanPopulationData
{
    [SerializeField] private string humanKey;                              // 생성할 인간 프리팹
    [SerializeField, Min(0)] private int initialCount;                     // 스테이지 시작시 배치 되는 수
    [SerializeField, Min(0)] private int targetCount;                      // 유지하려는 최대 생존 수
    [SerializeField, Min(1)] private int replenishCount = 1;               // 보충 주기마다 생성할 최대 수
    [SerializeField, Min(0.1f)] private float replenishInterval = 5f;      // 부족한 병력을 확인하고 보충하는 주기
    [SerializeField] private Vector3 spawnOffset;                          // 인간 요새의 SpawnPoint 보정
    [SerializeField, Min(0f)] private float spawnSpacing = 0.5f;           // 여러명을 동시에 생성할때의 간격

    public string HumanKey => humanKey;
    public int InitialCount => initialCount;
    public int TargetCount => targetCount;
    public int ReplenishCount => replenishCount;
    public float ReplenishInterval => replenishInterval;
    public Vector3 SpawnOffset => spawnOffset;
    public float SpawnSpacing => spawnSpacing;
}
