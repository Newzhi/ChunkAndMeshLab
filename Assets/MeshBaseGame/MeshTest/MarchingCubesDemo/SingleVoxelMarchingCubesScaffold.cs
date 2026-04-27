using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 最小单-voxel Marching Cubes 脚手架：
/// - 手动设置 8 个角点密度（与 `MarchingCubesSimple.compute` 同一角点顺序）
/// - 计算 cubeIndex → 查 TriTable → 在边上插值出交点 → 生成 Mesh
/// 适合用来“慢慢拆解”与验证表/绕序/法线方向。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class SingleVoxelMarchingCubesScaffold : MonoBehaviour
{
    [Header("Voxel（单立方体）")]
    [Min(0.0001f)] public float voxelSize = 1f;
    public Vector3 voxelOriginOffset = Vector3.zero;

    [Header("Isosurface")]
    public float isoLevel = 0f;

    [Header("角点密度（8 个，顺序必须一致）")]
    [Tooltip(
        "角点顺序与 `MarchingCubesSimple.compute` 一致：\n" +
        "0:(0,0,0) 1:(1,0,0) 2:(1,0,1) 3:(0,0,1)\n" +
        "4:(0,1,0) 5:(1,1,0) 6:(1,1,1) 7:(0,1,1)\n" +
        "密度约定：内部 > isoLevel，外部 < isoLevel；cubeIndex 使用 (density < isoLevel) 置位。"
    )]
    public float[] cornerDensities = new float[8]
    {
        // 默认给一个最简单 case：只有 corner0 在 iso 以下（cubeIndex = 1）
        -1f, 1f, 1f, 1f,
        1f, 1f, 1f, 1f
    };

    [Header("显示/调试")]
    public bool rebuildEveryFrame = false;
    public bool fixFlippedNormals = true;
    public bool drawGizmos = true;
    [Min(0.001f)] public float gizmoSphereRadius = 0.03f;

    private MeshFilter meshFilter;
    private Mesh mesh;

    private void Awake()
    {
        // 在 Play 模式下初始化组件引用。注意：编辑器下的 OnValidate 可能早于 Awake。
        meshFilter = GetComponent<MeshFilter>();
    }

    private void OnEnable()
    {
        Rebuild();
    }

    private void OnValidate()
    {
        // 编辑器热更新/改 Inspector 值时触发：这里仅做“数据修正 + 安全重建”。
        // 原因：OnValidate 常发生在 Awake 之前，此时 meshFilter 等组件引用可能尚未可用。
        if (cornerDensities == null || cornerDensities.Length != 8)
        {
            cornerDensities = new float[8];
        }

        // OnValidate 可能在 Awake 之前触发，此时组件引用尚未初始化。
        // 这里用延迟调用，避免在编辑器里修改参数时抛空引用异常。
        #if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    Rebuild();
                }
            };
            return;
        }
        #endif

        Rebuild();
    }

    private void Update()
    {
        if (rebuildEveryFrame)
        {
            Rebuild();
        }
    }

    [ContextMenu("Rebuild Single Voxel Mesh")]
    public void Rebuild()
    {
        // 这是整个演示的主流程入口：
        // 1) 组装 cubeCorners(位置+密度)
        // 2) 把 8 个角点的 inside/outside 编码成 cubeIndex(0..255)
        // 3) TriTable 查表得到“要在那些边上取交点，并如何组三角形”
        // 4) 插值求交点，写入 Mesh 顶点/索引
        if (cornerDensities == null || cornerDensities.Length != 8)
        {
            Debug.LogError("cornerDensities 必须是长度为 8 的数组。", this);
            return;
        }

        if (meshFilter == null)
        {
            // 兜底：允许在 Awake 之前也能重建（例如 OnValidate 的延迟回调）。
            meshFilter = GetComponent<MeshFilter>();
        }

        var corners = BuildCornerSamples();
        int cubeIndex = ComputeCubeIndex(corners, isoLevel);

        var vertices = new List<Vector3>(64);
        var indices = new List<int>(64);

        EmitTrianglesForCubeIndex(corners, cubeIndex, vertices, indices);
        ApplyMesh(vertices, indices);
    }

    private Vector4[] BuildCornerSamples()
    {
        // Step 1：准备这个 voxel 的 8 个角点采样 cubeCorners[8]。
        // 约定：cubeCorners[i] = (x, y, z, density)，与 compute shader 里的 float4 同义。
        //
        // 这里必须用“本地空间”坐标写入 Mesh：
        // - MeshFilter 会把 mesh.vertices 当作 local-space 顶点
        // - 渲染时再乘 transform.localToWorldMatrix
        // 若在这里直接写 world-space，会被二次变换，导致三角形看起来跑出 cell。
        Vector3 origin = voxelOriginOffset;
        float s = voxelSize;

        // 角点顺序必须与 TriTable/Edge 表匹配（这里与 `MarchingCubesSimple.compute` 一致）。
        // 一旦角点顺序错了，cubeIndex 的 bit 含义就变了，查表会“连错边”，生成的面会扭曲/穿帮。
        Vector3 p0 = origin + new Vector3(0, 0, 0) * s;
        Vector3 p1 = origin + new Vector3(1, 0, 0) * s;
        Vector3 p2 = origin + new Vector3(1, 0, 1) * s;
        Vector3 p3 = origin + new Vector3(0, 0, 1) * s;
        Vector3 p4 = origin + new Vector3(0, 1, 0) * s;
        Vector3 p5 = origin + new Vector3(1, 1, 0) * s;
        Vector3 p6 = origin + new Vector3(1, 1, 1) * s;
        Vector3 p7 = origin + new Vector3(0, 1, 1) * s;

        return new[]
        {
            new Vector4(p0.x, p0.y, p0.z, cornerDensities[0]),
            new Vector4(p1.x, p1.y, p1.z, cornerDensities[1]),
            new Vector4(p2.x, p2.y, p2.z, cornerDensities[2]),
            new Vector4(p3.x, p3.y, p3.z, cornerDensities[3]),
            new Vector4(p4.x, p4.y, p4.z, cornerDensities[4]),
            new Vector4(p5.x, p5.y, p5.z, cornerDensities[5]),
            new Vector4(p6.x, p6.y, p6.z, cornerDensities[6]),
            new Vector4(p7.x, p7.y, p7.z, cornerDensities[7]),
        };
    }

    private static int ComputeCubeIndex(Vector4[] cubeCorners, float iso)
    {
        // Step 2：把 8 个角点相对于 iso 的 inside/outside 编码成 8-bit 掩码 cubeIndex。
        //
        // cubeIndex 的每一位对应一个角点：
        // - 若 corner i 的 density < iso，则把第 i 位置为 1
        // 这样 8 个布尔值被压缩成 0..255 的整数，正好作为 TriTable 的行索引。
        int cubeIndex = 0;
        if (cubeCorners[0].w < iso) cubeIndex |= 1;
        if (cubeCorners[1].w < iso) cubeIndex |= 2;
        if (cubeCorners[2].w < iso) cubeIndex |= 4;
        if (cubeCorners[3].w < iso) cubeIndex |= 8;
        if (cubeCorners[4].w < iso) cubeIndex |= 16;
        if (cubeCorners[5].w < iso) cubeIndex |= 32;
        if (cubeCorners[6].w < iso) cubeIndex |= 64;
        if (cubeCorners[7].w < iso) cubeIndex |= 128;
        return cubeIndex;
    }

    private void EmitTrianglesForCubeIndex(
        Vector4[] cubeCorners,
        int cubeIndex,
        List<Vector3> verts,
        List<int> inds)
    {
        // Step 3：查 TriTable（256x16）。
        // 对当前 cubeIndex，TriTable 会给出一串 edgeId（0..11），每 3 个 edgeId 组成一个三角形。
        // 这些 edgeId 表达的是“拓扑连接关系”（怎么连），而不是固定的三角形坐标。
        //
        // 坐标来自下一步：对每条边做插值，求出 iso 与边的交点。
        var tri = MarchingCubesTables.Triangulation;
        for (int i = 0; tri[cubeIndex, i] != -1; i += 3)
        {
            int e0 = tri[cubeIndex, i];
            int e1 = tri[cubeIndex, i + 1];
            int e2 = tri[cubeIndex, i + 2];

            Vector3 a = InterpolateOnEdge(cubeCorners, e0, isoLevel);
            Vector3 b = InterpolateOnEdge(cubeCorners, e1, isoLevel);
            Vector3 c = InterpolateOnEdge(cubeCorners, e2, isoLevel);

            int baseV = verts.Count;
            verts.Add(a);
            if (fixFlippedNormals)
            {
                // 三角形绕序（winding）决定法线方向。
                // 若发现 RecalculateNormals() 得到的法线朝里，可以交换两个顶点来翻转绕序。
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

    private static Vector3 InterpolateOnEdge(Vector4[] cubeCorners, int edge, float iso)
    {
        // Step 4：把 edgeId（0..11）转换为“两个角点索引 (a,b)”，并在两点之间做线性插值求交点。
        // - Edge→Corner 的映射来自固定表（必须与 TriTable 使用同一套编号体系）
        // - 插值公式：t = (iso - da) / (db - da)，p = pa + t(pb - pa)
        //   直觉：密度沿边线性变化时，iso 在边上的交点也在线性比例处。
        int a = MarchingCubesTables.CornerIndexAFromEdge[edge];
        int b = MarchingCubesTables.CornerIndexBFromEdge[edge];

        Vector4 v1 = cubeCorners[a];
        Vector4 v2 = cubeCorners[b];

        float denom = (v2.w - v1.w);
        // 数值保护：当 denom 很小会导致 t 不稳定/NaN，这里退化为边中点。
        float t = Mathf.Abs(denom) < 1e-6f ? 0.5f : (iso - v1.w) / denom;
        t = Mathf.Clamp01(t);

        Vector3 p1 = new Vector3(v1.x, v1.y, v1.z);
        Vector3 p2 = new Vector3(v2.x, v2.y, v2.z);
        return Vector3.LerpUnclamped(p1, p2, t);
    }

    private void ApplyMesh(List<Vector3> vertices, List<int> triangles)
    {
        // Step 5：把三角形列表写进 Unity Mesh。
        // 这里使用“非共享顶点”的最小方式（每个三角形 3 个独立顶点），便于理解。
        // 你后续优化时可以引入 edge-cache/顶点焊接以减少重复顶点并获得更平滑法线。
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
        }

        if (meshFilter == null)
        {
            return;
        }

        if (mesh == null)
        {
            mesh = new Mesh { name = "SingleVoxelMarchingCubes" };
        }

        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0, true);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        meshFilter.sharedMesh = mesh;
    }

    private void OnDrawGizmos()
    {
        // 辅助可视化：把“采样点的 inside/outside + 当前 cubeIndex”直接画在 Scene 里，
        // 帮你把“密度 → cubeIndex → 三角形”这条链路建立直觉。
        if (!drawGizmos || cornerDensities == null || cornerDensities.Length != 8)
        {
            return;
        }

        var corners = BuildCornerSamples();
        int cubeIndex = ComputeCubeIndex(corners, isoLevel);

        // corners 是本地空间坐标，因此 Gizmos 也用本地到世界的矩阵显示
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.white;
        Gizmos.DrawWireCube(voxelOriginOffset + new Vector3(0.5f, 0.5f, 0.5f) * voxelSize, Vector3.one * voxelSize);

        for (int i = 0; i < 8; i++)
        {
            bool below = corners[i].w < isoLevel;
            Gizmos.color = below ? new Color(1f, 0.4f, 0.4f) : new Color(0.4f, 1f, 0.4f);
            Gizmos.DrawSphere(new Vector3(corners[i].x, corners[i].y, corners[i].z), gizmoSphereRadius);
        }

        // 在 Scene 视图里快速看到当前 case
        #if UNITY_EDITOR
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.Label(transform.TransformPoint(voxelOriginOffset), $"cubeIndex: {cubeIndex}");
        #endif
    }
}

