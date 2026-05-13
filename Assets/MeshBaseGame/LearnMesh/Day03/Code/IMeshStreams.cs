using Unity.Mathematics;
using UnityEngine;

namespace MeshBaseGame.LearnMesh.Day03.Code.ProceduralMeshes
{
    /// <summary>
    /// 网格「流」：负责在 <see cref="Mesh.MeshData"/> 上声明顶点/索引格式，并把逻辑 <see cref="Vertex"/> 写入实际缓冲。
    /// </summary>
    public interface IMeshStreams
    {
        void Setup(Mesh.MeshData meshData, Bounds bounds, int vertexCount, int indexCount);

        void SetVertex(int index, Vertex data);

        /// <summary>写入第 <paramref name="index"/> 个三角形的三元顶点索引（逻辑索引，与 <see cref="SetVertex"/> 一致）。</summary>
        void SetTriangle(int index, int3 triangle);
    }
}
