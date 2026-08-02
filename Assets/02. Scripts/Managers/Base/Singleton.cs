using UnityEngine;
using Debug = UnityEngine.Debug;

public abstract class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T _instance;
    
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<T>();
                
                if (_instance == null)
                {
                    Debug.LogError($"[{typeof(T).Name}] 인스턴스가 없음. Core씬에 해당 매니저가 배치 되어 있는지 확인 요망.");
                }
            }
            
            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning(
                $"중복된 {typeof(T).Name}을 제거합니다.",
                gameObject
            );

            Destroy(gameObject);
            return;
        }

        _instance = (T)this;
    }
    
    protected virtual void OnSingletonAwake() {}

    protected virtual void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
        
    }
}