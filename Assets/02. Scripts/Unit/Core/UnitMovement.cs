using UnityEngine;

public class UnitMovement : MonoBehaviour
{
    private UnitController _owner;
    private UnitAnimation _animation;
    private BattleArea _battleArea;
    private float _targetY;
    private bool _hasReachedTargetY;

    public void Initialize(
        UnitController owner,
        UnitAnimation animation,
        BattleArea battleArea,
        float targetY)
    {
        _owner = owner;
        _animation = animation;
        _battleArea = battleArea;
        _targetY = targetY;
        _hasReachedTargetY =
            Mathf.Approximately(
                owner.transform.position.y,
                targetY);
    }

    public void MoveForward(float speed)
    {
        if (_owner == null)
            return;

        _animation?.PlayWalk();

        float moveDistance =
            Mathf.Max(0f, speed) * Time.deltaTime;

        float direction =
            _owner.Team == UnitTeam.Zombie
                ? 1f
                : -1f;

        Transform unitRoot = _owner.transform;
        Vector3 nextPosition = unitRoot.position;

        nextPosition.x += direction * moveDistance;
        nextPosition.y = Mathf.MoveTowards(
            nextPosition.y,
            _targetY,
            moveDistance);

        unitRoot.position = ClampPosition(nextPosition);

        UpdateTargetYArrival(
            unitRoot.position.y);
    }

    public void MoveTo(
        Vector3 destination,
        float speed)
    {
        if (_owner == null)
            return;

        _animation?.PlayWalk();

        Transform unitRoot = _owner.transform;
        Vector3 currentPosition = unitRoot.position;

        destination.z = currentPosition.z;
        destination.y =
            GetMovementTargetY(destination.y);

        Vector3 nextPosition = Vector3.MoveTowards(
            currentPosition,
            destination,
            Mathf.Max(0f, speed) * Time.deltaTime);

        unitRoot.position = ClampPosition(nextPosition);
    }

    private float GetMovementTargetY(
        float destinationY)
    {
        UpdateTargetYArrival(
            _owner.transform.position.y);

        return _hasReachedTargetY
            ? destinationY
            : _targetY;
    }

    private void UpdateTargetYArrival(
        float currentY)
    {
        if (_hasReachedTargetY)
            return;

        if (Mathf.Abs(currentY - _targetY) <= 0.01f)
            _hasReachedTargetY = true;
    }

    private Vector3 ClampPosition(Vector3 position)
    {
        return _battleArea != null
            ? _battleArea.ClampPosition(position)
            : position;
    }
}
