using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    [SerializeField] private string zombieKey;
    [SerializeField] private Transform spawnPoint;
    [SerializeField, Min(0.1f)] private float baseSpawnTime = 30f;

    private List<UnitController> _spawnedZombies = new();
    private float _elapsedTime;
    private bool _isSpawning;
    private bool _isProducing;

    public IReadOnlyCollection<UnitController> SpawnedZombies => _spawnedZombies;
    
    public float SpawnTime => baseSpawnTime;
    public float SpawnProgress => Mathf.Clamp01(_elapsedTime / SpawnTime);

    private void Awake()
    {
        _ = PoolManager.Instance.PreLoadAsync(zombieKey, 10);
    }
    
    private void Start()
    {
        StartProduction();
    }

    private void Update()
    {
        Debug.Log("Spawner Update");

        if (!_isProducing)
            return;
        
        _elapsedTime += Time.deltaTime;

        if (_elapsedTime < SpawnTime || _isSpawning)
            return;

        if (_isSpawning)
            return;

        _elapsedTime -= SpawnTime;
        _ = SpawnAsync();
    }
    
    public void StartProduction()
    {
        if (_isProducing)
            return;

        _isProducing = true;
        _elapsedTime = 0f;

        // 스테이지 시작 즉시 한 마리 생산
        _ = SpawnAsync();
    }

    public void StopProduction()
    {
        _isProducing = false;
        _elapsedTime = 0f;
    }
    
    public async Task SpawnAsync()
    {
        if (_isSpawning)
            return;

        _isSpawning = true;
        
        GameObject zombieObj = await PoolManager.Instance.GetAsync(zombieKey, activateOnGet: false);
        
        if (zombieObj == null)
        {
            Debug.LogError("좀비 생성 안됨");
            return;
        }

        if (!_isProducing)
        {
            PoolManager.Instance.Release(zombieObj);
            return;
        }
        
        if (!zombieObj.TryGetComponent(out UnitController zombieController))
        {
            Debug.LogError("zombieController가 없음");
            PoolManager.Instance.Release(zombieObj);
            return;
        }
        
        zombieObj.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        zombieObj.SetActive(true);
        InitializeZombie(zombieController);
        zombieController.Died += HandleZombieDied;
        _spawnedZombies.Add(zombieController);
        
        _isSpawning = false;
    }

    // 좀비 초기화
    private void InitializeZombie(UnitController controller)
    {
        UpgradeStatSnapshot healthSnapshot = UpgradeManager.Instance.GetStatSnapshot(UpgradeStatType.Health);
        UpgradeStatSnapshot attackSnapshot = UpgradeManager.Instance.GetStatSnapshot(UpgradeStatType.Attack);
        UpgradeStatSnapshot attackSpeedSnapshot = UpgradeManager.Instance.GetStatSnapshot(UpgradeStatType.AttackSpeed);
        UpgradeStatSnapshot regenSnapshot = UpgradeManager.Instance.GetStatSnapshot(UpgradeStatType.Defense);
        UpgradeStatSnapshot moveSpeedSnapshot = UpgradeManager.Instance.GetStatSnapshot(UpgradeStatType.MoveSpeed);
        
        UnitStats stats = new UnitStats
        (
            controller.Data,
            healthSnapshot,
            attackSnapshot,
            attackSpeedSnapshot,
            regenSnapshot,
            moveSpeedSnapshot
        );
        
        controller.Initialize(controller.Data, stats);
    }
    
    public void SetSpawnOrigin(Transform spawnOrigin)
    {
        spawnPoint = spawnOrigin;
    }
    
    // 좀비 사망시 리스트에서 처리
    private void HandleZombieDied(UnitController zombie)
    {
        zombie.Died -= HandleZombieDied;
        _spawnedZombies.Remove(zombie);
    }
    
    // 스테이지 전환 시 남은 좀비 정리
    // 페이드 연출 이후 호출로 변경할 예정
    public void ReleaseAllZombies()
    {
        UnitController[] zombies = _spawnedZombies.ToArray();

        _spawnedZombies.Clear();

        foreach (UnitController zombie in zombies)
        {
            if (zombie == null)
                continue;

            zombie.Died -= HandleZombieDied;
            PoolManager.Instance.Release(zombie.gameObject);
        }
    }
}
