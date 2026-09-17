using UnityEngine;
using UnityEngine.UI;

public class DungeonButtonUI : BaseUI
{
    [SerializeField] private Button bossButton;
    [SerializeField] private Button huntButton;

    private void OnEnable()
    {
        bossButton.onClick.AddListener(OpenBossScene);
        huntButton.onClick.AddListener(OpenHuntScene);
    }

    private void OpenBossScene()
    {
        _ = SceneLoadManager.Instance.LoadContentSceneAsync("BossScene");
    }

    private void OpenHuntScene()
    {
        _ = SceneLoadManager.Instance.LoadContentSceneAsync("HuntScene");
    }

    private void OnDisable()
    {
        bossButton.onClick.RemoveListener(OpenBossScene);
        huntButton.onClick.RemoveListener(OpenHuntScene);
    }
}
