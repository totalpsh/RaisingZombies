using UnityEngine;

// Main UI 전체를 현재 기기의 노치와 시스템 영역을 제외한 Safe Area에 맞춥니다.
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaFitter : MonoBehaviour
{
    private RectTransform targetRect; // Safe Area Anchor를 적용할 현재 UI Root
    private Rect lastSafeArea; // 중복 레이아웃 적용을 막을 마지막 Safe Area
    private Vector2Int lastScreenSize = new(-1, -1); // 방향과 해상도 변경을 감지할 마지막 화면 크기

    // 활성화될 때 현재 화면의 Safe Area를 즉시 적용합니다.
    private void OnEnable()
    {
        targetRect = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    // Canvas 크기가 바뀔 때 Safe Area 변경 여부를 확인합니다.
    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled)
        {
            ApplySafeArea();
        }
    }

    // Screen.safeArea를 정규화된 Anchor 값으로 변환해 Root에 적용합니다.
    private void ApplySafeArea()
    {
        if (targetRect == null || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        Rect safeArea = Screen.safeArea; // 현재 기기가 제공하는 실제 Safe Area 픽셀 영역
        Vector2Int screenSize = new(Screen.width, Screen.height); // 현재 방향이 반영된 화면 픽셀 크기
        if (safeArea == lastSafeArea && screenSize == lastScreenSize)
        {
            return;
        }

        lastSafeArea = safeArea;
        lastScreenSize = screenSize;

        Vector2 anchorMin = safeArea.position; // Safe Area 좌하단의 픽셀 위치
        Vector2 anchorMax = safeArea.position + safeArea.size; // Safe Area 우상단의 픽셀 위치
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        targetRect.anchorMin = anchorMin;
        targetRect.anchorMax = anchorMax;
        targetRect.offsetMin = Vector2.zero;
        targetRect.offsetMax = Vector2.zero;
    }
}
