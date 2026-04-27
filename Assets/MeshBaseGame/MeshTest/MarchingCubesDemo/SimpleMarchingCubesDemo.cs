using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 最小演示：CPU 填充标量密度场（float4.xyzw，w 为密度）→ GPU Marching Cubes → 回读三角形生成 Mesh。
/// 密度约定：实体内部 w 大于 isoLevel，外部 w 小于 isoLevel（与 shader 中 cubeIndex 位掩码一致）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class SimpleMarchingCubesDemo : MonoBehaviour
{
    const int ThreadGroupSize = 8;
    const int MarchKernel = 0;

    [SerializeField] private ComputeShader marchingShader;

    [Header("教学模式（CPU 协程逐步生成）")]
    public bool stepByStep = false;

    [Tooltip("每次停顿的秒数。0 表示不暂停（但仍按批次更新 Mesh）。")]
    [Min(0f)] public float stepIntervalSeconds = 0.05f;

    [Tooltip("每次停顿前，处理多少个 voxel（越小越慢、越易看清过程）。")]
    [Min(1)] public int voxelsPerStep = 64;

    [Tooltip("每个 step 结束时就更新 Mesh（更直观，但更慢）。")]
    public bool updateMeshEachStep = true;

    [Tooltip("修正法线方向：交换三角形绕序（推荐）。")]
    public bool fixFlippedNormals = true;

    [Header("采样网格")]
    [Min(2)] public int numPointsPerAxis = 32;

    /// <summary>世界空间下采样立方体每条边的长度（角点均匀分布在 [0, extent]^3）。</summary>
    [Min(0.0001f)] public float gridExtent = 3f;

    [Header("等值面")]
    public float isoLevel = 0f;

    [Header("隐式球体（metaball 式密度：内部为正）")]
    public Vector3 sphereCenter = new Vector3(1.5f, 1.5f, 1.5f);
    [Min(0.01f)] public float sphereRadius = 1.1f;

    private MeshFilter meshFilter;
    private ComputeBuffer pointsBuffer;
    private ComputeBuffer triangleBuffer;
    private ComputeBuffer triangleCountBuffer;
    private Mesh mesh;
    private Coroutine buildRoutine;

    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
    }

    private void Start()
    {
        RebuildMesh();
    }

    private void OnDestroy()
    {
        ReleaseBuffers();
        if (mesh != null)
        {
            Destroy(mesh);
        }
    }

    [ContextMenu("Rebuild Mesh")]
    public void RebuildMesh()
    {
        if (buildRoutine != null)
        {
            StopCoroutine(buildRoutine);
            buildRoutine = null;
        }

        if (stepByStep)
        {
            ReleaseBuffers();
            buildRoutine = StartCoroutine(RebuildMeshStepByStep());
            return;
        }

        if (marchingShader == null)
        {
            Debug.LogError("SimpleMarchingCubesDemo: 请指定 MarchingCubesSimple ComputeShader。", this);
            return;
        }

        int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
        int numVoxelsPerAxis = numPointsPerAxis - 1;
        int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
        int maxTriangleCount = numVoxels * 5;

        ReleaseBuffers();
        triangleBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);
        pointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
        triangleCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

        float spacing = gridExtent / Mathf.Max(1, numPointsPerAxis - 1);
        var origin = transform.position;
        var points = new Vector4[numPoints];
        int i = 0;
        for (int z = 0; z < numPointsPerAxis; z++)
        {
            for (int y = 0; y < numPointsPerAxis; y++)
            {
                for (int x = 0; x < numPointsPerAxis; x++)
                {
                    var p = origin + new Vector3(x, y, z) * spacing;
                    float density = sphereRadius - Vector3.Distance(p, origin + sphereCenter);
                    points[i++] = new Vector4(p.x, p.y, p.z, density);
                }
            }
        }

        pointsBuffer.SetData(points);

        triangleBuffer.SetCounterValue(0);
        marchingShader.SetBuffer(MarchKernel, "points", pointsBuffer);
        marchingShader.SetBuffer(MarchKernel, "triangles", triangleBuffer);
        marchingShader.SetInt("numPointsPerAxis", numPointsPerAxis);
        marchingShader.SetFloat("isoLevel", isoLevel);

        int groups = Mathf.CeilToInt(numVoxelsPerAxis / (float)ThreadGroupSize);
        marchingShader.Dispatch(MarchKernel, groups, groups, groups);

        ComputeBuffer.CopyCount(triangleBuffer, triangleCountBuffer, 0);
        var countArr = new int[1];
        triangleCountBuffer.GetData(countArr);
        int numTris = countArr[0];

        var tris = new GpuTriangle[numTris];
        if (numTris > 0)
        {
            triangleBuffer.GetData(tris, 0, 0, numTris);
        }

        var vertices = new Vector3[numTris * 3];
        var triangles = new int[numTris * 3];
        for (int t = 0; t < numTris; t++)
        {
            int baseV = t * 3;
            // Unity 默认顺时针/逆时针与表的定义可能相反；这里用交换绕序来修复法线方向。
            vertices[baseV] = tris[t].a;
            vertices[baseV + 1] = fixFlippedNormals ? tris[t].c : tris[t].b;
            vertices[baseV + 2] = fixFlippedNormals ? tris[t].b : tris[t].c;
            triangles[baseV] = baseV;
            triangles[baseV + 1] = baseV + 1;
            triangles[baseV + 2] = baseV + 2;
        }

        if (mesh == null)
        {
            mesh = new Mesh { name = "MarchingCubesDemo" };
        }

        mesh.Clear();
        mesh.indexFormat = vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
    }

    private IEnumerator RebuildMeshStepByStep()
    {
        int n = numPointsPerAxis;
        if (n < 2)
        {
            yield break;
        }

        float spacing = gridExtent / Mathf.Max(1, n - 1);
        var origin = transform.position;
        int numPoints = n * n * n;
        var points = new Vector4[numPoints];

        int pi = 0;
        for (int z = 0; z < n; z++)
        {
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    var p = origin + new Vector3(x, y, z) * spacing;
                    float density = sphereRadius - Vector3.Distance(p, origin + sphereCenter);
                    points[pi++] = new Vector4(p.x, p.y, p.z, density);
                }
            }
        }

        var vertices = new List<Vector3>(1024);
        var indices = new List<int>(1024);

        int voxelCountSinceYield = 0;
        int voxelsPerAxis = n - 1;

        for (int z = 0; z < voxelsPerAxis; z++)
        {
            for (int y = 0; y < voxelsPerAxis; y++)
            {
                for (int x = 0; x < voxelsPerAxis; x++)
                {
                    EmitVoxelTriangles(points, n, x, y, z, isoLevel, vertices, indices);

                    voxelCountSinceYield++;
                    if (voxelCountSinceYield >= voxelsPerStep)
                    {
                        voxelCountSinceYield = 0;
                        if (updateMeshEachStep)
                        {
                            ApplyMesh(vertices, indices);
                        }

                        if (stepIntervalSeconds > 0f)
                        {
                            yield return new WaitForSeconds(stepIntervalSeconds);
                        }
                        else
                        {
                            yield return null;
                        }
                    }
                }
            }
        }

        ApplyMesh(vertices, indices);
        buildRoutine = null;
    }

    private void EmitVoxelTriangles(
        Vector4[] points,
        int numPointsPerAxis,
        int x,
        int y,
        int z,
        float iso,
        List<Vector3> verts,
        List<int> inds)
    {
        Vector4 c0 = points[IndexFromCoord(numPointsPerAxis, x, y, z)];
        Vector4 c1 = points[IndexFromCoord(numPointsPerAxis, x + 1, y, z)];
        Vector4 c2 = points[IndexFromCoord(numPointsPerAxis, x + 1, y, z + 1)];
        Vector4 c3 = points[IndexFromCoord(numPointsPerAxis, x, y, z + 1)];
        Vector4 c4 = points[IndexFromCoord(numPointsPerAxis, x, y + 1, z)];
        Vector4 c5 = points[IndexFromCoord(numPointsPerAxis, x + 1, y + 1, z)];
        Vector4 c6 = points[IndexFromCoord(numPointsPerAxis, x + 1, y + 1, z + 1)];
        Vector4 c7 = points[IndexFromCoord(numPointsPerAxis, x, y + 1, z + 1)];

        Vector4[] cubeCorners = { c0, c1, c2, c3, c4, c5, c6, c7 };

        int cubeIndex = 0;
        if (cubeCorners[0].w < iso) cubeIndex |= 1;
        if (cubeCorners[1].w < iso) cubeIndex |= 2;
        if (cubeCorners[2].w < iso) cubeIndex |= 4;
        if (cubeCorners[3].w < iso) cubeIndex |= 8;
        if (cubeCorners[4].w < iso) cubeIndex |= 16;
        if (cubeCorners[5].w < iso) cubeIndex |= 32;
        if (cubeCorners[6].w < iso) cubeIndex |= 64;
        if (cubeCorners[7].w < iso) cubeIndex |= 128;

        var tri = MarchingCubesTables.Triangulation;
        for (int i = 0; tri[cubeIndex, i] != -1; i += 3)
        {
            int e0 = tri[cubeIndex, i];
            int e1 = tri[cubeIndex, i + 1];
            int e2 = tri[cubeIndex, i + 2];

            Vector3 a = InterpolateOnEdge(cubeCorners, e0, iso);
            Vector3 b = InterpolateOnEdge(cubeCorners, e1, iso);
            Vector3 c = InterpolateOnEdge(cubeCorners, e2, iso);

            int baseV = verts.Count;
            verts.Add(a);
            if (fixFlippedNormals)
            {
                verts.Add(c);
                verts.Add(b);
            }
            else
            {
                verts.Add(b);
                verts.Add(c);
            }

            inds.Add(baseV);
            inds.Add(baseV + 1);
            inds.Add(baseV + 2);
        }
    }

    private static int IndexFromCoord(int numPointsPerAxis, int x, int y, int z)
        => z * numPointsPerAxis * numPointsPerAxis + y * numPointsPerAxis + x;

    private static Vector3 InterpolateOnEdge(Vector4[] cubeCorners, int edge, float iso)
    {
        int a = MarchingCubesTables.CornerIndexAFromEdge[edge];
        int b = MarchingCubesTables.CornerIndexBFromEdge[edge];
        Vector4 v1 = cubeCorners[a];
        Vector4 v2 = cubeCorners[b];

        float denom = (v2.w - v1.w);
        float t = Mathf.Abs(denom) < 1e-6f ? 0.5f : (iso - v1.w) / denom;
        t = Mathf.Clamp01(t);

        Vector3 p1 = new Vector3(v1.x, v1.y, v1.z);
        Vector3 p2 = new Vector3(v2.x, v2.y, v2.z);
        return Vector3.LerpUnclamped(p1, p2, t);
    }

    private void ApplyMesh(List<Vector3> vertices, List<int> triangles)
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "MarchingCubesDemo" };
        }

        mesh.Clear();
        mesh.indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
    }

    private void ReleaseBuffers()
    {
        pointsBuffer?.Release();
        triangleBuffer?.Release();
        triangleCountBuffer?.Release();
        pointsBuffer = null;
        triangleBuffer = null;
        triangleCountBuffer = null;
    }

    private struct GpuTriangle
    {
        public Vector3 a;
        public Vector3 b;
        public Vector3 c;
    }
}
