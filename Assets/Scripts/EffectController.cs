using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System;

public class EffectController : MonoBehaviour
{
    public static EffectController Instance { get; private set; }

    [Header("Danh sách Prefab Hiệu ứng")]
    public List<GameObject> effectPrefabs;

    [Header("Cấu hình Pool")]
    public List<int> prewarmAmounts;

    // Quản lý Pool
    private Dictionary<int, Queue<GameObject>> _poolDictionary = new Dictionary<int, Queue<GameObject>>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializePools(); 
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializePools()
    {
        for (int i = 0; i < effectPrefabs.Count; i++)
        {
            if (effectPrefabs[i] == null) continue;

            _poolDictionary[i] = new Queue<GameObject>();

            int amountToCreate = (i < prewarmAmounts.Count) ? prewarmAmounts[i] : 0;

            for (int j = 0; j < amountToCreate; j++)
            {
                GameObject newEffect = Instantiate(effectPrefabs[i], transform);
                newEffect.SetActive(false);
                _poolDictionary[i].Enqueue(newEffect);
            }
        }
    }

    public void PlayEffect(int prefabIndex, Vector3 position)
    {
        if (prefabIndex < 0 || prefabIndex >= effectPrefabs.Count || effectPrefabs[prefabIndex] == null)
        {
            Debug.LogWarning($"[EffectController] Index {prefabIndex} không hợp lệ!");
            return;
        }

        GameObject effectObj = GetEffectFromPool(prefabIndex);

        // Đặt vị trí và bật hiệu ứng lên
        effectObj.transform.position = position;
        effectObj.SetActive(true);

        // Chạy Particle System nếu có
        ParticleSystem ps = effectObj.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Play(true);
        }

        // ĐÃ SỬA: Bắt buộc tắt và trả về Pool sau đúng 1 giây (1f)
        ReturnToPoolAfterDelay(prefabIndex, effectObj, 1f).Forget();
    }

    private GameObject GetEffectFromPool(int index)
    {
        if (_poolDictionary.ContainsKey(index) && _poolDictionary[index].Count > 0)
        {
            return _poolDictionary[index].Dequeue();
        }
        else
        {
            GameObject newEffect = Instantiate(effectPrefabs[index], transform);
            newEffect.SetActive(false);
            return newEffect;
        }
    }

    private async UniTaskVoid ReturnToPoolAfterDelay(int index, GameObject effectObj, float delaySeconds)
    {
        // Chờ đúng thời gian delaySeconds (ở đây là 1s)
        await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds), ignoreTimeScale: false, PlayerLoopTiming.Update, effectObj.GetCancellationTokenOnDestroy());

        // Sau 1s, tắt đi và nhét lại vào Pool
        if (effectObj != null)
        {
            effectObj.SetActive(false);
            _poolDictionary[index].Enqueue(effectObj); 
        }
    }
}