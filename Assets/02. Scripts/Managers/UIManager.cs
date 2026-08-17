
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;

public enum UILayer { Main, HUD, PopUp, Transition }

public class UIManager : Singleton<UIManager>
{
    [SerializeField] private RectTransform uiRoot;
    [SerializeField] private EventSystem eventSystem;

    [SerializeField] private Canvas mainCanvas;
    [SerializeField] private Canvas hudCanvas;
    [SerializeField] private Canvas popUpCanvas;
    [SerializeField] private Canvas transitionCanvas;

    // 캔버스
    public Transform Root => uiRoot; // 캠버스 위치 가지고 가기.// 이벤트 시스템

    private readonly Dictionary<UILayer, Transform> _layers = new();
    private readonly Dictionary<string, Stack<BaseUI>> _pooledUI = new();
    private readonly Dictionary<string, BaseUI> _activeUI = new();

    private readonly Stack<BaseUI> _popUpStack = new();
    private readonly Stack<BaseUI> _reverseStack = new();

    private GameObject _modalPanel; // 팝업 아래 UI 상호작용 차단용
    private LoadingUI _loadingUI;
    private StageFadeUI _stageFadeUI;

    private Task _initializationTask;
    private Task<LoadingUI> _loadingUICreationTask;
    private Task<StageFadeUI> _stageFadeUICreationTask;

    private bool _isInitialized;
    private bool _isCleaning = false;

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    #region Initialization

    public Task InitializeAsync()
    {
        if (_isInitialized)
            return Task.CompletedTask;

        return _initializationTask ??= InitializeInternalAsync();
    }


    private async Task InitializeInternalAsync()
    {
        if (!ValidateReferences())
        {
            _initializationTask = null;
            return;
        }

        RegisterLayers();
        await CreateModalPanel();

        _isInitialized = true;
    }

    private bool ValidateReferences()
    {
        bool isValid = true;

        if (uiRoot == null)
        {
            Debug.LogError("[UIManager] UIRoot가 연결되지 않았습니다.");
            isValid = false;
        }

        if (eventSystem == null)
        {
            Debug.LogError("[UIManager] EventSystem이 연결되지 않았습니다.");
            isValid = false;
        }

        if (mainCanvas == null)
        {
            Debug.LogError("[UIManager] MainLayer가 연결되지 않았습니다.");
            isValid = false;
        }

        if (hudCanvas == null)
        {
            Debug.LogError("[UIManager] HUDLayer가 연결되지 않았습니다.");
            isValid = false;
        }

        if (popUpCanvas == null)
        {
            Debug.LogError("[UIManager] PopUpLayer가 연결되지 않았습니다.");
            isValid = false;
        }

        if (transitionCanvas == null)
        {
            Debug.LogError("[UIManager] TransitionLayer가 연결되지 않았습니다.");
            isValid = false;
        }

        return isValid;
    }

    private void RegisterLayers()
    {
        _layers.Clear();

        _layers[UILayer.Main] = mainCanvas.transform;
        _layers[UILayer.HUD] = hudCanvas.transform;
        _layers[UILayer.PopUp] = popUpCanvas.transform;
        _layers[UILayer.Transition] = transitionCanvas.transform;
    }

    private async Task CreateModalPanel()
    {
        if (_modalPanel != null) return;

        GameObject createdPanel = await ResourceManager.Instance.CreateAsync<GameObject>("ModalPanel", popUpCanvas.transform);

        if (createdPanel == null)
        {
            Debug.LogError("[UIManager] ModalPanel 생성 실패");
            return;
        }

        // 팝업보다 뒤에 렌더링되도록 첫 번째 자식으로 배치
        createdPanel.transform.SetAsFirstSibling();
        createdPanel.SetActive(false);

        _modalPanel = createdPanel;
    }

    private async Task<bool> EnsureInitializedAsync()
    {
        await InitializeAsync();

        if (_isInitialized)
            return true;

        Debug.LogError("[UIManager] 초기화되지 않았습니다.");
        return false;
    }

    #endregion
    
    #region Open/Close UI

    public async Task<T> OpenUI<T>(object param = null, UILayer layer = UILayer.Main) where T : BaseUI
    {
        if (!await EnsureInitializedAsync())
            return null;

        if (!_layers.TryGetValue(layer, out Transform layerRoot))
        {
            Debug.LogError($"[UIManager] {layer} 레이어를 찾을 수 없습니다.");
            return null;
        }

        if (!layerRoot.gameObject.activeSelf)
            layerRoot.gameObject.SetActive(true);

        BaseUI ui = await GetOrCreateUI<T>(layer);

        if (ui == null)
        {
            Debug.LogError($"[UIManager] {typeof(T).Name} 열기 실패");
            return null;
        }

        ui.gameObject.SetActive(true);

        string key = typeof(T).Name;
        _activeUI[key] = ui;

        if (!_reverseStack.Contains(ui))
            _reverseStack.Push(ui);

        if (layer == UILayer.PopUp)
        {
            if (!_popUpStack.Contains(ui))
                _popUpStack.Push(ui);

            // ModalPanel보다 앞에 오도록 보장
            ui.transform.SetAsLastSibling();
        }

        ui.Init(param);

        UpdateModal();

        return ui as T;
    }

