using System.Threading.Tasks;
using UnityEngine;

public class BossScene : BaseScene
{
    [SerializeField] private BossDungeonManager bossDungeonManager;
    public override SceneLoadState LoadState { get; }

    public override async Task ScenePrepareAsync()
    {
        if (bossDungeonManager != null)
            await bossDungeonManager.InitializeAsync();
        else
            Debug.LogError("[BossScene] BossDungeonManager is required.", this);

        await UIManager.Instance.OpenUI<ExitDungeonButtonUI>();
    }

    public override void OnSceneExit()
    {
        if (bossDungeonManager != null)
            bossDungeonManager.Shutdown();
        UIManager.Instance.CloseUI<ExitDungeonButtonUI>();
    }
}
