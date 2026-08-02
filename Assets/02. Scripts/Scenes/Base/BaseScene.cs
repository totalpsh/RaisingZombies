using System.Threading.Tasks;
using UnityEngine;

public abstract class BaseScene : MonoBehaviour
{
    // public StageManager stageManager;
    public abstract SceneLoadState LoadState { get; }

    public virtual Task ScenePrepareAsync()
    {
        return Task.CompletedTask;
    }
    
    // 씬이 완전히 로드되고 나서 실행할 초기화 로직
    public virtual void OnSceneEnter()
    {
        if (SceneLoadManager.Instance != null)
            SceneLoadManager.Instance.TossSceneController(this);
    }
    
    // 다른 씬으로 넘어가기 전에 정리할 로직
    public virtual void OnSceneExit() { }
}