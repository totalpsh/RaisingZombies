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
    [SerializeField, Min(1)] private int stageNumber;
    [SerializeField] private StageDifficultyData difficulty;
    [SerializeField] private List<StageDefenseData> defenses;
    [SerializeField] private StageHumanDeploymentData humanDeployment;
    
    public int StageNumber => stageNumber;
    public StageDifficultyData Difficulty => difficulty;
    public List<StageDefenseData> Defenses => defenses;
    public StageHumanDeploymentData HumanDeployment => humanDeployment;
}
