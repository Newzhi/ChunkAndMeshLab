using System;
using System.Collections.Generic;
using UnityEngine;

// 实验：Perlin + 简易 FBM 连续高度场 → 单 Mesh + MeshCollider；高度写入 ChunkObjectSaveData.terrainHeights 并由专用存储器落盘。
[Serializable]
public sealed class MeshNoiseTerrainChunkGenerator : IChunkObjectGenerator
{
    [SerializeField]
    private float heightAmplitude = 18f;

    [SerializeField]
    [Min(1)]
    private int fbmOctaves = 4;

    [SerializeField]
    private float fbmLacunarity = 2f;

    [SerializeField]
    [Range(0f, 1f)]
    private float fbmPersistence = 0.5f;

    public void LoadContent(ChunkData chunk, ChunkSettings settings, IChunkObjectStorager storager)
    {
        if (chunk is null || storager is null)
        {
            return;
        }

        EnsureChunkObjectRoot(chunk, settings);

        int size = Mathf.Max(1, chunk.Bounds.Size);
        int vertPerAxis = size + 1;
        int heightCount = vertPerAxis * vertPerAxis;

        ChunkObjectSaveData data;
        if (!storager.TryLoad(chunk.Id, settings, out data) || !TryGetValidTerrainHeights(data, heightCount))
        {
            data = new ChunkObjectSaveData
            {
                chunkId = chunk.Id,
                spawns = new List<ChunkSpawnData>(),
                terrainHeights = BuildTerrainHeights(chunk, settings, vertPerAxis)
            };
            if (settings.EnableChunkObjectDiskCache)
            {
                storager.SaveAsync(data, settings);
            }
        }

        chunk.ObjectSaveData = data;
        chunk.SpawnedInstances.Clear();

        if (data?.terrainHeights is null || data.terrainHeights.Count != heightCount)
        {
            return;
        }

        Mesh mesh = BuildTerrainMesh(data.terrainHeights, vertPerAxis);
        Transform root = chunk.ObjectRoot;
        GameObject meshGo = new GameObject("TerrainMesh");
        meshGo.transform.SetParent(root, false);
        meshGo.transform.localPosition = Vector3.zero;
        meshGo.transform.localRotation = Quaternion.identity;
        meshGo.transform.localScale = Vector3.one;

        var mf = meshGo.AddComponent<MeshFilter>();
        var mr = meshGo.AddComponent<MeshRenderer>();
        var mc = meshGo.AddComponent<MeshCollider>();
        mf.sharedMesh = mesh;
        mc.sharedMesh = mesh;
        mc.convex = false;

        Material mat = CreateTerrainMaterial();
        if (mat is not null)
        {
            mr.sharedMaterial = mat;
        }

        chunk.SpawnedInstances.Add(meshGo.transform);
    }

    public void UnloadContent(ChunkData chunk, ChunkSettings settings, IChunkObjectStorager storager)
    {
        if (chunk is null || storager is null)
        {
            return;
        }

        if (chunk.ObjectSaveData?.terrainHeights is not null && settings.EnableChunkObjectDiskCache)
        {
            storager.SaveAsync(chunk.ObjectSaveData, settings);
        }

        chunk.SpawnedInstances.Clear();
        DestroyChunkObjectRoot(chunk);
    }

    private static bool TryGetValidTerrainHeights(ChunkObjectSaveData data, int expectedCount)
    {
        return data?.terrainHeights is not null && data.terrainHeights.Count == expectedCount;
    }

    private List<float> BuildTerrainHeights(ChunkData chunk, ChunkSettings settings, int vertPerAxis)
    {
        var list = new List<float>(vertPerAxis * vertPerAxis);
        int seed = settings.WorldSeed * 739;
        float baseY = chunk.Bounds.MinY;

        for (int z = 0; z < vertPerAxis; z++)
        {
            for (int x = 0; x < vertPerAxis; x++)
            {
                int wx = chunk.Bounds.MinX + x;
                int wz = chunk.Bounds.MinZ + z;
                float n = SampleFbm(wx, wz, settings.NoiseSmoothness, seed);
                float worldY = baseY + n * heightAmplitude;
                list.Add(worldY - baseY);
            }
        }

        return list;
    }

