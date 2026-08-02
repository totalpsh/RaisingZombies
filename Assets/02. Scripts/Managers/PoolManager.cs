using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

//ResourceManager를 사용해서 생성하고,
// Destroy 대신 풀에 넣어두는 전역 풀 매니저.
// 풀링을 쓰는 오브젝트는 PoolManager.Instance... 로만 생성,해제,제거 하게 됨.
// 재사용 대기 중인 오브젝트를 스택에 담아두고, 키값을 통해 주무르기
// 키값에 해당하는 오브젝트를 가져온다. 남아 있으면 꺼내서 반환 없으면, ResourceManager로 새로 생성
// 나중에 씬을 변경 했을 때 걔만 제거하는 함수도 필요 할 수 있다.
public class PoolManager : Singleton<PoolManager>
{
    // key = 대기 중인 오브젝트 스택
    private readonly Dictionary<string, Stack<GameObject>> _pool = new();
    // 인스턴스 = key; 어떤 key로 만들어진 애인지 기록 하는 애
    private readonly Dictionary<GameObject, string> _instanceToKey = new();
    
    // 하이어 라키 정리띠
    private Transform _poolRoot;
    private readonly Dictionary <string, Transform> _keyRoots = new();
    
    // 이름용 시리얼 관리
    private readonly Dictionary<string, int> _keySerial = new();
    private readonly Dictionary<GameObject, int> _instanceSerial = new();
    
    private readonly Dictionary<string, GameObject> _prefabsCash = new(); // 프리팹 캐싱
    private readonly Dictionary<string, Task<GameObject>> _loadingTasks = new(); // 검사.
    

    protected override void Awake()
    {
        base.Awake();
        PoolRoot();
    }

    private void PoolRoot()
    {
        if (_poolRoot != null) return;
        
        _poolRoot = new GameObject("Pool Root").transform;
        _poolRoot.SetParent(transform, false);
    }

    private Transform GetKeyRoot(string key)
    {
        PoolRoot();
        
        if(_keyRoots.TryGetValue(key, out var root) && root != null) return root;
        
        var folder =  new GameObject(key).transform;
        root = folder.transform;
        root.SetParent(_poolRoot, false);
        _keyRoots[key] = root;
        return root;
    }

    private int GetOrAssignSerial(GameObject obj, string key)
    {
        if(_instanceSerial.TryGetValue(obj, out int serial)) return serial;
        
        _keySerial.TryGetValue(key, out int next);
        next++;
        _keySerial[key] = next;
        
        _instanceSerial[obj] = next;
        return next;
    }

    private void ApplyName(GameObject obj, string key)
    {
        int serial = GetOrAssignSerial(obj, key);
        obj.name = $"{KeyToLabel(key)}_{serial:0000}";
    }

    private static string KeyToLabel(string key)
    {
        if(string.IsNullOrEmpty(key)) return "NULL_KEY";
        
        key = key.Replace('\\', '/');
        int idx = key.LastIndexOf('/');
        string label = (idx >= 0) ? key[(idx + 1)..] : key;
        
        // 하이어라키 위험 문자 제거.
        foreach(char c in new[] { ':', '*', '?', '"', '<', '>', '|' })
            label = label.Replace(c, '_');
        
        return label;
    }

