using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class HumanSpawner : MonoBehaviour
{
    [SerializeField] private HumanScalingData scalingData;

    private int _stageNumber;
    private StageDifficultyData _difficulty;
    
    private Transform _spawnOrigin;
    
    private List<PopulationRuntime> _populationRuntimes = new();
    private HashSet<UnitController> _spawnedHumans = new();
    private Dictionary<UnitController, PopulationRuntime> _populationOwnerByHuman = new();

    private bool _isProducing;
    private int _productionSession;

    private void Update()
    {
        if (!_isProducing)
            return;

        float deltaTime = Time.deltaTime;

        foreach (PopulationRuntime runtime in _populationRuntimes)
            UpdatePopulationRule(runtime, deltaTime);
    }

    public void SetSpawnOrigin(Transform spawnOrigin)
    {
        _spawnOrigin = spawnOrigin;
    }

    public void StartProduction(StageRuntimeData stageData)
    {
        StopProduction();
        ReleaseAllHumans();

        if (stageData == null || stageData.HumanDeployment == null || scalingData == null)
        {
            Debug.LogError("인간 배치 데이터가 엄서여");

            return;
        }

        if (_spawnOrigin == null)
        {
            Debug.LogError("SpawnOrigin이 엄서여");

            return;
        }
        
        _stageNumber = stageData.StageNumber;
        _difficulty = stageData.Difficulty;

        List<HumanPopulationData> rules = stageData.HumanDeployment.PopulationRules;

        if (rules != null)
        {
            foreach (HumanPopulationData rule in rules)
            {
                if (!IsValidPopulationRule(rule))
                    continue;

                _populationRuntimes.Add(new PopulationRuntime(rule));
            }
        }

        _isProducing = true;
        _productionSession++;

        int session = _productionSession;

        foreach (PopulationRuntime runtime in _populationRuntimes)
        {
            if (runtime.Data.InitialCount <= 0)
                continue;

            runtime.IsSpawning = true;

            _ = SpawnPopulationUnitsAsync(runtime, runtime.Data.InitialCount, session);
        }
    }

    public void StopProduction()
    {
        _isProducing = false;
        _productionSession++;
    }

    private void UpdatePopulationRule(PopulationRuntime runtime, float deltaTime)
    {
        if (runtime.IsSpawning)
            return;

        int currentCount = runtime.Humans.Count;

        if (currentCount >= runtime.Data.TargetCount)
        {
            runtime.ElapsedTime = 0f;
            return;
        }

        runtime.ElapsedTime += deltaTime;

        if (runtime.ElapsedTime < runtime.Data.ReplenishInterval)
            return;

        runtime.ElapsedTime = 0f;

        int missingCount = runtime.Data.TargetCount - currentCount;

        int spawnCount = Mathf.Min(missingCount, runtime.Data.ReplenishCount);

        runtime.IsSpawning = true;

        int session = _productionSession;

        _ = SpawnPopulationUnitsAsync(runtime, spawnCount, session);
    }

    private async Task SpawnPopulationUnitsAsync(PopulationRuntime runtime, int count, int session)
    {
        try
        {
            for (int i = 0; i < count; i++)
            {
                GameObject humanObject = await PoolManager.Instance.GetAsync(runtime.Data.HumanKey, activateOnGet: false);

                if (!_isProducing || session != _productionSession)
                {
                    if (humanObject != null)
                    {
                        PoolManager.Instance.Release(humanObject);
                    }

                    return;
                }

                if (humanObject == null)
                {
                    Debug.LogError($"{runtime.Data.HumanKey} 생성 실패입니다잉");

                    continue;
                }

                if (!humanObject.TryGetComponent(out UnitController human))
                {
                    Debug.LogError($"{runtime.Data.HumanKey}에 " + "UnitController가 엄서여");

                    PoolManager.Instance.Release(humanObject);
                    continue;
                }

                Vector3 spacing = Vector3.right * (runtime.Data.SpawnSpacing * i);

                Vector3 position =
                    _spawnOrigin.position
                    + runtime.Data.SpawnOffset
                    + spacing;

                humanObject.transform.SetPositionAndRotation(position, _spawnOrigin.rotation);
                
                humanObject.SetActive(true);
                
                UnitStats stats = HumanStatsCalculator.Calculate(human.Data, _stageNumber, scalingData, _difficulty);

                human.Initialize(human.Data, stats);

                RegisterPopulationHuman(human, runtime);
            }
        }
        finally
        {
            runtime.IsSpawning = false;
            runtime.ElapsedTime = 0f;
        }
    }

    private void RegisterPopulationHuman(UnitController human, PopulationRuntime runtime)
    {
        _spawnedHumans.Add(human);
        runtime.Humans.Add(human);

        _populationOwnerByHuman[human] = runtime;

        human.Died -= HandleHumanDied;
        human.Died += HandleHumanDied;
    }

    private void HandleHumanDied(UnitController human)
    {
        human.Died -= HandleHumanDied;

        _spawnedHumans.Remove(human);

        if (_populationOwnerByHuman.Remove(human,
                out PopulationRuntime runtime))
            runtime.Humans.Remove(human);
    }

    public void ReleaseAllHumans()
    {
        List<UnitController> humans = new(_spawnedHumans);

        foreach (UnitController human in humans)
        {
            if (human == null)
                continue;

            human.Died -= HandleHumanDied;

            if (human.gameObject.activeSelf)
                PoolManager.Instance.Release(human.gameObject);
        }

        _spawnedHumans.Clear();
        _populationOwnerByHuman.Clear();
        _populationRuntimes.Clear();
    }

    private static bool IsValidPopulationRule(HumanPopulationData rule)
    {
        return rule != null
               && !string.IsNullOrWhiteSpace(rule.HumanKey)
               && rule.TargetCount > 0
               && rule.InitialCount <= rule.TargetCount;
    }

    private sealed class PopulationRuntime
    {
        public HumanPopulationData Data { get; }

        public HashSet<UnitController> Humans { get; } =
            new();

        public float ElapsedTime;
        public bool IsSpawning;

        public PopulationRuntime(HumanPopulationData data)
        {
            Data = data;
        }
    }
}
