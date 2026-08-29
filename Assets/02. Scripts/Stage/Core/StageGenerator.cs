using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

public class StageGenerator
{
    private readonly StageCatalogData _catalog;

    public StageGenerator(StageCatalogData catalog)
    {
        _catalog = catalog;
    }

    public StageRuntimeData Generate(int stageNumber)
    {
        if (_catalog == null)
        {
            Debug.LogError("[StageGenerator] StageCatalogData가 없습니다.");

            return null;
        }
        
        int safeStageNumber = Mathf.Max(1, stageNumber);

        if (_catalog.TryGetManualStage(safeStageNumber, out StageData manualStage))
        {
            Debug.Log(
                $"[StageGenerator] Stage {safeStageNumber} " +
                $"수동 데이터 사용: {manualStage.name}");
            
            return CreateFromManualStage(manualStage);
        }

        return CreateFromTemplate(safeStageNumber);
    }

    private StageRuntimeData CreateFromManualStage(StageData stageData)
    {
        int stageNumber = stageData.StageNumber;

        StageProgressionData progression = _catalog.Progression;
        StageDifficultyData progressionDifficulty = progression != null ? progression.CreateDifficulty(stageNumber) : new StageDifficultyData();
        StageDifficultyData finalDifficulty = StageDifficultyData.Combine(progressionDifficulty, stageData.Difficulty);

        int additionalPopulation = progression != null ? progression.GetAdditionalPopulation(stageNumber) : 0;

        StageHumanDeploymentData deployment = stageData.HumanDeployment != null ? stageData.HumanDeployment.CreateScaled(additionalPopulation) : new StageHumanDeploymentData();

        return new StageRuntimeData(
            stageNumber,
            finalDifficulty,
            stageData.Defenses,
            deployment);
    }

    private StageRuntimeData CreateFromTemplate(int stageNumber)
    {
        List<StageTemplateData> availableTemplates = _catalog.GetAvailableTemplates(stageNumber);

        if (availableTemplates.Count == 0)
        {
            Debug.LogError($"[StageGenerator] Stage {stageNumber}에서 " + "사용 가능한 템플릿이 없습니다.");
            return null;
        }

        StageTemplateData selectedTemplate = SelectTemplate(availableTemplates, stageNumber);

        Debug.Log(
            $"[StageGenerator] Stage {stageNumber} " +
            $"자동 템플릿 사용: {selectedTemplate.TemplateId}");
        
        StageDifficultyData difficulty = CreateDifficulty(stageNumber);
        StageHumanDeploymentData deployment = CreateHumanDeployment(selectedTemplate, stageNumber);
        
        ApplyRules(stageNumber, deployment);

        return new StageRuntimeData(
            stageNumber,
            difficulty,
            selectedTemplate.Defenses,
            deployment);
    }

    private StageDifficultyData CreateDifficulty(int stageNumber)
    {
        StageProgressionData progression = _catalog.Progression;

        if (progression == null)
        {
            Debug.LogWarning("[StageGenerator] StageProgressionData가 없어 " + "기본 난이도를 사용합니다.");
            return new StageDifficultyData();
        }

        return progression.CreateDifficulty(stageNumber);
    }

    private StageTemplateData SelectTemplate(IReadOnlyList<StageTemplateData> templates, int stageNumber)
    {
        int totalWeight = 0;

        foreach (StageTemplateData template in templates)
        {
            totalWeight += template.SelectionWeight;
        }

        // 같은 스테이지는 재도전해도 같은 템플릿이 선택된다.
        int seed = unchecked(stageNumber * 73856093);
        Random random = new(seed);

        int randomValue = random.Next(totalWeight);

        int accumulatedWeight = 0;

        foreach (StageTemplateData template in templates)
        {
            accumulatedWeight += template.SelectionWeight;

            if (randomValue < accumulatedWeight)
                return template;
        }

        return templates[templates.Count - 1];
    }
    
    private StageHumanDeploymentData CreateHumanDeployment(StageTemplateData template, int stageNumber)
    {
        if (template.HumanDeployment == null)
            return new StageHumanDeploymentData();

        StageProgressionData progression = _catalog.Progression;

        if (progression == null)
        {
            return template.HumanDeployment.CreateScaled(0);
        }

        int additionalPopulation = progression.GetAdditionalPopulation(stageNumber);

        return template.HumanDeployment.CreateScaled(additionalPopulation);
    }
    
    private void ApplyRules(int stage, StageHumanDeploymentData human)
    {
        if (human == null || _catalog.Rules == null)
            return;

        foreach (StageRuleData rule in _catalog.Rules)
        {
            if (rule != null && rule.Matches(stage))
                human.Merge(rule.Human);
        }
    }
}
