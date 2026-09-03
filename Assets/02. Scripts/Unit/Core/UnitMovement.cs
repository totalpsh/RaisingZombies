using UnityEngine;

public class UnitMovement : MonoBehaviour
{
    private UnitController _owner;
    private UnitAnimation _animation;
    private BattleArea _battleArea;

    public void Initialize(
        UnitController owner,
        UnitAnimation animation,
        BattleArea battleArea)
    {
        _owner = owner;
        _animation = animation;
        _battleArea = battleArea;
    }

    public void MoveForward(float speed)
    {
        if (_owner == null)
            return;

        float direction =
            _owner.Team == UnitTeam.Zombie
                ? 1f
                : -1f;

        Vector3 destination =
            _owner.transform.position +
            Vector3.right * direction;

        MoveTo(destination, speed);
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

        Vector3 nextPosition = Vector3.MoveTowards(
            currentPosition,
            destination,
            Mathf.Max(0f, speed) * Time.deltaTime);

        unitRoot.position = ClampPosition(nextPosition);
    }

    private Vector3 ClampPosition(Vector3 position)
    {
        return _battleArea != null
            ? _battleArea.ClampPosition(position)
            : position;
    }
}
