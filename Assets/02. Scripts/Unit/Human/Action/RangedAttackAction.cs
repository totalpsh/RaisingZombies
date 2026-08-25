using System.Threading.Tasks;
using UnityEngine;

public class RangedAttackAction : UnitAction
{
    [SerializeField] private string projectileKey;
    [SerializeField] private Transform firePoint;
    [SerializeField, Min(0.01f)] private float projectileSpeed = 8f;
    [SerializeField, Min(0.1f)] private float projectileLifetime = 5f;

    private int _session;

    public override void Execute(UnitController owner, ICombatTarget target, float power)
    {
        if (!IsValidOwner(owner) || !IsValidTarget(target))
            return;

        int session = _session;

        _ = FireAsync(owner, target, power, session);
    }

    private async Task FireAsync(UnitController owner, ICombatTarget target, float power, int session)
    {
        if (string.IsNullOrWhiteSpace(projectileKey))
        {
            Debug.LogError("ProjectileKey가 없습니다.", this);
            return;
        }

        GameObject projectileObject = await PoolManager.Instance.GetAsync(projectileKey, activateOnGet: false);

        if (projectileObject == null)
        {
            Debug.LogError($"{projectileKey} 생성 실패", this);
            return;
        }

        // 생성 대기 중 공격자가 죽거나 풀로 반환된 경우
        if (session != _session || !IsValidOwner(owner) || !IsValidTarget(target))
        {
            PoolManager.Instance.Release(projectileObject);
            return;
        }

        if (!projectileObject.TryGetComponent(out ProjectileController projectile))
        {
            Debug.LogError($"{projectileKey}에 " + "ProjectileController가 없습니다.", projectileObject);
            PoolManager.Instance.Release(projectileObject);
            return;
        }

        Transform origin = firePoint != null ? firePoint : owner.transform;
        Vector2 targetPosition = target.TargetCollider != null ? target.TargetCollider.bounds.center : target.TargetTransform.position;
        projectileObject.transform.SetPositionAndRotation(origin.position, origin.rotation);
        projectile.Initialize(owner.Team, targetPosition, projectileSpeed, projectileLifetime);
        projectileObject.SetActive(true);
    }

    private static bool IsValidOwner(UnitController owner)
    {
        return owner != null && owner.gameObject.activeInHierarchy && !owner.IsDead;
    }

    private static bool IsValidTarget(ICombatTarget target)
    {
        if (target == null || target.IsDead)
            return false;

        MonoBehaviour targetObject = target as MonoBehaviour;

        return targetObject != null && targetObject.gameObject.activeInHierarchy;
    }

    private void OnDisable()
    {
        // 이전 풀링 주기의 비동기 공격 무효화
        _session++;
    }
}
