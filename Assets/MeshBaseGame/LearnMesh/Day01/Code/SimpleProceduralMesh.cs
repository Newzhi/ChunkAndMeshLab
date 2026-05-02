using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SimpleProceduralMesh : MonoBehaviour
{
    [Header("setting")]
    public Vector3[] verts;
    public int[] triangles;
    public Vector3[] normals;
    public Vector2[] uvs;

    [Header("quad")]
    [SerializeField] private Vector2 quadSize = Vector2.one;

    public Mesh mesh;

    #region 生命事件函数

    /// <summary>
    /// Unity 调用顺序为 Awake → OnEnable → Start。若在 OnEnable 里调用 DrawMesh，
    /// 必须先已通过 BuildQuad 写好 verts/triangles，否则会绘制空数据。
    /// </summary>
    private void OnEnable()
    {
        EnsureMesh();
        BuildQuad(quadSize);
        DrawMesh();
    }

    private void Start()
    {
        
    }

    private void Update()
    {
    }

    private void OnDestroy()
    {
        if (mesh != null)
        {
            Destroy(mesh);
            mesh = null;
        }
    }

    #endregion

    private void EnsureMesh()
    {
        if (mesh == null)
        {
            mesh = new Mesh { name = "SimpleQuadMesh" };
        }
    }

    private void DrawMesh()
    {
        EnsureMesh();
        if (verts == null || triangles == null || uvs == null) return;

        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = triangles;
        mesh.uv = uvs;

        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        normals = mesh.normals;

        var mf = GetComponent<MeshFilter>();
        mf.mesh = mesh;
    }

    /// <summary>
    /// XY 平面上的矩形面片，居中于原点；正面朝向 +Z（Unity 默认剔除背面）。
    /// UV：(0,0)-(1,1) 铺满四角。
    /// </summary>
    private void BuildQuad(Vector2 size)
    {
        mesh.vertices = new Vector3[] {
            Vector3.zero, Vector3.right, Vector3.up, new Vector3(1f, 1f)
            //new Vector3(1.1f, 0f), new Vector3(0f, 1.1f), new Vector3(1.1f, 1.1f)
        };

        mesh.normals = new Vector3[] {
            Vector3.back, Vector3.back, Vector3.back, Vector3.back
            //Vector3.back, Vector3.back, Vector3.back,
        };

        mesh.triangles = new int[] {
            0, 2, 1, 1, 2, 3
        };

        mesh.uv = new Vector2[] {
            Vector2.zero, Vector2.right, Vector2.up, Vector2.one
            //Vector2.right, Vector2.up, Vector2.one
        };
    }

    #region 调试方法

    #endregion
}

