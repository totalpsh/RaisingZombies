using System;
using UnityEngine;

public class BattleScene : BaseScene
{
    public override SceneLoadState LoadState { get; }

    private void Start()
    {
        _ = UIManager.Instance.ShowMainNavigationAsync();
    }
}