    public void CloseUI<T>() where T : BaseUI
    {
        string key = typeof(T).Name;

        if (_activeUI.TryGetValue(key, out BaseUI ui))
            CloseUI(ui);
    }

    public void CloseUI(BaseUI ui)
    {
        if (ui == null)
            return;

        string key = ui.GetType().Name;

        ui.CloseUI();

        _activeUI.Remove(key);

        if (!_pooledUI.TryGetValue(key, out Stack<BaseUI> stack))
        {
            stack = new Stack<BaseUI>();
            _pooledUI[key] = stack;
        }

        if (!stack.Contains(ui))
            stack.Push(ui);

        RemoveFromStack(_popUpStack, ui);
        RemoveFromStack(_reverseStack, ui);

        UpdateModal();
    }

    public void CloseTopPopUp()
    {
        while (_popUpStack.Count > 0)
        {
            BaseUI ui = _popUpStack.Pop();

            if (ui == null || !ui.gameObject.activeSelf)
                continue;

            CloseUI(ui);
            return;
        }

        UpdateModal();
    }

    private void RemoveFromStack(Stack<BaseUI> stack, BaseUI target)
    {
        if (stack.Count == 0)
            return;

        Stack<BaseUI> temporaryStack = new();

        while (stack.Count > 0)
        {
            BaseUI current = stack.Pop();

            if (current != target)
                temporaryStack.Push(current);
        }

        while (temporaryStack.Count > 0)
            stack.Push(temporaryStack.Pop());
    }

    #endregion

    #region Get / Create UI

    public T GetUI<T>() where T : BaseUI
    {
        return _activeUI.TryGetValue(typeof(T).Name, out BaseUI ui) ? ui as T : null;
    }

    public T FindUI<T>() where T : BaseUI
    {
        string key = typeof(T).Name;

        if (_activeUI.TryGetValue(key, out BaseUI active))
            return active as T;

        if (!_pooledUI.TryGetValue(key, out Stack<BaseUI> stack))
            return null;

        foreach (BaseUI ui in stack)
        {
            if (ui != null)
                return ui as T;
        }

        return null;
    }

    private async Task<BaseUI> GetOrCreateUI<T>(
        UILayer layer
    ) where T : BaseUI
    {
        string uiName = typeof(T).Name;

        if (!_layers.TryGetValue(layer, out Transform layerRoot))
        {
            Debug.LogError($"[UIManager] {layer} 레이어가 없습니다.");
            return null;
        }

        if (_activeUI.TryGetValue(uiName, out BaseUI active))
        {
            if (active == null)
            {
                _activeUI.Remove(uiName);
            }
            else
            {
                active.transform.SetParent(layerRoot, false);
                active.gameObject.SetActive(true);

                return active;
            }
        }

        if (_pooledUI.TryGetValue(uiName, out Stack<BaseUI> stack))
        {
            while (stack.Count > 0)
            {
                BaseUI pooled = stack.Pop();

                if (pooled == null)
                    continue;

                pooled.transform.SetParent(layerRoot, false);
                pooled.gameObject.SetActive(true);

                _activeUI[uiName] = pooled;

                return pooled;
            }
        }

        GameObject createdObject = await ResourceManager.Instance.CreateAsync<GameObject>(uiName, layerRoot);

        if (createdObject == null)
        {
            Debug.LogError($"[UIManager] UI 생성 실패: {uiName}");
            return null;
        }

        if (!createdObject.TryGetComponent(out BaseUI createdUI))
        {
            Debug.LogError($"[UIManager] {uiName} 프리팹에 BaseUI가 없습니다.");
            Destroy(createdObject);
            return null;
        }

        createdUI.name = uiName;
        _activeUI[uiName] = createdUI;

        return createdUI;
    }

    public async Task<T> CreateSlotUI<T>(Transform parent = null) where T : BaseUI
    {
        if (!await EnsureInitializedAsync())
            return null;

        Transform targetParent = parent != null ? parent : mainCanvas.transform;

        return await ResourceManager.Instance.CreateAsync<T>(typeof(T).Name, targetParent);
    }

    #endregion

    #region Modal Panel

    private void UpdateModal()
    {
        if (_modalPanel == null)
            return;

        BaseUI topPopup = GetTopActivePopup();

        if (topPopup == null)
        {
            _modalPanel.SetActive(false);
            return;
        }

        _modalPanel.SetActive(true);

        // PopupLayer 안에서 순서를 다음처럼 유지
        // ModalPanel → 팝업 UI
        _modalPanel.transform.SetAsFirstSibling();
        topPopup.transform.SetAsLastSibling();
    }

    private BaseUI GetTopActivePopup()
    {
        foreach (BaseUI ui in _popUpStack)
        {
            if (ui != null && ui.gameObject.activeSelf)
                return ui;
        }

        return null;
    }

    #endregion

    #region Loading UI

