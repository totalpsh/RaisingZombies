using System.Threading.Tasks;

public class BattleScene : BaseScene
{
    public override SceneLoadState LoadState { get; }

    public override async Task ScenePrepareAsync()
    {
        await UIManager.Instance.ShowMainNavigationAsync();
        await UIManager.Instance.OpenUI<DungeonButtonUI>();
    }

    public override void OnSceneExit()
    {
        UIManager.Instance.CloseUI<DungeonButtonUI>();
    }
}
