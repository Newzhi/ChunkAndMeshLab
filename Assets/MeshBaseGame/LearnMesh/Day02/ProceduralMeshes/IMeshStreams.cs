using Unity.Mathematics;
using UnityEngine;

namespace MeshBaseGame.LearnMesh.Day02.ProceduralMeshes
{
    /// <summary>
    /// 网格「流」：负责在 <see cref="Mesh.MeshData"/> 上声明顶点/索引格式，并把逻辑 <see cref="Vertex"/> 写入实际缓冲。
    /// 不同实现（如单流 / 多流）可互换，生成器代码无需修改。
    /// </summary>
    public interface IMeshStreams
    {
        /// <summary>
        /// 配置顶点缓冲布局、索引格式、子网格，并缓存 <see cref="Mesh.MeshData.GetVertexData{T}"/> / <see cref="Mesh.MeshData.GetIndexData{T}"/> 视图。
        /// 需在 Job 写入顶点与三角形之前调用。
        /// </summary>
        void Setup(Mesh.MeshData meshData, Bounds bounds, int vertexCount, int indexCount);

        void SetVertex(int index, Vertex data);

        /// <summary>写入第 <paramref name="index"/> 个三角形的三元顶点索引（逻辑索引，与 <see cref="SetVertex"/> 一致）。</summary>
        void SetTriangle(int index, int3 triangle);
    }
}
