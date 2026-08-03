using UnityEngine;
using UnityEngine.UI;

public class UpgradeTest : MonoBehaviour
{
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private Button oneDraw;
    [SerializeField] private Button tenDraw;

    private void Awake()
    {
        if (oneDraw != null)
        {
            oneDraw.onClick.AddListener(DrawOne);
        }

        if (tenDraw != null)
        {
            tenDraw.onClick.AddListener(DrawTen);
        }
    }

    private void OnDestroy()
    {
        if (oneDraw != null)
        {
            oneDraw.onClick.RemoveListener(DrawOne);
        }

        if (tenDraw != null)
        {
            tenDraw.onClick.RemoveListener(DrawTen);
        }
    }

    public void DrawOne()
    {
        if (upgradeManager == null)
        {
            return;
        }

        upgradeManager.TryDrawOne(out _);
    }

    public void DrawTen()
    {
        if (upgradeManager == null)
        {
            return;
        }

        upgradeManager.TryDrawTen(out _);
    }
}