    private float SampleFbm(int worldX, int worldZ, float smoothness, int seed)
    {
        float sum = 0f;
        float amp = 1f;
        float norm = 0f;
        float frequency = 1f / Mathf.Max(0.0001f, smoothness);

        for (int o = 0; o < fbmOctaves; o++)
        {
            float nx = (worldX + seed) * frequency * Mathf.Pow(fbmLacunarity, o);
            float nz = (worldZ + seed * 3) * frequency * Mathf.Pow(fbmLacunarity, o);
            sum += Mathf.PerlinNoise(nx, nz) * amp;
            norm += amp;
            amp *= fbmPersistence;
        }

        return norm > 0f ? Mathf.Clamp01(sum / norm) : 0f;
    }

    private static Mesh BuildTerrainMesh(IReadOnlyList<float> heights, int vertPerAxis)
    {
        int size = vertPerAxis - 1;
        var verts = new Vector3[vertPerAxis * vertPerAxis];
        var uvs = new Vector2[verts.Length];
        int vi = 0;
        for (int z = 0; z < vertPerAxis; z++)
        {
            for (int x = 0; x < vertPerAxis; x++)
            {
                verts[vi] = new Vector3(x, heights[vi], z);
                uvs[vi] = new Vector2(x / (float)size, z / (float)size);
                vi++;
            }
        }

        int quadCount = size * size;
        var indices = new int[quadCount * 6];
        int ti = 0;
        for (int z = 0; z < size; z++)
        {
            for (int x = 0; x < size; x++)
            {
                int i00 = z * vertPerAxis + x;
                int i10 = i00 + 1;
                int i01 = i00 + vertPerAxis;
                int i11 = i01 + 1;

                indices[ti++] = i00;
                indices[ti++] = i01;
                indices[ti++] = i10;
                indices[ti++] = i10;
                indices[ti++] = i01;
                indices[ti++] = i11;
            }
        }

        var mesh = new Mesh { name = $"Terrain_{vertPerAxis}" };
        mesh.indexFormat = verts.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.vertices = verts;
        mesh.triangles = indices;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateTerrainMaterial()
    {
        // URP 优先：Built-in Standard 在 URP 工程里常为粉/missing，不能先试 Standard。
        string[] shaderCandidates =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Simple Lit",
            "Universal Render Pipeline/Unlit",
            "Standard",
            "Diffuse",
        };

        Shader sh = null;
        foreach (string path in shaderCandidates)
        {
            sh = Shader.Find(path);
            if (sh is not null)
            {
                break;
            }
        }

        if (sh is null)
        {
            return null;
        }

        var mat = new Material(sh);
        Color terrainTint = new Color(0.35f, 0.55f, 0.28f, 1f);

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", terrainTint);
        }
        else
        {
            mat.color = terrainTint;
        }

        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", 0.12f);
        }

        if (mat.HasProperty("_Metallic"))
        {
            mat.SetFloat("_Metallic", 0f);
        }

        return mat;
    }

    private static void EnsureChunkObjectRoot(ChunkData chunk, ChunkSettings settings)
    {
        if (chunk.ObjectRoot is not null)
        {
            return;
        }

        Transform parent = settings.ChunkObjectParent;
        GameObject go = new GameObject($"ChunkTerrain ({chunk.Coord.X}, {chunk.Coord.Z})");
        go.transform.SetParent(parent, worldPositionStays: false);
        go.transform.position = new Vector3(chunk.Bounds.MinX, chunk.Bounds.MinY, chunk.Bounds.MinZ);
        chunk.ObjectRoot = go.transform;
    }

    private static void DestroyChunkObjectRoot(ChunkData chunk)
    {
        if (chunk.ObjectRoot is null)
        {
            return;
        }

        UnityEngine.Object.Destroy(chunk.ObjectRoot.gameObject);
        chunk.ObjectRoot = null;
    }
}
