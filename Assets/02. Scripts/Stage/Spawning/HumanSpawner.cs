using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class HumanSpawner : MonoBehaviour
{
    [SerializeField] private HumanScalingData scalingData;
    [SerializeField] private List<RepeatRuntime> repeatRuntimes = new();
    [SerializeField] private List<TimedWaveRuntime> timedWaveRuntimes = new();
    
    private int _stageNumber;
    private StageDifficultyData _difficulty;
    
    private Transform _spawnOrigin;
    
    private List<PopulationRuntime> _populationRuntimes = new();
    private HashSet<UnitController> _spawnedHumans = new();
    private Dictionary<UnitController, PopulationRuntime> _populationOwnerByHuman = new();

    private bool _isProducing;
    private int _productionSession;
    
    public IReadOnlyCollection<UnitController> SpawnedHumans => _spawnedHumans;

    private void Update()
    {
        if (!_isProducing)
            return;

        float deltaTime = Time.deltaTime;

        foreach (PopulationRuntime runtime in _populationRuntimes)
            UpdatePopulationRule(runtime, deltaTime);
        
        foreach (RepeatRuntime runtime in repeatRuntimes)
            UpdateRepeatRule(runtime, deltaTime);
        
        foreach (TimedWaveRuntime runtime in timedWaveRuntimes)
            UpdateTimedWave(runtime, deltaTime);
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

        List<HumanPopulationData> populationRules = stageData.HumanDeployment.PopulationRules;
        List<HumanRepeatRuleData> repeatRules = stageData.HumanDeployment.RepeatRules;
        List<HumanTimedWaveData> timedWaves = stageData.HumanDeployment.TimedWaves;

        if (populationRules != null)
        {
            foreach (HumanPopulationData rule in populationRules)
            {
                if (!IsValidPopulationRule(rule))
                    continue;

                _populationRuntimes.Add(new PopulationRuntime(rule));
            }
        }
        
        if (repeatRules != null)
        {
            foreach (HumanRepeatRuleData rule in repeatRules)
            {
                if (!IsValidRepeatRule(rule))
                    continue;

                repeatRuntimes.Add(new RepeatRuntime(rule));
            }
        }
        
        if (timedWaves != null)
        {
            foreach (HumanTimedWaveData wave in timedWaves)
            {
                if (!IsValidTimedWave(wave))
                    continue;

                timedWaveRuntimes.Add(new TimedWaveRuntime(wave));
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
    
    private void UpdateRepeatRule(RepeatRuntime runtime, float deltaTime)
    {
        if (runtime.IsCompleted || runtime.IsSpawning)
            return;

        runtime.ElapsedTime += deltaTime;

        float requiredTime = runtime.SpawnedWaveCount == 0 ? runtime.Data.StartDelay : runtime.Data.RepeatInterval;

        if (runtime.ElapsedTime < requiredTime)
            return;

        runtime.ElapsedTime = 0f;
        runtime.IsSpawning = true;

        int session = _productionSession;

        _ = SpawnRepeatFormationAsync(runtime, session);
    }
    
    private void UpdateTimedWave(TimedWaveRuntime runtime, float deltaTime)
    {
        if (runtime.IsTriggered)
            return;

        runtime.ElapsedTime += deltaTime;

        if (runtime.ElapsedTime < runtime.Data.TriggerTime)
            return;

        runtime.IsTriggered = true;

        int session = _productionSession;

        _ = SpawnTimedWaveAsync(runtime, session);
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
                        PoolManager.Instance.Release(humanObject);

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
                Vector3 position = _spawnOrigin.position + runtime.Data.SpawnOffset + spacing;
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

    private async Task SpawnRepeatFormationAsync(RepeatRuntime runtime, int session)
    {
        try
        {
            foreach (HumanFormationEntryData entry in runtime.Data.Formation)
            {
                if (entry == null)
                    continue;

                for (int i = 0; i < entry.Count; i++)
                {
                    GameObject humanObject = await PoolManager.Instance.GetAsync(entry.HumanKey, activateOnGet: false);

                    if (!_isProducing || session != _productionSession)
                    {
                        if (humanObject != null)
                            PoolManager.Instance.Release(humanObject);

                        return;
                    }

                    if (humanObject == null)
                    {
                        Debug.LogError($"[HumanSpawner] {entry.HumanKey} 생성 실패");
                        continue;
                    }

                    if (!humanObject.TryGetComponent(out UnitController human))
                    {
                        Debug.LogError($"[HumanSpawner] {entry.HumanKey}에 UnitController가 없습니다.");
                        PoolManager.Instance.Release(humanObject);
                        continue;
                    }

                    Vector3 spacing = Vector3.right * (entry.SpawnSpacing * i);
                    Vector3 position = _spawnOrigin.position + entry.SpawnOffset + spacing;
                    humanObject.transform.SetPositionAndRotation(position, _spawnOrigin.rotation);
                    UnitStats stats = HumanStatsCalculator.Calculate(human.Data, _stageNumber, scalingData, _difficulty);
                    human.Initialize(human.Data, stats);
                    RegisterRepeatHuman(human);

                    humanObject.SetActive(true);
                }
            }

            runtime.SpawnedWaveCount++;

            if (!runtime.Data.IsInfinite &&
                runtime.SpawnedWaveCount >= runtime.Data.RepeatCount)
            {
                runtime.IsCompleted = true;
            }
        }
        finally
        {
            runtime.IsSpawning = false;
        }
    }
    
    private async Task SpawnTimedWaveAsync(TimedWaveRuntime runtime, int session)
    {
        foreach (HumanFormationEntryData entry in runtime.Data.Formation)
        {
            if (entry == null)
                continue;

            for (int i = 0; i < entry.Count; i++)
            {
                GameObject humanObject = await PoolManager.Instance.GetAsync(entry.HumanKey, activateOnGet: false);

                if (!_isProducing || session != _productionSession)
                {
                    if (humanObject != null)
                        PoolManager.Instance.Release(humanObject);

                    return;
                }

                if (humanObject == null)
                {
                    Debug.LogError($"[HumanSpawner] {entry.HumanKey} 생성 실패");
                    continue;
                }

                if (!humanObject.TryGetComponent(out UnitController human))
                {
                    Debug.LogError($"[HumanSpawner] {entry.HumanKey}에 UnitController가 없습니다.");
                    PoolManager.Instance.Release(humanObject);
                    continue;
                }

                Vector3 spacing = Vector3.right * (entry.SpawnSpacing * i);
                Vector3 position = _spawnOrigin.position + entry.SpawnOffset + spacing;
                humanObject.transform.SetPositionAndRotation(position, _spawnOrigin.rotation);
                UnitStats stats = HumanStatsCalculator.Calculate(human.Data, _stageNumber, scalingData, _difficulty);
                human.Initialize(human.Data, stats);
                RegisterSpawnedHuman(human);

                humanObject.SetActive(true);
            }
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
    
    private void RegisterRepeatHuman(UnitController human)
    {
        _spawnedHumans.Add(human);

        human.Died -= HandleHumanDied;
        human.Died += HandleHumanDied;
    }

    private void HandleHumanDied(UnitController human)
    {
        human.Died -= HandleHumanDied;

        _spawnedHumans.Remove(human);

        if (_populationOwnerByHuman.Remove(human, out PopulationRuntime runtime))
            runtime.Humans.Remove(human);
    }
    
    private void RegisterSpawnedHuman(
        UnitController human)
    {
        _spawnedHumans.Add(human);

        human.Died -= HandleHumanDied;
        human.Died += HandleHumanDied;
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
        repeatRuntimes.Clear();
        timedWaveRuntimes.Clear();
    }

    private static bool IsValidPopulationRule(HumanPopulationData rule)
    {
        return rule != null
               && !string.IsNullOrWhiteSpace(rule.HumanKey)
               && rule.TargetCount > 0
               && rule.InitialCount <= rule.TargetCount;
    }
    
    private static bool IsValidRepeatRule(HumanRepeatRuleData rule)
    {
        if (rule == null || rule.Formation == null || rule.Formation.Count == 0)
            return false;

        foreach (HumanFormationEntryData entry in rule.Formation)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.HumanKey) && entry.Count > 0)
                return true;
        }

        return false;
    }
    
    private static bool IsValidTimedWave(HumanTimedWaveData wave)
    {
        if (wave == null || wave.Formation == null || wave.Formation.Count == 0)
            return false;

        foreach (HumanFormationEntryData entry in wave.Formation)
        {
            if (entry != null && !string.IsNullOrWhiteSpace(entry.HumanKey) && entry.Count > 0)
                return true;
        }

        return false;
    }

    private sealed class PopulationRuntime
    {
        public HumanPopulationData Data { get; }

        public HashSet<UnitController> Humans { get; } = new();

        public float ElapsedTime;
        public bool IsSpawning;

        public PopulationRuntime(HumanPopulationData data)
        {
            Data = data;
        }
        
    }
    
    private sealed class RepeatRuntime
    {
        public HumanRepeatRuleData Data { get; }

        public float ElapsedTime;
        public int SpawnedWaveCount;
        public bool IsSpawning;
        public bool IsCompleted;

        public RepeatRuntime(HumanRepeatRuleData data)
        {
            Data = data;
        }
    }
    
    private sealed class TimedWaveRuntime
    {
        public HumanTimedWaveData Data { get; }

        public float ElapsedTime;
        public bool IsTriggered;

        public TimedWaveRuntime(HumanTimedWaveData data)
        {
            Data = data;
        }
    }
}
