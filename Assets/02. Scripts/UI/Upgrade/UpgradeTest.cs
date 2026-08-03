using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.UI;

public class UpgradeTest : MonoBehaviour
{
    private static readonly List<RaycastResult> RaycastResults = new();

    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private Button oneDraw;
    [SerializeField] private Button tenDraw;

    private void Awake()
    {
        if(oneDraw != null) oneDraw.onClick.AddListener(DrawOne);
        if(tenDraw != null) tenDraw.onClick.AddListener(DrawTen);
        Debug.Log("안녕");
    }

    private IEnumerator Start()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();

#if ENABLE_INPUT_SYSTEM
        Debug.Log($"[UpgradeTest] 입력 장치: Mouse.current={(Mouse.current == null ? "없음" : Mouse.current.displayName)}, " +
                  $"Pointer.current={(Pointer.current == null ? "없음" : Pointer.current.displayName)}");
#endif
        LogButtonRaycast("1회 버튼", oneDraw);
        LogButtonRaycast("10회 버튼", tenDraw);
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Pointer.current?.press.wasPressedThisFrame == true)
        {
            LogRaycast("실제 포인터 클릭", Pointer.current.position.ReadValue());
        }
#endif
    }

    public void DrawOne()
    {
        Debug.Log("hi");
        if (upgradeManager == null) return;

        if (upgradeManager.TryDrawOne(out var result))
        {
            Debug.Log($"{result.StatType} +{result.Value}, 누적 {result.Total}");
            Debug.Log("시발");
        }
    }

    public void DrawTen()
    {
        if (upgradeManager == null) return;
        if (upgradeManager.TryDrawTen(out var results))
        {
            foreach (var result in results)
            {
                Debug.Log($"{result.StatType} + {result.Value}");
            }
        }
    }

    /// <summary>버튼 중심 좌표에서 실제로 어떤 UI가 Raycast 되는지 출력합니다.</summary>
    private static void LogButtonRaycast(string label, Button button)
    {
        if (button == null)
        {
            Debug.LogError($"[UpgradeTest] {label} 참조가 없습니다.");
            return;
        }

        Canvas canvas = button.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        RectTransform rect = button.transform as RectTransform;
        Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
        Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCenter);

        LogRaycast(label, screenPosition, button.gameObject);
    }

    /// <summary>지정 좌표의 Raycast 순서와 클릭 핸들러를 출력합니다.</summary>
    private static void LogRaycast(string label, Vector2 screenPosition, GameObject expectedButton = null)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            Debug.LogError($"[UpgradeTest] {label}: EventSystem.current가 없습니다.");
            return;
        }

        RaycastResults.Clear();
        var eventData = new PointerEventData(eventSystem) { position = screenPosition };
        eventSystem.RaycastAll(eventData, RaycastResults);

        if (RaycastResults.Count == 0)
        {
            Debug.LogError($"[UpgradeTest] {label}: ({screenPosition.x:0}, {screenPosition.y:0})에서 UI Raycast 결과가 없습니다.");
            return;
        }

        RaycastResult top = RaycastResults[0];
        GameObject clickHandler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(top.gameObject);
        string expected = expectedButton == null ? "-" : expectedButton.name;
        string handler = clickHandler == null ? "없음" : clickHandler.name;

        Debug.Log(
            $"[UpgradeTest] {label}: 좌표=({screenPosition.x:0}, {screenPosition.y:0}), " +
            $"최상위={GetHierarchyPath(top.gameObject)}, 클릭핸들러={handler}, 예상버튼={expected}, " +
            $"Raycast수={RaycastResults.Count}, 입력모듈={eventSystem.currentInputModule?.GetType().Name}");
    }

    private static string GetHierarchyPath(GameObject target)
    {
        string path = target.name;
        Transform current = target.transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
