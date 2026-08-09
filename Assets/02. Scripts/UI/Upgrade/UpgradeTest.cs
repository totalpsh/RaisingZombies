using UnityEngine;
using UnityEngine.UI;

public class UpgradeTest : MonoBehaviour
{
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private RectTransform upgradePanelRoot;

    private async void Awake()
    {
        upgradeManager = UpgradeManager.Instance;
        uiManager = UIManager.Instance;
        var testObj = uiManager.OpenUI<UpgradeMenuController>("UpgradeMenuController");
        testObj.Result.gameObject.SetActive(true);
        testObj.Result.transform.SetParent(upgradePanelRoot);
    }
}
