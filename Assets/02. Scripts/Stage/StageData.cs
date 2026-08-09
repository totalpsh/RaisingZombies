using System.Collections.Generic;
using UnityEngine;

public enum StageClearCondition
{
    DestroyDefense,  // 방어선 파괴
    DefeatAllEnemies, // 모든 인간 처치
    ClearAll          // 방어선과 인간 모두 제거
}

[CreateAssetMenu(fileName = "StageData", menuName = "Game/Stage/StageData")]
public class StageData : ScriptableObject
{
    [SerializeField] private int stageNumber;
    [SerializeField] private StageClearCondition clearCondition;
    // [SerializeField] private List<StageEnemyData> enemies;
    // [SerializeField] private StageDefenseData defenseData;
    
    public int StageNumber => stageNumber;
    public StageClearCondition ClearCondition => clearCondition;
    // public IReadOnlyList<StageEnemyData> Enemies => enemies;
    // public StageDefenseData DefenseData => defenseData;
}
