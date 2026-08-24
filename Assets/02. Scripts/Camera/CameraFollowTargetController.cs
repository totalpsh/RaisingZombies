using UnityEngine;

public class CameraFollowTargetController : MonoBehaviour
{
    [SerializeField] private Transform battleFocusPoint;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Collider2D cameraBounds;
    [SerializeField, Min(0.01f)] private float dragSensitivity = 1f;

    private bool _isDragging;

    private void Start()
    {
        SnapToBattleFocus();
    }

    private void OnEnable()
    {
        BattleCameraDragInput.DragStarted += BeginDrag;
        BattleCameraDragInput.Dragged += Drag;
        BattleCameraDragInput.DragEnded += EndDrag;
    }

    private void LateUpdate()
    {
        if (_isDragging || battleFocusPoint == null)
            return;

        Vector3 position = transform.position;
        position.x = battleFocusPoint.position.x;

        transform.position = position;
    }

    public void BeginDrag()
    {
        _isDragging = true;
    }

    public void Drag(float screenDeltaX)
    {
        if (!_isDragging || worldCamera == null)
            return;

        float worldWidth = worldCamera.orthographicSize * 2f * worldCamera.aspect;
        float worldDeltaX = screenDeltaX / Screen.width * worldWidth * dragSensitivity;
        Vector3 position = transform.position;

        // 손가락을 왼쪽으로 밀면 카메라는 오른쪽으로 이동
        position.x -= worldDeltaX;
        position.x = ClampPositionX(position.x);

        transform.position = position;
    }

    public void EndDrag()
    {
        _isDragging = false;
    }

    private float ClampPositionX(float positionX)
    {
        if (cameraBounds == null || worldCamera == null)
            return positionX;

        Bounds bounds = cameraBounds.bounds;

        float cameraHalfWidth = worldCamera.orthographicSize * worldCamera.aspect;
        float minimumX = bounds.min.x + cameraHalfWidth;
        float maximumX = bounds.max.x - cameraHalfWidth;

        if (minimumX > maximumX)
            return bounds.center.x;

        return Mathf.Clamp(positionX, minimumX, maximumX);
    }

    private void SnapToBattleFocus()
    {
        if (battleFocusPoint == null)
            return;

        Vector3 position = transform.position;
        position.x = battleFocusPoint.position.x;
        transform.position = position;
    }

    private void OnDisable()
    {
        BattleCameraDragInput.DragStarted -= BeginDrag;
        BattleCameraDragInput.Dragged -= Drag;
        BattleCameraDragInput.DragEnded -= EndDrag;

        _isDragging = false;
    }
}
