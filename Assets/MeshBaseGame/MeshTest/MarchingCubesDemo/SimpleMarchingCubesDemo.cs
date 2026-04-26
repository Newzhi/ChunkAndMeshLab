using UnityEngine;
using UnityEngine.Rendering;

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
            vertices[baseV] = tris[t].a;
            vertices[baseV + 1] = tris[t].b;
            vertices[baseV + 2] = tris[t].c;
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
