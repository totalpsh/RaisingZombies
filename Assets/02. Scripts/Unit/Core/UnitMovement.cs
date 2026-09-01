using UnityEngine;

public class UnitMovement : MonoBehaviour
{
    private UnitController _owner;
    private UnitAnimation _animation;
    private BattleArea _battleArea;
    
    public void Initialize(UnitController owner, UnitAnimation anim, BattleArea battleArea)
    {
        _owner = owner;
        _animation = anim;
        _battleArea = battleArea;
    }
    
    public void MoveForward(float speed)
    {
        if (_owner == null)
            return;

        _animation.PlayWalk();

        float direction = _owner.Team == UnitTeam.Zombie ? 1f : -1f;
        Vector3 nextPosition = transform.position + Vector3.right * (direction * speed * Time.deltaTime);

        ApplyPosition(nextPosition);
    }
    
    public void MoveTo(Vector3 position, float speed)
    {
        _animation.PlayWalk();

        Vector3 nextPosition = Vector3.MoveTowards(transform.position, position, speed * Time.deltaTime);

        ApplyPosition(nextPosition);
    }
    
    private void ApplyPosition(Vector3 position)
    {
        if (_battleArea != null)
            position = _battleArea.ClampPosition(position);

        transform.position = position;
    }
}
