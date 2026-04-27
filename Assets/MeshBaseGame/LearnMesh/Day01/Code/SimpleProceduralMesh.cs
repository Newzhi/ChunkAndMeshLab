using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SimpleProceduralMesh : MonoBehaviour
{
    [Header("setting")]
    public Vector3[] verts;
    public int[]  triangles;
    public Vector3[] normals;

    [Header("cube")]
    [SerializeField] private bool buildCubeOnStart = true;
    [SerializeField] private Vector3 cubeSize = Vector3.one;
    
    public Mesh mesh;

    #region 生命事件函数

    void Start()
    {
        mesh = new Mesh() { name = "Mesh" };
        if (buildCubeOnStart)
        {
            BuildCube(cubeSize);
        }
        DrawMesh();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    #endregion
    
    #region 
    
    private void DrawMesh()
    {
        if (mesh == null)
        {
            mesh = new Mesh() { name = "Mesh" };
        }

        mesh.Clear();
        mesh.vertices = verts;
        mesh.triangles = triangles;

        if (normals != null && normals.Length == verts.Length)
        {
            mesh.normals = normals;
        }
        else
        {
            mesh.RecalculateNormals();
        }

        mesh.RecalculateBounds();

        GetComponent<MeshFilter>().mesh = mesh;
    }

    private void BuildCube(Vector3 size)
    {
        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;
        float hz = size.z * 0.5f;

        // 24 vertices (4 per face) so the cube keeps hard edges under lighting.
        verts = new Vector3[]
        {
            // +Z (front)
            new Vector3(-hx, -hy,  hz),
            new Vector3( hx, -hy,  hz),
            new Vector3( hx,  hy,  hz),
            new Vector3(-hx,  hy,  hz),

            // -Z (back)
            new Vector3( hx, -hy, -hz),
            new Vector3(-hx, -hy, -hz),
            new Vector3(-hx,  hy, -hz),
            new Vector3( hx,  hy, -hz),

            // +X (right)
            new Vector3( hx, -hy,  hz),
            new Vector3( hx, -hy, -hz),
            new Vector3( hx,  hy, -hz),
            new Vector3( hx,  hy,  hz),

            // -X (left)
            new Vector3(-hx, -hy, -hz),
            new Vector3(-hx, -hy,  hz),
            new Vector3(-hx,  hy,  hz),
            new Vector3(-hx,  hy, -hz),

            // +Y (top)
            new Vector3(-hx,  hy,  hz),
            new Vector3( hx,  hy,  hz),
            new Vector3( hx,  hy, -hz),
            new Vector3(-hx,  hy, -hz),

            // -Y (bottom)
            new Vector3(-hx, -hy, -hz),
            new Vector3( hx, -hy, -hz),
            new Vector3( hx, -hy,  hz),
            new Vector3(-hx, -hy,  hz),
        };

        triangles = new int[]
        {
            // front (+Z)
            0, 1, 2,
            0, 2, 3,

            // back (-Z)
            4, 5, 6,
            4, 6, 7,

            // right (+X)
            8, 9, 10,
            8, 10, 11,

            // left (-X)
            12, 13, 14,
            12, 14, 15,

            // top (+Y)
            16, 17, 18,
            16, 18, 19,

            // bottom (-Y)
            20, 21, 22,
            20, 22, 23,
        };

        normals = new Vector3[]
        {
            // front
            Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
            // back
            Vector3.back, Vector3.back, Vector3.back, Vector3.back,
            // right
            Vector3.right, Vector3.right, Vector3.right, Vector3.right,
            // left
            Vector3.left, Vector3.left, Vector3.left, Vector3.left,
            // top
            Vector3.up, Vector3.up, Vector3.up, Vector3.up,
            // bottom
            Vector3.down, Vector3.down, Vector3.down, Vector3.down,
        };
    }
    
    #endregion
    
    #region 调试方法

    private void OnDrawGizmos()
    {
        //Debug.Log("OnDrawGizmos");
    }
    
    #endregion

}
