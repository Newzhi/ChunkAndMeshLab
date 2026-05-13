using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MeshBaseGame.LearnMesh.Day03.Code.ProceduralMeshes
{
    /// <summary>
    /// 与 <see cref="MeshJob{G,S}.ScheduleParallel"/> 签名一致的调度委托，用于在 Inspector 中选择不同生成器。
    /// </summary>
    public delegate JobHandle MeshJobScheduleDelegate(
        Mesh mesh,
        Mesh.MeshData meshData,
        int resolution,
        JobHandle dependency
    );

    /// <summary>
    /// Burst <see cref="IJobFor"/>：把并行下标转发给生成器，由生成器通过 <see cref="IMeshStreams"/> 写入 <see cref="Mesh.MeshData"/>。
    /// </summary>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    public struct MeshJob<G, S> : IJobFor
        where G : struct, IMeshGenerator
        where S : struct, IMeshStreams
    {
        public G generator;

        [WriteOnly]
        public S streams;

        public void Execute(int i) => generator.Execute(i, streams);

        public static JobHandle ScheduleParallel(
            Mesh mesh,
            Mesh.MeshData meshData,
            int resolution,
            JobHandle dependency
        )
        {
            var job = new MeshJob<G, S>();
            job.generator.Resolution = resolution;
            job.streams.Setup(
                meshData,
                mesh.bounds = job.generator.Bounds,
                job.generator.VertexCount,
                job.generator.IndexCount
            );
            return job.ScheduleParallel(job.generator.JobLength, 1, dependency);
        }
    }
}
