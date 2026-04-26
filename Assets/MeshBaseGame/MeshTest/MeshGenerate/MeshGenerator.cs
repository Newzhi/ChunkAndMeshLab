using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshGenerator : MonoBehaviour
{
    #region 变量定义
    [Header("网格长宽")]
    [Min(1)] public int XSize = 20;
    [Min(1)] public int ZSize = 20;

    [Header("生成过程(协程可视化)")]
    public bool stepByTriangle = true; // true: 一个三角形一步；false: 一个格子(两个三角形)一步
    [Min(0f)] public float waitForSec = 0.1f;
    
    [Header("Vertex Debug")]
    public bool drawVertexGizmos = true;
    [Min(0.001f)] public float vertexGizmoRadius = 0.06f;
    public Gradient vertexColorGradient;

    //私有
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    
    private Vector3[] vertices;
    private int[] triangles;
    private Coroutine buildRoutine;
    #endregion

    #region 生命周期
    private void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (vertexColorGradient == null) vertexColorGradient = new Gradient();

        mesh = new Mesh { name = "Generated Grid" };
        meshFilter.sharedMesh = mesh;
    }

    private void Start()
    {
        StartBuild();
    }
    

    private void OnDestroy()
    {
        if (buildRoutine != null) StopCoroutine(buildRoutine);
    }
    #endregion

    #region 生成方法

    [ContextMenu("Start Build")]
    public void StartBuild()
    {
        XSize = Mathf.Max(1, XSize);
        ZSize = Mathf.Max(1, ZSize);

        if (buildRoutine != null) StopCoroutine(buildRoutine);
        buildRoutine = StartCoroutine(BuildRoutine());
    }

    private IEnumerator BuildRoutine()
    {
        CreateVertices();
        triangles = new int[XSize * ZSize * 6];

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.RecalculateBounds();

        int vertCountX = XSize + 1;
        int vert = 0;
        int ti = 0;
        var wait = waitForSec > 0f ? new WaitForSeconds(waitForSec) : null;

        for (int z = 0; z < ZSize; z++)
        {
            for (int x = 0; x < XSize; x++)
            {
                if (stepByTriangle)
                {
                    // tri 1
                    triangles[ti + 0] = vert;
                    triangles[ti + 1] = vert + XSize + 1;
                    triangles[ti + 2] = vert + 1;
                    ti += 3;
                    ApplyTrianglesPartial(ti);
                    yield return wait;

                    // tri 2
                    triangles[ti + 0] = vert + 1;
                    triangles[ti + 1] = vert + XSize + 1;
                    triangles[ti + 2] = vert + XSize + 2;
                    ti += 3;
                    ApplyTrianglesPartial(ti);
                    yield return wait;
                }
                else
                {
                    triangles[ti + 0] = vert;
                    triangles[ti + 1] = vert + XSize + 1;
                    triangles[ti + 2] = vert + 1;
                    triangles[ti + 3] = vert + 1;
                    triangles[ti + 4] = vert + XSize + 1;
                    triangles[ti + 5] = vert + XSize + 2;
                    ti += 6;
                    ApplyTrianglesPartial(ti);
                    yield return wait;
                }

                vert++;
            }
            vert++; // 每行结束：跳过最右边界顶点
        }

        mesh.RecalculateNormals();
        buildRoutine = null;
    }
    
    private void CreateVertices()
    {
        vertices = new Vector3[(XSize + 1) * (ZSize + 1)];
        for (int z = 0, i = 0; z <= ZSize; z++)
        {
            for (int x = 0; x <= XSize; x++)
            {
                vertices[i++] = new Vector3(x, 0f, z);
            }
        }
    }

    private void ApplyTrianglesPartial(int triangleIndexCount)
    {
        // 只提交已生成的索引数，避免未填充区域(默认 0)带来不必要的处理
        mesh.SetTriangles(triangles, 0, triangleIndexCount, 0, true);
        mesh.RecalculateBounds();
    }
    
    #endregion

    #region 调试方法
    private void OnDrawGizmos()
    {
        if (!drawVertexGizmos)
        {
            return;
        }

        if (vertices == null || vertices.Length == 0)
        {
            return;
        }

        // 以物体 Transform 为基准显示顶点
        for (int i = 0; i < vertices.Length; i++)
        {
            if (vertexColorGradient != null)
            {
                float t = (vertices.Length <= 1) ? 0f : (i / (float)(vertices.Length - 1));
                Gizmos.color = vertexColorGradient.Evaluate(t);
            }
            Gizmos.DrawSphere(transform.TransformPoint(vertices[i]), vertexGizmoRadius);
        }
    }
    #endregion
}
