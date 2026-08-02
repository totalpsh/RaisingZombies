using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

public sealed class ResourceManager : Singleton<ResourceManager>
{
    // 로드한 에셋 관리
    private readonly Dictionary<string, Object> cache = new();

    public async Task<T> LoadAsync<T>(string key) where T : Object
    {
        if (cache.TryGetValue(key, out var obj))
            return obj as T;
        
        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            cache[key] = handle.Result;
            return handle.Result;
        }
        else
        {
            Debug.LogError($"{key} 어드레서블 불러오기 실패");
            return null;
        }
    }

    public async Task<T> CreateAsync<T>(string key, Transform parent = null) where T : Object
    {
        if (typeof(T) == typeof(GameObject))
        {
            if (cache.TryGetValue(key, out var prefabObj))
            {
                GameObject prefab = prefabObj as GameObject;
                GameObject instance = Object.Instantiate(prefab, parent);
                return instance as T;
            }

            var loadHandle = Addressables.LoadAssetAsync<GameObject>(key);
            GameObject prefabLoaded = await loadHandle.Task;

            if (prefabLoaded == null)
            {
                Debug.LogError($"{key} Prefab Load 실패");
                return null;
            }

            // Prefab을 캐싱
            cache[key] = prefabLoaded;

            // Prefab 기반으로 Instance 생성
            GameObject newInstance = GameObject.Instantiate(prefabLoaded, parent);
            return newInstance as T;
        }

        // Non-GameObject 타입은 원래 로직
        return await LoadAsync<T>(key);
    }

    public async Task<List<T>> LoadByLabel<T>(string label) where T : Object
    {
        List<T> resultList = new List<T>();
        AsyncOperationHandle<List<T>> handle = Addressables.LoadAssetAsync<List<T>>(label);
        await handle.Task;

        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            foreach (var item in handle.Result)
            {
                resultList.Add(item);
            }
        }
        else
        {
            Debug.LogError($"{label} 라벨 에셋 불러오기 실패");
        }
        
        return resultList;
    }

    public async Task<IList<T>> LoadAllAsync<T>(string label) where T : Object
    {
        var handle = Addressables.LoadAssetsAsync<T>(label, null);

        await handle.Task;
        

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"Addressables LoadAllAsync failed : {label}");
            return null;
        }
        
        var result= handle.Result;
        
        Addressables.Release(handle);

        return result;
    }

    public void Release(string key)
    {
        if (cache.TryGetValue(key, out var obj))
        {
            if (obj is GameObject)
                Addressables.ReleaseInstance(obj as GameObject);
            else
                Addressables.Release(obj);

            cache.Remove(key);
        }
    }

    public void ClearCache()
    {
        foreach (var keyValue in cache)
        {
            if (keyValue.Value is GameObject)
                Addressables.ReleaseInstance(keyValue.Value as GameObject);
            else
                Addressables.Release(keyValue.Value);
        }
        cache.Clear();
    }
}