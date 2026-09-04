using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "StageCatalogData",
    menuName = "Game/Stage/Stage Catalog")]
public class StageCatalogData : ScriptableObject
{
    [SerializeField] private StageProgressionData progression;
    [SerializeField] private List<StageTemplateData> templates;
    [SerializeField] private List<StageData> manualStages;
    [SerializeField] private List<StageRuleData> rules = new();

    public StageProgressionData Progression => progression;
    public List<StageTemplateData> Templates => templates;
    public List<StageData> ManualStages => manualStages;
    public List<StageRuleData> Rules => rules;

    public bool TryGetManualStage(int stageNumber, out StageData stageData)
    {
        stageData = null;

        if (manualStages == null)
            return false;

        foreach (StageData candidate in manualStages)
        {
            if (candidate == null)
                continue;

            if (candidate.StageNumber != stageNumber)
                continue;

            stageData = candidate;
            return true;
        }

        return false;
    }

    public List<StageTemplateData> GetAvailableTemplates(int stageNumber)
    {
        List<StageTemplateData> availableTemplates = new();

        if (templates == null)
            return availableTemplates;

        foreach (StageTemplateData template in templates)
        {
            if (template == null)
                continue;

            if (!template.CanUse(stageNumber))
                continue;

            availableTemplates.Add(template);
        }

        return availableTemplates;
    }
}