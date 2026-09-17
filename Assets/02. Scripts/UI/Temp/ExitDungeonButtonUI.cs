using UnityEngine;
using UnityEngine.UI;

public class ExitDungeonButtonUI : BaseUI
{
    [SerializeField] private Button exitButton;
    
    private void OnEnable()
    {
        exitButton.onClick.AddListener(OpenBattleScene);
    }

    private void OpenBattleScene()
    {
        _ = SceneLoadManager.Instance.LoadContentSceneAsync("BattleScene");
    }

    private void OnDisable()
    {
        exitButton.onClick.RemoveListener(OpenBattleScene);
    }
}
