using System;
using UnityEngine;

public class BattleScene : BaseScene
{
    public override SceneLoadState LoadState { get; }

    private void Start()
    {
        _ = UIManager.Instance.OpenUI<UpgradeMenuController>("UpgradeMenuController", UILayer.Main);
    }
}