    public async Task<LoadingUI> GetOrCreateLoadingUIAsync()
    {
        if (!await EnsureInitializedAsync())
            return null;

        if (_loadingUI != null)
            return _loadingUI;

        _loadingUICreationTask ??= CreateLoadingUIInternalAsync();

        LoadingUI result = await _loadingUICreationTask;

        if (result == null)
            _loadingUICreationTask = null;

        return result;
    }

    private async Task<LoadingUI> CreateLoadingUIInternalAsync()
    {
        GameObject createdObject =
            await ResourceManager.Instance.CreateAsync<GameObject>(nameof(LoadingUI), transitionCanvas.transform);

        if (createdObject == null || !createdObject.TryGetComponent(out LoadingUI createdUI))
        {
            Debug.LogError("[UIManager] LoadingUI 생성 실패");

            if (createdObject != null)
                Destroy(createdObject);

            return null;
        }

        _loadingUI = createdUI;
        _loadingUI.HideImmediate();

        return _loadingUI;
    }

    public async Task ShowLoadingAsync(string text = "Loading...")
    {
        LoadingUI ui = await GetOrCreateLoadingUIAsync();

        if (ui == null)
            return;

        ui.transform.SetAsLastSibling();
        ui.SetText(text);

        await ui.ShowAsync();
    }

    public async Task HideLoadingAsync()
    {
        if (_loadingUI == null)
            return;

        await _loadingUI.HideAsync();
    }

    #endregion

    #region Stage Fade UI

    public async Task<StageFadeUI> GetOrCreateStageFadeUIAsync()
    {
        if (!await EnsureInitializedAsync())
            return null;

        if (_stageFadeUI != null)
            return _stageFadeUI;

        _stageFadeUICreationTask ??= CreateStageFadeUIInternalAsync();

        StageFadeUI result = await _stageFadeUICreationTask;

        if (result == null)
            _stageFadeUICreationTask = null;

        return result;
    }

    private async Task<StageFadeUI> CreateStageFadeUIInternalAsync()
    {
        GameObject createdObject = await ResourceManager.Instance.CreateAsync<GameObject>(nameof(StageFadeUI), transitionCanvas.transform);

        if (createdObject == null)
        {
            Debug.LogError("[UIManager] StageFadeUI 생성 실패");
            return null;
        }

        if (!createdObject.TryGetComponent(out StageFadeUI createdUI))
        {
            Debug.LogError("[UIManager] StageFadeUI 컴포넌트가 없습니다.");
            Destroy(createdObject);
            return null;
        }

        _stageFadeUI = createdUI;

        return _stageFadeUI;
    }

    #endregion
    
    #region Cleanup

    public void CloseAllUI()
    {
        foreach (KeyValuePair<string, BaseUI> pair in _activeUI)
        {
            BaseUI ui = pair.Value;

            if (ui == null || IsPersistentTransitionUI(ui))
                continue;

            ui.gameObject.SetActive(false);
            AddToPool(pair.Key, ui);
        }

        _activeUI.Clear();
        _popUpStack.Clear();
        _reverseStack.Clear();

        if (_modalPanel != null)
            _modalPanel.SetActive(false);

        if (uiRoot != null)
        {
            BaseUI[] allUIs =
                uiRoot.GetComponentsInChildren<BaseUI>(true);

            foreach (BaseUI ui in allUIs)
            {
                if (ui == null || IsPersistentTransitionUI(ui))
                    continue;

                ui.gameObject.SetActive(false);
                AddToPool(ui.GetType().Name, ui);
            }
        }

        PoolManager.Instance.ClearAll();
    }

    public IEnumerator CleanupAllUI()
    {
        if (_isCleaning)
            yield break;

        _isCleaning = true;

        List<string> removeList = new();

        foreach (KeyValuePair<string, BaseUI> pair in _activeUI)
        {
            BaseUI ui = pair.Value;

            if (ui == null)
            {
                removeList.Add(pair.Key);
                continue;
            }

            if (!IsPersistentTransitionUI(ui))
                ui.gameObject.SetActive(false);
        }

        foreach (string key in removeList)
            _activeUI.Remove(key);

        List<string> poolKeys = new(_pooledUI.Keys);

        foreach (string key in poolKeys)
        {
            Stack<BaseUI> original = _pooledUI[key];
            Stack<BaseUI> cleaned = new();

            foreach (BaseUI ui in original)
            {
                if (ui == null)
                    continue;

                if (!IsPersistentTransitionUI(ui))
                    ui.gameObject.SetActive(false);

                cleaned.Push(ui);
            }

            _pooledUI[key] = cleaned;
        }

        _popUpStack.Clear();
        _reverseStack.Clear();

        if (_modalPanel != null)
            _modalPanel.SetActive(false);

        _isCleaning = false;
    }

    private void AddToPool(string key, BaseUI ui)
    {
        if (ui == null)
            return;

        if (!_pooledUI.TryGetValue(
                key,
                out Stack<BaseUI> stack))
        {
            stack = new Stack<BaseUI>();
            _pooledUI[key] = stack;
        }

        if (!stack.Contains(ui))
            stack.Push(ui);
    }

    private bool IsPersistentTransitionUI(BaseUI ui)
    {
        return ui == _loadingUI || ui == _stageFadeUI;
    }

    #endregion
    
}
