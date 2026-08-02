using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

// 어떤 씬인지 기억 하기
// 씬을 로드하는 기능 만들기
// 로드 중복 방지, 로딩, 페이드 가튼 연출 넣기

public enum SceneLoadState // 실제 씬 이름을 잘 적어 주세요
{
    Title,
    MainMenu,
    MainGame,
    Stats,
    Stage
}

public class SceneLoadManager : Singleton<SceneLoadManager>
{
    private const string CORE_SCENE_NAME = "Core";

    private bool isLoading; // 동일 씬을 로드 되는 것을 방지 차 읽어 놓는 것입니다.
    private AsyncOperationHandle<SceneInstance>? currentAddressableSceneHandle = null;

    public BaseScene currentScene { get; private set; }
    public string currentSceneName { get; private set; }
    public SceneLoadState currentSceneState { get; private set; }

    [SerializeField] private LoadingUI loadingUI;

    public void Init()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        currentSceneName = activeScene.name;
        
        if (Enum.TryParse(activeScene.name, out SceneLoadState state))
            currentSceneState = state;
    }

    protected override void OnDestroy()
    {
        // SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void TossSceneController(BaseScene scene)
    {
        currentScene = scene;
    }

    public async Task LoadContentSceneAsync(string sceneKeyOrName, bool useAddressable = false)
    {
        if (isLoading) return;
        isLoading = true;

        try
        {
            float loadingStartTime = Time.unscaledTime;
            
            await UIManager.Instance.ShowLoadingAsync("Loading...");
            LoadingUI ui = await UIManager.Instance.GetOrCreateLoadingUIAsync();
            
            if (ui != null)
                ui.SetProgress(0f);
            
            currentScene?.OnSceneExit();
            currentScene = null;

            if (currentAddressableSceneHandle.HasValue)
            {
                if (ui != null)
                {
                    ui.SetText("한 걸음 내딛는 중...");
                    ui.SetProgress(0.1f);
                }
                
                var prevHandle = currentAddressableSceneHandle.Value;
                await Addressables.UnloadSceneAsync(prevHandle).Task;
                currentAddressableSceneHandle = null;

                Scene core = SceneManager.GetSceneByName(CORE_SCENE_NAME);
                if (core.IsValid())
                    SceneManager.SetActiveScene(core);
            }
            
            if (ui != null)
            {
                ui.SetText("페어리와 교감 중...");
                ui.SetProgress(0.2f);
            }
            
            var handle = Addressables.LoadSceneAsync(sceneKeyOrName, LoadSceneMode.Additive, activateOnLoad: true);
            await handle.Task;

            while (!handle.IsDone)
            {
                if (ui != null)
                {
                    float progress = Mathf.Lerp(0.2f, 0.7f, handle.PercentComplete);
                    ui.SetProgress(progress);
                }

                await Task.Yield();
            }
            
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"{sceneKeyOrName} 씬 로드 실패");
                return;
            }

            currentAddressableSceneHandle = handle;

            Scene loadedScene = handle.Result.Scene;
            SceneManager.SetActiveScene(loadedScene);
            
            BaseScene found = null;
            foreach (var obj in loadedScene.GetRootGameObjects())
            {
                found = obj.GetComponent<BaseScene>();
                if (found != null)
                    break;
            }

            if (found == null)
            {
                Debug.LogError($"BaseScene을 {sceneKeyOrName}에서 찾지 못했습니다.");
                return;
            }
            
            if (ui != null)
            {
                ui.SetText("가방에 장비 넣는 중...");
                ui.SetProgress(0.8f);
            }
            
            // UIManager.Instance.FindUI<SkillLevelUpPanel>()?.ClearSelectedSkillUI();
            
            await found.ScenePrepareAsync();
            
            if (ui != null)
                ui.SetProgress(0.95f);
            
            await Task.Yield();
            
            found.OnSceneEnter();
            
            currentScene = found;
            currentSceneName = sceneKeyOrName;
            currentSceneState = found.LoadState;
            
            if (ui != null)
            {
                ui.SetText("모험 준비 완료!!");
                ui.SetProgress(1f);
            }
            
            const float minLoadingTime = 0.5f;
            float elapsed = Time.unscaledTime - loadingStartTime;
            if (elapsed < minLoadingTime)
            {
                int remains = Mathf.CeilToInt((minLoadingTime - elapsed) * 1000f);
                await Task.Delay(remains);
            }
            
            // 사운드으
            
            // if(found.stageManager?.stageData.bgmKey != null)
            //     SoundManager.Instance.PlayBGM(found.stageManager.stageData.bgmKey);
            
            await UIManager.Instance.HideLoadingAsync();
            
        }
        catch (Exception e)
        {
            Debug.LogError($"씬 로드 중 예외 발생: {e}");
        }
        finally
        {
            isLoading = false;
        }
        
        // if (isLoading) return;
        // isLoading = true;
        //
        // await UIManager.Instance.ShowLoadingAsync("Loading...");
        //
        // currentScene?.OnSceneExit();
        // currentScene = null;
        //
        // if (currentAddressableSceneHandle.HasValue)
        // {
        //     if (loadingUI != null) loadingUI.SetProgress(0.2f);
        //     
        //     var prevHandle = currentAddressableSceneHandle.Value;
        //     await Addressables.UnloadSceneAsync(prevHandle).Task;
        //     currentAddressableSceneHandle = null;
        //     
        //     Scene core = SceneManager.GetSceneByName(CORE_SCENE_NAME);
        //     if (core.IsValid()) SceneManager.SetActiveScene(core);
        // }
        //
        // Scene loadedScene = default;
        //
        // // Additive 씬 로드
        // var handle = Addressables.LoadSceneAsync(sceneKeyOrName, LoadSceneMode.Additive, activateOnLoad: true);
        // await handle.Task;
        //
        // if (handle.Status != AsyncOperationStatus.Succeeded)
        // {
        //     Debug.LogError($"{sceneKeyOrName} 씬 로드 실패");
        //     isLoading = false;
        //     return;
        // }
        //     
        // currentAddressableSceneHandle = handle;
        //
        // loadedScene = handle.Result.Scene;
        //
        // SceneManager.SetActiveScene(loadedScene);
        //
        // await Task.Yield();
        //
        // BaseScene found = null;
        //
        // foreach (var obj in loadedScene.GetRootGameObjects())
        // {
        //     found = obj.GetComponent<BaseScene>();
        //     if (found != null)
        //         break;
        // }
        //
        // if (found == null)
        // {
        //     await Task.Yield();
        //     foreach (var obj in loadedScene.GetRootGameObjects())
        //     {
        //         found = obj.GetComponent<BaseScene>();
        //         if (found != null)
        //             break;
        //     }
        // }
        //
        // if (found == null)
        // {
        //     Debug.LogError($"BaseScene을 {sceneKeyOrName}에서 찾지 못했습니다");
        //     if (loadingUI != null) await loadingUI.HideAsync();
        //     isLoading = false;
        //     return;
        // }
        //
        // currentScene = found;
        // currentSceneName = sceneKeyOrName;
        //
        // await currentScene.ScenePrepareAsync();
        //
        // await Task.Yield();
        // await Task.Yield();
        //
        // currentScene.OnSceneEnter();
        //
        // await UIManager.Instance.HideLoadingAsync();
        // isLoading = false;
    }

    public async Task UnloadCurrentContentSceneAsync()
    {
        if (isLoading) return;
        if (string.IsNullOrEmpty(currentSceneName) || currentSceneName == CORE_SCENE_NAME) return;
        
        isLoading = true;
        
        currentScene?.OnSceneExit();
        currentScene = null;

        if (currentAddressableSceneHandle.HasValue)
        {
            await Addressables.UnloadSceneAsync(currentAddressableSceneHandle.Value).Task;
            currentAddressableSceneHandle = null;
        }
        else
        {
            var op = SceneManager.UnloadSceneAsync(currentSceneName);
            while (!op.isDone) await Task.Yield();
        }

        currentSceneName = CORE_SCENE_NAME;
        isLoading = false;
    }

