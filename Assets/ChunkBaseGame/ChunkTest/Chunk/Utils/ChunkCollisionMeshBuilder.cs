using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 职责：由 chunk-local 体素占用（整数格）生成用于 MeshCollider 的合并网格（朴素面剔除）。
public static class ChunkCollisionMeshBuilder
{
    private static readonly Vector3Int[] Neighbors =
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0),
        new Vector3Int(0, 0, 1),
        new Vector3Int(0, 0, -1),
    };

    public static Mesh Build(HashSet<Vector3Int> occupied, int sizeXZ, int heightY)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        foreach (Vector3Int c in occupied)
        {
            for (int i = 0; i < Neighbors.Length; i++)
            {
                Vector3Int d = Neighbors[i];
                int nx = c.x + d.x;
                int ny = c.y + d.y;
                int nz = c.z + d.z;
                if (IsOccupied(occupied, nx, ny, nz, sizeXZ, heightY))
                {
                    continue;
                }

                AddFace(vertices, triangles, c.x, c.y, c.z, i);
            }
        }

        var mesh = new Mesh { name = "ChunkCollisionMesh" };
        if (vertices.Count > 65535)
        {
            mesh.indexFormat = IndexFormat.UInt32;
        }

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static bool IsOccupied(HashSet<Vector3Int> occupied, int nx, int ny, int nz, int sizeXZ, int heightY)
    {
        if (nx < 0 || ny < 0 || nz < 0 || nx >= sizeXZ || nz >= sizeXZ || ny >= heightY)
        {
            return false;
        }

        return occupied.Contains(new Vector3Int(nx, ny, nz));
    }

    private static void AddFace(List<Vector3> v, List<int> t, int x, int y, int z, int dirIndex)
    {
        switch (dirIndex)
        {
            case 0: // +X
                AddQuad(v, t,
                    new Vector3(x + 1, y, z),
                    new Vector3(x + 1, y + 1, z),
                    new Vector3(x + 1, y + 1, z + 1),
                    new Vector3(x + 1, y, z + 1));
                break;
            case 1: // -X
                AddQuad(v, t,
                    new Vector3(x, y, z + 1),
                    new Vector3(x, y + 1, z + 1),
                    new Vector3(x, y + 1, z),
                    new Vector3(x, y, z));
                break;
            case 2: // +Y
                AddQuad(v, t,
                    new Vector3(x, y + 1, z),
                    new Vector3(x, y + 1, z + 1),
                    new Vector3(x + 1, y + 1, z + 1),
                    new Vector3(x + 1, y + 1, z));
                break;
            case 3: // -Y
                AddQuad(v, t,
                    new Vector3(x, y, z),
                    new Vector3(x + 1, y, z),
                    new Vector3(x + 1, y, z + 1),
                    new Vector3(x, y, z + 1));
                break;
            case 4: // +Z
                AddQuad(v, t,
                    new Vector3(x, y, z + 1),
                    new Vector3(x, y + 1, z + 1),
                    new Vector3(x + 1, y + 1, z + 1),
                    new Vector3(x + 1, y, z + 1));
                break;
            default: // -Z
                AddQuad(v, t,
                    new Vector3(x + 1, y, z),
                    new Vector3(x + 1, y + 1, z),
                    new Vector3(x, y + 1, z),
                    new Vector3(x, y, z));
                break;
        }
    }

    private static void AddQuad(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int i = verts.Count;
        verts.Add(a);
        verts.Add(b);
        verts.Add(c);
        verts.Add(d);
        tris.Add(i);
        tris.Add(i + 1);
        tris.Add(i + 2);
        tris.Add(i);
        tris.Add(i + 2);
        tris.Add(i + 3);
    }
}
