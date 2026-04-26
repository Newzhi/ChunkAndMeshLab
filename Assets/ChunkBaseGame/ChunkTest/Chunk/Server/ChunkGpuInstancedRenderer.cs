using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 职责：在 UseGpuInstancingForChunkObjects 开启时，用 Graphics.DrawMeshInstanced 绘制区块方块（无 GameObject 实体）。
public sealed class ChunkGpuInstancedRenderer : MonoBehaviour
{
    [SerializeField] private ChunkManager chunkManager;

    private static readonly Matrix4x4[] MatrixBatch = new Matrix4x4[1023];

    private Mesh[] cachedMeshes;
    private Material[] cachedMaterials;
    private readonly List<Material> ownedMaterials = new List<Material>();
    private GameObject[] lastPrefabsRef;

    private void Awake()
    {
        if (chunkManager == null)
        {
            chunkManager = FindObjectOfType<ChunkManager>();
        }
    }

    private void OnDestroy()
    {
        foreach (Material m in ownedMaterials)
        {
            if (m != null)
            {
                Destroy(m);
            }
        }

        ownedMaterials.Clear();
    }

    private void LateUpdate()
    {
        if (chunkManager == null || !chunkManager.isActiveAndEnabled)
        {
            return;
        }

        ChunkSettings settings = chunkManager.Settings;
        if (!settings.UseGpuInstancingForChunkObjects)
        {
            return;
        }

        GameObject[] prefabs = settings.SpawnPrefabs;
        if (prefabs == null || prefabs.Length == 0)
        {
            return;
        }

        EnsurePrefabRenderCache(prefabs);

        IReadOnlyDictionary<long, ChunkData> chunks = chunkManager.Chunks;
        foreach (ChunkData chunk in chunks.Values)
        {
            if (chunk.State != ChunkState.Active)
            {
                continue;
            }

            Dictionary<int, List<Matrix4x4>> batches = chunk.GpuInstanceMatricesByPrefabIndex;
            if (batches == null || batches.Count == 0)
            {
                continue;
            }

            foreach (KeyValuePair<int, List<Matrix4x4>> kv in batches)
            {
                int prefabIndex = kv.Key;
                List<Matrix4x4> matrices = kv.Value;
                if (matrices == null || matrices.Count == 0)
                {
                    continue;
                }

                if (cachedMeshes == null || (uint)prefabIndex >= (uint)cachedMeshes.Length)
                {
                    continue;
                }

                Mesh mesh = cachedMeshes[prefabIndex];
                Material mat = cachedMaterials[prefabIndex];
                if (mesh == null || mat == null)
                {
                    continue;
                }

                DrawInstancedBatches(mesh, mat, matrices);
            }
        }
    }

    private void EnsurePrefabRenderCache(GameObject[] prefabs)
    {
        if (cachedMeshes != null
            && cachedMeshes.Length == prefabs.Length
            && ReferenceEquals(lastPrefabsRef, prefabs))
        {
            return;
        }

        foreach (Material m in ownedMaterials)
        {
            if (m != null)
            {
                Destroy(m);
            }
        }

        ownedMaterials.Clear();

        lastPrefabsRef = prefabs;
        int n = prefabs.Length;
        cachedMeshes = new Mesh[n];
        cachedMaterials = new Material[n];

        for (int i = 0; i < n; i++)
        {
            GameObject p = prefabs[i];
            if (p == null)
            {
                continue;
            }

            MeshFilter mf = p.GetComponent<MeshFilter>();
            MeshRenderer mr = p.GetComponent<MeshRenderer>();
            if (mf == null || mr == null)
            {
                Debug.LogWarning($"[ChunkGpuInstancedRenderer] spawnPrefabs[{i}] 缺少 MeshFilter 或 MeshRenderer，已跳过。");
                continue;
            }

            cachedMeshes[i] = mf.sharedMesh;
            Material shared = mr.sharedMaterial;
            if (shared == null)
            {
                continue;
            }

            if (!shared.enableInstancing)
            {
                Material clone = new Material(shared)
                {
                    name = shared.name + " (GPU Instancing)",
                    enableInstancing = true
                };
                ownedMaterials.Add(clone);
                cachedMaterials[i] = clone;
            }
            else
            {
                cachedMaterials[i] = shared;
            }
        }
    }

    private static void DrawInstancedBatches(Mesh mesh, Material material, List<Matrix4x4> matrices)
    {
        int total = matrices.Count;
        int offset = 0;
        while (offset < total)
        {
            int batch = Mathf.Min(1023, total - offset);
            for (int i = 0; i < batch; i++)
            {
                MatrixBatch[i] = matrices[offset + i];
            }

            Graphics.DrawMeshInstanced(
                mesh,
                0,
                material,
                MatrixBatch,
                batch,
                null,
                ShadowCastingMode.On,
                true,
                0,
                null);

            offset += batch;
        }
    }
}
