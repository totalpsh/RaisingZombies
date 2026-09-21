using System.Threading.Tasks;
using UnityEngine;

public class BossDungeonManager : MonoBehaviour
{
    [SerializeField] private string bossKey;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private BattleArea battleArea;
    [SerializeField, Min(1f)] private float testMaxHealth = 1000f;

    private BossController _boss;
    private bool _isInitializing;
    private int _session;

    public async Task InitializeAsync()
    {
        if (_isInitializing || _boss != null)
            return;
        if (string.IsNullOrEmpty(bossKey) || spawnPoint == null || battleArea == null)
        {
            Debug.LogError("[BossDungeonManager] Boss key, SpawnPoint and BattleArea are required.", this);
            return;
        }

        PoolManager pool = PoolManager.Instance;
        if (pool == null)
            return;

        _isInitializing = true;
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
            initialized = true;
        }
        finally
        {
            if (!initialized && bossObject != null && pool != null)
                pool.Release(bossObject);
            _isInitializing = false;
        }
    }

    public void Shutdown()
    {
        _session++;
        if (_boss != null && _boss.gameObject.activeSelf && PoolManager.HasInstance)
            PoolManager.Instance.Release(_boss.gameObject);
        _boss = null;
    }

    private void OnDisable()
    {
        Shutdown();
    }
}
