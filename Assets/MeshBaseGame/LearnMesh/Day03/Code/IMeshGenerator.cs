using UnityEngine;

namespace MeshBaseGame.LearnMesh.Day03.Code.ProceduralMeshes
{
    /// <summary>
    /// 网格生成器：只描述几何与拓扑（通过 <see cref="Vertex"/> 与三角形索引），不接触顶点缓冲的具体内存布局。
    /// </summary>
    public interface IMeshGenerator
    {
        /// <summary>
        /// 泛型 <typeparamref name="S"/> 使同一生成器可配合任意 struct 流实现，且在 Burst 下仍可按具体类型特化。
        /// </summary>
        void Execute<S>(int i, S streams) where S : struct, IMeshStreams;

        /// <summary>与 <see cref="IMeshStreams.Setup"/> 中申请的顶点缓冲长度一致。</summary>
        int VertexCount { get; }

        /// <summary>索引缓冲中的标量索引个数（每三角形 3 个）。</summary>
        int IndexCount { get; }

        /// <summary><see cref="Unity.Jobs.IJobFor"/> 并行长度；含义由实现定义（如按行则为分辨率或分辨率+1）。</summary>
        int JobLength { get; }

        /// <summary>写入 <see cref="Mesh.bounds"/> 与子网格描述，避免依赖尚未填充的缓冲做包围盒/索引校验。</summary>
        Bounds Bounds { get; }

        /// <summary>示例网格使用的分辨率；调度前由 <see cref="MeshJob{G,S}.ScheduleParallel"/> 赋值。</summary>
        int Resolution { get; set; }
    }
}
