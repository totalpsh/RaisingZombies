using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

public enum UILayer { Main, HUD, PopUp }

public class UIManager : Singleton<UIManager>, IAsyncInitializable
{
    public const string UICommonPath = "UI/Common/";
    public const string UIPrefabPath = "UI/Elements/";
    
    [SerializeField]private Transform uiRoot;                                 // 캔버스
    public Transform Root => uiRoot;                          // 캠버스 위치 가지고 가기.
    private EventSystem eventSystem;                          // 이벤트 시스템
    private GameObject modalPanel;                            // 팝업 아래 UI 상호작용 차단용

    private readonly Dictionary<UILayer, Transform> layers = new();
    private readonly Dictionary<string, Stack<BaseUI>> pooledUI = new();
    private readonly Dictionary<string, BaseUI> activeUI = new();

    private readonly Stack<BaseUI> popUpStack = new();
    private readonly Stack<BaseUI> reverseStack = new();

    private bool isCleaning = false;

    private LoadingUI _loadingUI;
    
    protected override void Awake()
    {
        base.Awake();

        // SceneManager.sceneUnloaded += OnSceneUnloaded;
    }
    
    public async Task<LoadingUI> GetOrCreateLoadingUIAsync()
    {
        if (_loadingUI != null)
            return _loadingUI;

        var obj = await ResourceManager.Instance.CreateAsync<GameObject>("LoadingUI", uiRoot);
        _loadingUI = obj.GetComponent<LoadingUI>();
        
        if (_loadingUI == null)
        {
            Debug.LogError("LoadingUI 생성 실패");
            return null;
        }

        _loadingUI.HideImmediate();
        return _loadingUI;
    }
    
    public async Task ShowLoadingAsync(string text = "Loading...")
    {
        var ui = await GetOrCreateLoadingUIAsync();
        if (ui == null) return;

        ui.SetText(text);
        await ui.ShowAsync();
    }

    public async Task HideLoadingAsync()
    {
        if (_loadingUI == null) return;
        await _loadingUI.HideAsync();
    }

    public async Task Init()
    {
        await CreateCanvasAndEventSystem();
        CreateLayers();
        // await CreateModalPanel();

    }
    
    public async Task InitializeAsync()
    {
        await CreateCanvasAndEventSystem();
        CreateLayers();
        await CreateModalPanel();
    }

