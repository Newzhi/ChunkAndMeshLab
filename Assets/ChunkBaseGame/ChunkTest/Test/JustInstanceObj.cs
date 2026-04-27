using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class JustInstanceObj : MonoBehaviour
{
    public GameObject prefab;
    public int loadTimes;
    public Button spawnButton;
    public Button destroyButton;

    private readonly List<GameObject> spawned = new List<GameObject>(1024);

    void Awake()
    {
        spawnButton.onClick.AddListener(SpawnMany);
        destroyButton.onClick.AddListener(DestroyMany);
    }

    void SpawnMany()
    {
        if (prefab == null) return;
        for (int i = 0; i < loadTimes; i++)
        {
            var go = Instantiate(prefab, transform);
            go.transform.position = new Vector3(i % 100, 0, i / 100); // 示例排布
            spawned.Add(go);
        }
    }

    void DestroyMany()
    {
        for (int i = 0; i < spawned.Count; i++)
            Destroy(spawned[i]);
        spawned.Clear();
    }
}