    public async Task<GameObject> GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogError($"{nameof(GetAsync)} called with a null key");
            return null;
        }
        
        // 풀에 남아 있는게 있으면 꺼내서 사용
        if (_pool.TryGetValue(key, out var stack))
        {
            while (stack.Count > 0)
            {
                var obj = stack.Pop();
                if (obj == null) continue;
                
                _instanceToKey[obj] = key;
                ApplyName(obj, key);
                obj.transform.SetParent(GetKeyRoot(key), false);
                obj.SetActive(true);
                
                return obj;
            }
        }
        // 캐시에 prefab 있으면 Instantiate
        if (_prefabsCash.TryGetValue(key, out var prefab))
        {
            var obj = Instantiate(prefab, GetKeyRoot(key));
            _instanceToKey[obj] = key;
            ApplyName(obj, key);
            
            return obj;
        }
        // 이미 같은 키를 로드 중이면 기다리기
        if (_loadingTasks.TryGetValue(key, out var loadingTask))
        {
            return await loadingTask;
        }

        var task = LoadAndCreateAsync(key);
        
        _loadingTasks[key] = task;

        try
        {
            return await task;
        }
        finally
        {
            _loadingTasks.Remove(key);
        }
    }
    private async Task<GameObject> LoadAndCreateAsync(string key)
    {
        var prefab = await ResourceManager.Instance.LoadAsync<GameObject>(key);

        if (prefab == null)
        {
            Debug.LogError($"Prefab Load Failed : {key}");
            return null;
        }

        _prefabsCash[key] = prefab;

        var obj = Instantiate(prefab, GetKeyRoot(key));

        _instanceToKey[obj] = key;
        ApplyName(obj, key);

        return obj;
    }
    
    public GameObject Get(string key) // 동기 버전
    {
        if( string.IsNullOrEmpty(key) ) return null;
        
        if (_pool.TryGetValue(key, out var stack))
        {
            while (stack.Count > 0)
            {
                var obj = stack.Pop();
                if (obj == null) continue;
                
                _instanceToKey[obj] = key;
                obj.transform.SetParent(GetKeyRoot(key), false);
                obj.SetActive(true);
                return obj;
            }
        }
        // 캐시에 프리펩 있으면 동기로 생성.
        if (_prefabsCash.TryGetValue(key, out var prefab))
        {
            var obj = Instantiate(prefab, GetKeyRoot(key));
            _instanceToKey[obj] = key;
            ApplyName(obj, key);
            return obj;
        }
        return null;
    }
    
    public async Task<GameObject> PreLoadAsync(string key, int preLoadIndex)
    {
        if (string.IsNullOrEmpty(key) || preLoadIndex <= 0)
            return null;

        if (!_prefabsCash.TryGetValue(key, out var prefab))
        {
            prefab = await ResourceManager.Instance.LoadAsync<GameObject>(key);
            if (prefab == null) return null;
            
            _prefabsCash[key] = prefab;
        }

        if (!_pool.TryGetValue(key, out var stack))
        {
            stack = new Stack<GameObject>();
            _pool[key] = stack;
        }
        
        for (int i = 0; i < preLoadIndex; i++)
        {
            var obj = Instantiate(prefab, GetKeyRoot(key));
            obj.SetActive(false);
            _instanceToKey[obj] = key;
            ApplyName(obj, key);
            stack.Push(obj);
        }
        return stack.Peek();
    }

    // 오브젝트를 다시 넣는 로직
    public void Release(GameObject obj)
    {
        if (obj == null) return;
        // 키값을 모르면 당연히 없애라.
        if(!_instanceToKey.TryGetValue(obj, out string key) || string.IsNullOrEmpty(key))
        {
            Debug.Log(obj.name + " 오브젝트 키가 없어서 Destroy 됐자너");
            Destroy(obj);
            return;
        }

        if (!_pool.TryGetValue(key, out var stack))
        {
            stack = new Stack<GameObject>();
            _pool[key] = stack;
        }
        obj.SetActive(false);
        obj.transform.SetParent(GetKeyRoot(key), false);
        stack.Push(obj);
    }

    public void ClearAll(bool includeActive = false) // 씬 전환 시
    {
        if (includeActive)
        {
            foreach (var obj in _instanceToKey.Keys)
            {
                if(obj != null)
                    Destroy(obj);
            }
        }
        
        foreach (var kvp in _pool)
        {
            var stack = kvp.Value;

            while (stack.Count > 0)
            {
                var obj = stack.Pop();
                if (obj == null) return;
                
                Destroy(obj);
            }
        }
        _pool.Clear();

        foreach (var root in _keyRoots.Values)
        {
            if (root != null) Destroy(root.gameObject);
        }
        _keyRoots.Clear();
        //인스턴스 추적 정보 제거
        _instanceToKey.Clear();
        _instanceSerial.Clear();
        // 키 관련 데이터 제거
        _keySerial.Clear();
        // 프리팹 캐시 제거
        _prefabsCash.Clear();
    }
    // 씬 전환이나 불 필요한 풀링 제거
    public void ClearKey(string key)
    {
        if (!_pool.TryGetValue(key, out var stack)) return;
        while (stack.Count > 0)
        {
            var obj = stack.Pop();
            _instanceToKey.Remove(obj);
            _instanceSerial.Remove(obj);
            Object.Destroy(obj);
        }
        _pool.Remove(key);
        if(_keyRoots.TryGetValue(key, out var root) && root != null)
            Destroy(root.gameObject);
        _keyRoots.Remove(key);
        _keySerial.Remove(key);
    }
    // TODO: ClearKey 외에도 ClearAll같은 완전 초기화가 필요 할 수도
}