    protected override void OnDestroy()
    {
        // SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    public Transform GetLayer(UILayer layer) // 레이어 정의
    {
        if (layers.TryGetValue(layer, out var t))
            return t;
        return null;
    }

    public bool HasPopup()
    {
        return popUpStack.Count > 0;
    }

    #region Initialization
    private async Task CreateCanvasAndEventSystem()
    {
        Debug.Log("UIManager.CreateCanvasAndEventSystem");
        if (uiRoot == null)
        {
            GameObject canvasPrefab = await ResourceManager.Instance.CreateAsync<GameObject>("Canvas");
            canvasPrefab.transform.SetParent(transform);
            uiRoot = canvasPrefab.transform;
        }

        if (eventSystem == null)
        {
            GameObject eventSystemPrefab = await ResourceManager.Instance.CreateAsync<GameObject>("EventSystem");
            eventSystemPrefab.transform.SetParent(transform);
            eventSystem = eventSystemPrefab.GetComponent<EventSystem>();
        }

        await Task.Yield();
    }

    private void CreateLayers()
    {
        if (uiRoot == null) return;
        
        foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
        {
            if (!layers.ContainsKey(layer))
            {
                GameObject obj = new GameObject(layer.ToString() + "Layer", typeof(RectTransform));
                RectTransform rt = obj.GetComponent<RectTransform>();
                
                rt.SetParent(uiRoot, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.sizeDelta = Vector2.zero;
                rt.anchoredPosition = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                
                layers[layer] = rt;
            }
        }
    }

    private async Task CreateModalPanel()
    {
        if (modalPanel != null) return;

        modalPanel = await ResourceManager.Instance.CreateAsync<GameObject>("ModalPanel", layers[UILayer.PopUp]);
        modalPanel.SetActive(false);
    }
    #endregion

    #region Open/Close UI
    public async Task<T> OpenUI<T>(object param = null, UILayer layer = UILayer.Main) where T : BaseUI
    {
        if (!layers.ContainsKey(layer))
            CreateLayers();
        
        if (layers.TryGetValue(layer, out var layerRoot) && !layerRoot.gameObject.activeSelf)
            layerRoot.gameObject.SetActive(true);
        
        BaseUI ui = await GetOrCreateUI<T>(layer);
        if (ui == null)
        {
            Debug.LogError($"{typeof(T).Name} 열기 실패");
            return null;
        }

        ui.gameObject.SetActive(true);
        // UIButtonSfxAutoBinder.BindButtonsIn(ui.transform);
        
        string key = typeof(T).Name;
        activeUI[key] = ui;

        // reverseStack 중복 방지
        if (!reverseStack.Contains(ui)) reverseStack.Push(ui);

        if (layer == UILayer.PopUp && !popUpStack.Contains(ui)) popUpStack.Push(ui);

        ui?.Init(param);

        UpdateModal();
        
        return ui as T;
    }

    public void CloseUI<T>() where T : BaseUI
    {
        string uiName = typeof(T).Name;
        if (activeUI.TryGetValue(uiName, out var ui))
            CloseUI(ui);
    }

    public void CloseUI(BaseUI ui)
    {
        if (ui == null) return;

        string key = ui.GetType().Name;
        
        ui.CloseUI();
        
        activeUI.Remove(key);

        if (!pooledUI.ContainsKey(key))
            pooledUI[key] = new Stack<BaseUI>();
        pooledUI[key].Push(ui);

        RemoveFromStack(popUpStack, ui);
        RemoveFromStack(reverseStack, ui);

        UpdateModal();
    }

    private void RemoveFromStack(Stack<BaseUI> stack, BaseUI ui)
    {
        if (stack.Count == 0) return;

        Stack<BaseUI> temp = new();
        while (stack.Count > 0)
        {
            BaseUI top = stack.Pop();
            if (top != ui)
                temp.Push(top);
        }
        while (temp.Count > 0)
            stack.Push(temp.Pop());
    }

    public void CloseTopPopUp()
    {
        while (popUpStack.Count > 0)
        {
            BaseUI ui = popUpStack.Pop();

            if (ui.gameObject.activeSelf)
            {
                CloseUI(ui);
                break;
            }
        }
    }
    #endregion

    #region Get / Create UI
    public T GetUI<T>() where T : BaseUI
    {
        return activeUI.TryGetValue(typeof(T).Name, out var ui) ? ui as T : null;
    }

    public T FindUI<T>() where T : BaseUI // UI 찾기
    {
        string key = typeof(T).Name;
        
        if(activeUI.TryGetValue(key, out var active))
            return active as T;
        
        if(pooledUI.TryGetValue(key, out var stack))
        {
            foreach (var ui in stack)
            {
                if (ui != null)
                    return ui as T;
            }
        }

        return null;
    }

    private async Task<BaseUI> GetOrCreateUI<T>(UILayer layer) where T : BaseUI
    {
        string uiName = typeof(T).Name;
        BaseUI ui;

        if (activeUI.TryGetValue(uiName, out var active))
        {
            if (active == null || active.gameObject == null) // Destroy된 참조
            {
                activeUI.Remove(uiName);
            }
            else
            {
                // 이미 켜져 있음 → 그대로 사용
                active.gameObject.SetActive(true);
                active.transform.SetParent(layers[layer], false);
                // UIButtonSfxAutoBinder.BindButtonsIn(active.transform);
                return active;
            }
        }

        if (pooledUI.TryGetValue(uiName, out var stack))
        {
            while (stack.Count > 0)
            {
                ui = stack.Pop();

                // Destroy된 UI 제거
                if (ui == null || ui.gameObject == null)
                    continue;

                // 정상 UI라면 활성화
                ui.transform.SetParent(layers[layer], false);
                ui.gameObject.SetActive(true);
                // UIButtonSfxAutoBinder.BindButtonsIn(ui.transform);

                activeUI[uiName] = ui;
                return ui;
            }
        }

        string path = uiName;
        GameObject go = await ResourceManager.Instance.CreateAsync<GameObject>(path, layers[layer]);

        if (go == null)
        {
            Debug.LogError($"UI 생성 실패: {uiName}");
            return null;
        }

        ui = go.GetComponent<BaseUI>();
        if (ui == null)
        {
            Debug.LogError($"{uiName} 은 BaseUI가 없음");
            GameObject.Destroy(go);
            return null;
        }

        ui.name = uiName;
        activeUI[uiName] = ui;
        // UIButtonSfxAutoBinder.BindButtonsIn(ui.transform);

        return ui;
    }

    public async Task<T> CreateSlotUI<T>(Transform parent = null) where T : BaseUI
    {
        string path = typeof(T).Name;
        
        T ui = await ResourceManager.Instance.CreateAsync<T>(path, parent);
        
        // if (ui != null)
            // UIButtonSfxAutoBinder.BindButtonsIn(ui.transform);

        return ui;
    }
    #endregion

    #region Modal Panel
    private void UpdateModal()
    {
        if (modalPanel == null) return;

        if (popUpStack.Count == 0)
        {
            modalPanel.SetActive(false);
            return;
        }

        modalPanel.SetActive(true);

        // Modal 항상 팝업 최상단 SortingOrder + 1
        Canvas modalCanvas = modalPanel.GetComponent<Canvas>();
        if (modalCanvas != null)
        {
            modalCanvas.overrideSorting = true;

            BaseUI top = null;
            foreach (var ui in popUpStack)
            {
                if (ui.gameObject.activeSelf)
                {
                    top = ui;
                    break;
                }
            }

            if (top != null)
            {
                Canvas topCanvas = top.GetComponent<Canvas>();
                int topOrder = topCanvas != null ? topCanvas.sortingOrder : 1000;
                modalCanvas.sortingOrder = topOrder + 1;
            }
        }
    }
    #endregion

    #region Scene / Cleanup
    private void OnSceneUnloaded(Scene scene)
    {
        StartCoroutine(CleanupAllUI());
    }

    public void CloseAllUI()
    {
        Debug.Log("CloseAllUI");
        
        // activeUI → 모두 UI 닫기 + 풀로 이동
        foreach (var kv in activeUI)
        {
            BaseUI ui = kv.Value;
            if (ui == null) 
                continue;
            if (ui is LoadingUI)
                continue;
            
            ui.gameObject.SetActive(false);

            if (!pooledUI.ContainsKey(kv.Key))
                pooledUI[kv.Key] = new Stack<BaseUI>();

            // 중복 푸시 방지
            if (!pooledUI[kv.Key].Contains(ui))
                pooledUI[kv.Key].Push(ui);
        }

        activeUI.Clear();
        popUpStack.Clear();
        reverseStack.Clear();
        PoolManager.Instance.ClearAll();
        
        if (uiRoot != null)
        {
            var allUIs = uiRoot.GetComponentsInChildren<BaseUI>(true);
            foreach (var ui in allUIs)
            {
                if (ui == null) continue;

                ui.gameObject.SetActive(false);

                string key = ui.GetType().Name;
                if (!pooledUI.ContainsKey(key)) pooledUI[key] = new Stack<BaseUI>();
                if (!pooledUI[key].Contains(ui)) pooledUI[key].Push(ui);
            }
        }
    }
    
    
    // 전부 정리해야할때 아래 코드 로 직접 호출하기
    // StartCoroutine(UIManager.Instance.CleanupAllUIs());
    public IEnumerator CleanupAllUI()
    {
        if (isCleaning) yield break;
        isCleaning = true;

        List<string> activeRemoveList = new List<string>();
        foreach (var uiValue in activeUI)
        {
            BaseUI ui = uiValue.Value;

            // Destroy된 오브젝트 제거
            if (ui == null)
            {
                activeRemoveList.Add(uiValue.Key);
                continue;
            }

            // 비활성화
            ui.gameObject.SetActive(false);
        }
        
        foreach (var key in activeRemoveList)
            activeUI.Remove(key);

        foreach (var kv in pooledUI)
        {
            Stack<BaseUI> original = kv.Value;
            Stack<BaseUI> cleaned = new Stack<BaseUI>();

            foreach (var ui in original)
            {
                if (ui != null)   // Destroy된 UI는 null로 인식됨
                {
                    ui.gameObject.SetActive(false);
                    cleaned.Push(ui);
                }
            }

            pooledUI[kv.Key] = cleaned; // 정리된 스택으로 교체
        }
        
        popUpStack.Clear();
        reverseStack.Clear();

        isCleaning = false;
        yield break;
    }
    #endregion
}
