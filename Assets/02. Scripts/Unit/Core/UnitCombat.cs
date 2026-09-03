using UnityEngine;

public class UnitCombat : MonoBehaviour
{
    private UnitController _owner;
    private UnitAction _unitAction;
    private UnitModel _model;
    private UnitAnimation _animation;

    public void Initialize(
        UnitController owner,
        UnitAction unitAction,
        UnitModel model,
        UnitAnimation animation)
    {
        _owner = owner;
        _unitAction = unitAction;
        _model = model;
        _animation = animation;
    }

    public bool IsInAttackRange(ICombatTarget target)
    {
        if (!IsValidTarget(target))
            return false;

        Collider2D ownerCollider = _owner.TargetCollider;
        Collider2D targetCollider = target.TargetCollider;

        if (ownerCollider == null || targetCollider == null)
            return false;

        ColliderDistance2D distance =
            ownerCollider.Distance(targetCollider);

        return distance.distance <=
               _model.Stats.AttackRange;
    }

    public bool TryAttack(ICombatTarget target)
    {
        if (!CanStartAttack(target))
            return false;

        ICombatTarget attackTarget = target;
        float attackPower = _model.Stats.AttackPower;

        if (_animation == null)
        {
            _model.ResetAttackCooldown();
            ExecuteAttack(attackTarget, attackPower);
            return true;
        }

        bool started = _animation.PlayAttack(
            () => ExecuteAttack(
                attackTarget,
                attackPower));

        if (!started)
            return false;

        _model.ResetAttackCooldown();
        return true;
    }

    private bool CanStartAttack(ICombatTarget target)
    {
        if (_owner == null ||
            _owner.IsDead ||
            _unitAction == null ||
            _model == null)
        {
            return false;
        }

        if (!_model.CanAttack)
            return false;

        if (_animation != null && _animation.IsBusy)
            return false;

        if (!_unitAction.CanTarget(_owner, target))
            return false;

        return IsInAttackRange(target);
    }

    private void ExecuteAttack(
        ICombatTarget target,
        float attackPower)
    {
        if (_owner == null || _owner.IsDead)
            return;

        if (!IsValidTarget(target))
            return;

        if (!_unitAction.CanTarget(_owner, target))
            return;

        if (!IsInAttackRange(target))
            return;

        _unitAction.Execute(
            _owner,
            target,
            attackPower);
    }

    private static bool IsValidTarget(
        ICombatTarget target)
    {
        if (target == null || target.IsDead)
            return false;

        if (target is not MonoBehaviour targetObject)
            return false;

        return targetObject.gameObject.activeInHierarchy;
    }
}