// 재시작 (현재 콘텐츠 재로딩)
    public Task RestartContentAsync(string sceneKeyOrName, bool useAddressables = false)
    {
        return LoadContentSceneAsync(currentSceneName, useAddressables);
    }
}

//
//
//     public void LoadScene(SceneLoadState state)// 부를 때 숫자로 넣어도 됩니다.
//     {
//         if(isLoading) return;
//         isLoading = true;
//         // 다른 씬으로 나가기 전에 현재 씬 정리
//         currentScene?.OnSceneExit();
//         SceneManager.LoadScene(state.ToString());
//     }
//     public void LoadSceneAsync(SceneLoadState state)//페이드 용(비동기)
//     {
//         if(isLoading) return;
//         if (state.ToString() == currentSceneName) return;
//         currentScene?.OnSceneExit(); // 시작 전에 제거
//         StartCoroutine(LoadSceneCoroutine(state));
//     }
//
//     private IEnumerator LoadSceneCoroutine(SceneLoadState state)
//     {
//         isLoading = true;
//         
//         AsyncOperation asyncOperation = SceneManager.LoadSceneAsync(state.ToString());
//         asyncOperation.allowSceneActivation = false;
//         
//         while (!asyncOperation.isDone)
//         {
//             float progress = Mathf.Clamp01(asyncOperation.progress / 0.9f);
//             // 여기서 로딩바 업데이트 만들면 됩니다. ex) UIManager.Instance.SetProgress(progress)
//             
//             if (asyncOperation.progress >= 0.9f)
//             {
//                 //씬 전환 승인 입니다.(페이드 인, 연출 끝난 뒤)
//                 asyncOperation.allowSceneActivation = true;
//             }
//             yield return null;
//         }
//         
//     }
//     
//     // 재시작 (동기/비동기)
//     public void RestartScene()
//     {
//         if (isLoading) return;
//         
//         LoadScene(currentSceneState);
//     }
//     public void RestartSceneAsync()
//     {
//         if (isLoading) return;
//         StartCoroutine(LoadSceneCoroutine(currentSceneState));
//     }
//     
//     
// }
