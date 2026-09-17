using System.Threading.Tasks;

public class BossScene : BaseScene
{
    public override SceneLoadState LoadState { get; }

    public override async Task ScenePrepareAsync()
    {
        await UIManager.Instance.OpenUI<ExitDungeonButtonUI>();
    }

    public override void OnSceneExit()
    {
        UIManager.Instance.CloseUI<ExitDungeonButtonUI>();
    }
}
