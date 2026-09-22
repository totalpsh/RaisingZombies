using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class BossDungeonManager : MonoBehaviour
{
    [SerializeField] private string bossKey;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private BattleArea battleArea;
    [SerializeField, Min(1f)] private float testMaxHealth = 1000f;
    [SerializeField] private string zombieKey;
    [SerializeField] private Transform zombieSpawnPoint;
    [SerializeField, Min(1)] private int testZombieCount = 5;
    [SerializeField, Min(0f)] private float zombieSpacing = 0.5f;

    private readonly List<UnitController> _spawnedZombies = new();
    private BossController _boss;
    private bool _isInitializing;
    private int _session;
    private bool _isBattleRunning;
    private bool _isReturningToMain;
    private bool _bossStoppedForResult;

    public BossDungeonResult Result { get; private set; }

    public async Task InitializeAsync()
    {
        if (_isInitializing || _boss != null)
            return;
        if (string.IsNullOrEmpty(bossKey) || spawnPoint == null || battleArea == null ||
            string.IsNullOrEmpty(zombieKey) || zombieSpawnPoint == null ||
            testZombieCount < 1 || zombieSpacing < 0f)
        {
            Debug.LogError("[BossDungeonManager] Boss/zombie keys, spawn points, BattleArea and valid deployment settings are required.", this);
            return;
        }

        PoolManager pool = PoolManager.Instance;
        if (pool == null)
            return;

        _isInitializing = true;
        Result = BossDungeonResult.None;
        _isBattleRunning = false;
        _bossStoppedForResult = false;
        int session = _session;
        GameObject bossObject = null;
        bool initialized = false;
        try
        {
            bossObject = await pool.GetAsync(bossKey, activateOnGet: false);
            if (this == null || !isActiveAndEnabled || session != _session)
                return;
            if (bossObject == null)
            {
                Debug.LogError("[BossDungeonManager] Boss creation failed.", this);
                return;
            }
            if (!bossObject.TryGetComponent(out BossController boss))
            {
                Debug.LogError("[BossDungeonManager] Boss prefab requires BossController.", this);
                return;
            }

            bossObject.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
            bossObject.SetActive(true);
            UnitStats stats = new(testMaxHealth, 0f, 0f, 1f, 0f, 0f);
            if (!boss.Initialize(stats, battleArea))
                return;

            _boss = boss;
            _boss.Died += HandleBossDied;
            initialized = true;
            await DeployZombiesAsync(pool, session);
        }
        finally
        {
            if (!initialized && bossObject != null && pool != null)
                pool.Release(bossObject);
            _isInitializing = false;
        }
    }

    private async Task DeployZombiesAsync(PoolManager pool, int session)
    {
        List<GameObject> preparedObjects = new();
        List<UnitController> preparedZombies = new();
        bool deployed = false;

        try
        {
            for (int i = 0; i < testZombieCount; i++)
            {
                GameObject zombieObject = await pool.GetAsync(zombieKey, activateOnGet: false);
                if (zombieObject != null)
                    preparedObjects.Add(zombieObject);

                if (this == null || !isActiveAndEnabled || session != _session)
                    return;

                if (zombieObject == null ||
                    !zombieObject.TryGetComponent(out UnitController zombie) ||
                    zombie.Data == null || zombie.Data.Team != UnitTeam.Zombie)
                {
                    Debug.LogError("[BossDungeonManager] Invalid zombie prefab or UnitData.", this);
                    return;
                }

                zombieObject.transform.SetPositionAndRotation(
                    zombieSpawnPoint.position + Vector3.left * zombieSpacing * i,
                    zombieSpawnPoint.rotation);
                preparedZombies.Add(zombie);
            }

            // No awaits here: all prepared zombies start in the same frame.
            foreach (UnitController zombie in preparedZombies)
            {
                UnitStats stats = UnitStats.CreateZombie(zombie.Data, UpgradeManager.Instance);
                zombie.gameObject.SetActive(true);
                zombie.Initialize(zombie.Data, stats, battleArea);
                if (zombie.IsDead)
                {
                    Debug.LogError("[BossDungeonManager] Zombie initialization failed.", this);
                    return;
                }

                zombie.Died += HandleZombieDied;
                _spawnedZombies.Add(zombie);
            }

            deployed = true;
            _isBattleRunning = true;
        }
        finally
        {
            if (!deployed)
            {
                foreach (UnitController zombie in preparedZombies)
                {
                    if (zombie == null)
                        continue;

                    zombie.Died -= HandleZombieDied;
                    _spawnedZombies.Remove(zombie);
                }

                foreach (GameObject instance in preparedObjects)
                {
                    if (instance != null && pool != null)
                        pool.Release(instance);
                }
            }
        }
    }

    private void HandleZombieDied(UnitController zombie)
    {
        zombie.Died -= HandleZombieDied;
        if (!_spawnedZombies.Remove(zombie) || !_isBattleRunning || _spawnedZombies.Count > 0)
            return;

        if (_boss != null && !_boss.IsDead)
            EndBattle(BossDungeonResult.Defeat);
    }

    private void HandleBossDied(BossController boss)
    {
        if (boss == _boss)
            EndBattle(BossDungeonResult.Victory);
    }

    private void EndBattle(BossDungeonResult result)
    {
        if (!_isBattleRunning || Result != BossDungeonResult.None)
            return;

        _isBattleRunning = false;
        Result = result;
        UnsubscribeBattleEvents();

        foreach (UnitController zombie in _spawnedZombies)
        {
            if (zombie != null && !zombie.IsDead)
                zombie.gameObject.SetActive(false);
        }

        if (_boss != null && !_boss.IsDead)
        {
            _bossStoppedForResult = true;
            _boss.gameObject.SetActive(false);
        }

        HandleBattleResult();
    }

    private void UnsubscribeBattleEvents()
    {
        if (_boss != null)
            _boss.Died -= HandleBossDied;

        foreach (UnitController zombie in _spawnedZombies)
        {
            if (zombie != null)
                zombie.Died -= HandleZombieDied;
        }
    }

    private void HandleBattleResult()
    {
        GoToMain();
    }

    public void GoToMain()
    {
        if (_isReturningToMain || !isActiveAndEnabled)
            return;

        _ = GoToMainAsync();
    }

    private async Task GoToMainAsync()
    {
        _isReturningToMain = true;
        int session = _session;
        try
        {
            // Let the death event finish before starting scene teardown.
            await Task.Yield();
            if (this == null || !isActiveAndEnabled || session != _session)
                return;

            SceneLoadManager loader = SceneLoadManager.Instance;
            while (loader != null && loader.IsLoading)
            {
                await Task.Yield();
                if (this == null || !isActiveAndEnabled || session != _session)
                    return;
            }

            if (loader != null && this != null && isActiveAndEnabled && session == _session)
                await loader.LoadContentSceneAsync("BattleScene");
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            _isReturningToMain = false;
        }
    }

    public void GoToNextDungeon()
    {
        Debug.LogWarning("[BossDungeonManager] Next dungeon progression is not implemented.", this);
    }

    public void Shutdown()
    {
        _session++;
        _isBattleRunning = false;
        UnsubscribeBattleEvents();
        foreach (UnitController zombie in _spawnedZombies)
        {
            if (zombie == null)
                continue;

            zombie.Died -= HandleZombieDied;
            if (PoolManager.HasInstance)
                PoolManager.Instance.Release(zombie.gameObject);
        }
        _spawnedZombies.Clear();

        if (_boss != null && (_boss.gameObject.activeSelf || _bossStoppedForResult) && PoolManager.HasInstance)
            PoolManager.Instance.Release(_boss.gameObject);
        _boss = null;
        _bossStoppedForResult = false;
    }

    private void OnDisable()
    {
        Shutdown();
    }
}
