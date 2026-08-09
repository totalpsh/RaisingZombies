using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class CoreScene : BaseScene
{
    public override SceneLoadState LoadState { get; }
    
    [SerializeField] private List<MonoBehaviour> initializeList;
    [SerializeField] private List<MonoBehaviour>  initializeAsyncList; 
    
    private void Start()
    {
        Initialize();
    }

    private void Initialize()
    {
        foreach (MonoBehaviour behaviour in initializeList)
        {
            if (behaviour is not IInitializable initializable)
            {
                Debug.LogError(behaviour.name + "은 구현하지 않음.", behaviour);

                continue;
            }

            try
            {
                initializable.Initialize();
            }
            catch (Exception e)
            {
                Debug.LogException(e, behaviour);
                return;
            }
        }

        _ = UIManager.Instance.Init();
        
        Debug.Log("Core 초기화 완료");

        LoadFirstContent();
    }

    private void LoadFirstContent()
    {
        // 콘텐츠 씬 로드
        _ = SceneLoadManager.Instance.LoadContentSceneAsync("BattleScene", false);

        Debug.Log("Dsdfsdfd");
    }

    
}
