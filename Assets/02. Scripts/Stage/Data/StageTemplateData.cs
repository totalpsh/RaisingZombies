using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "StageTemplateData", menuName = "Game/Stage/Stage Template")]
public class StageTemplateData : ScriptableObject
{
    [SerializeField] private string templateId;
    [SerializeField, Min(1)] private int minimumStage = 1;
    [SerializeField, Min(0)] private int maximumStage;
    [SerializeField, Min(1)] private int selectionWeight = 1;
    [SerializeField] private List<StageDefenseData> defenses;
    [SerializeField] private StageHumanDeploymentData humanDeployment;

    public string TemplateId => templateId;
    public int MinimumStage => minimumStage;
    public int MaximumStage => maximumStage;
    public int SelectionWeight => selectionWeight;

    public List<StageDefenseData> Defenses => defenses;

    public StageHumanDeploymentData HumanDeployment => humanDeployment;

    public bool CanUse(int stageNumber)
    {
        if (stageNumber < minimumStage)
            return false;

        if (maximumStage > 0 && stageNumber > maximumStage)
            return false;
        

        return true;
    }
}
