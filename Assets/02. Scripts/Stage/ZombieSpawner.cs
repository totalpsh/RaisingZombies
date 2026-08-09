using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class ZombieSpawner : MonoBehaviour
{
    [SerializeField] private string zombieKey;
    [SerializeField] private Transform spawnPoint;
    
    [SerializeField, Min(0.1f)] private float baseSpawnTime = 30f;

    private float _elapsedTime;
    private bool _isSpawning;

    public float SpawnTime => baseSpawnTime;
    public float SpawnProgress => Mathf.Clamp01(_elapsedTime / SpawnTime);

    private void Awake()
    {
        _ = PoolManager.Instance.PreLoadAsync(zombieKey, 10);
    }
    
    private void Start()
    {
        _ = SpawnAsync();
    }

    private void Update()
    {
        Debug.Log("Spawner Update");

        _elapsedTime += Time.deltaTime;

        if (_elapsedTime < baseSpawnTime)
            return;

        if (_isSpawning)
            return;

        _elapsedTime -= baseSpawnTime;
        _ = SpawnAsync();

    }
    
    public async Task SpawnAsync()
    {
        if (_isSpawning)
            return;

        _isSpawning = true;
        
        GameObject zombieObj = await PoolManager.Instance.GetAsync(zombieKey, activateOnGet: false);
        UnitController zombieController = zombieObj.GetComponent<UnitController>();
        
        if (zombieObj == null)
        {
            Debug.LogError("좀비 생성 안됨");
            return;
        }

        if (!zombieObj.TryGetComponent(out UnitController zombieController2))
        {
            Debug.LogError("zombieController가 없음");
            PoolManager.Instance.Release(zombieObj);
            return;
        }
        
        zombieObj.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        UnitInitialize(zombieController);
        
        zombieObj.SetActive(true);
        
        _isSpawning = false;
    }

    private void UnitInitialize(UnitController controller)
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
}
