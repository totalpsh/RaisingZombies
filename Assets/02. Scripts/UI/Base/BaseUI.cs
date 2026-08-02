using UnityEngine;

public abstract class BaseUI : MonoBehaviour
{
    public string UIName => gameObject.name;
    
    public virtual void Init(object param = null) { } // 필요시 UI 초기화

    public virtual void OpenUI()
    {
        gameObject.SetActive(true);
        OnOpen();
        UpdateUI();
    }

    public virtual void CloseUI()
    {
        OnClose();
        gameObject.SetActive(false);
    }

    protected virtual void OnOpen() { }
    protected virtual void OnClose() { }
    protected virtual void UpdateUI() { }
}