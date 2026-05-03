using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MeshBaseGame.LearnMesh.Day02.ProceduralMeshes
{
    /// <summary>
    /// Burst <see cref="IJobFor"/>：把并行下标转发给生成器，由生成器通过 <see cref="IMeshStreams"/> 写入 <see cref="Mesh.MeshData"/>。
    /// </summary>
    /// <remarks>
    /// <para><b>与网格生成的关系</b></para>
    /// <para>
    /// 实际「几何长什么样」由 <typeparamref name="G"/>（如 <c>SquareGrid</c>）在 <see cref="IMeshGenerator.Execute{S}"/> 中计算；
    /// 「顶点/索引在缓冲里如何排版」由 <typeparamref name="S"/>（如 <c>MultiStream</c>）在 <see cref="IMeshStreams.Setup"/> / <c>Set*</c> 中完成。
    /// 本类型只负责 <b>并行调度</b> 与 <b>调度前初始化</b>。
    /// </para>
    /// <para><b>原理</b></para>
    /// <list type="bullet">
    /// <item><typeparamref name="G"/>、<typeparamref name="S"/> 均为 struct：Job 可按具体类型 Burst 编译，避免接口虚调用。</item>
    /// <item><see cref="ScheduleParallel"/> 必须先配置生成器参数（如 <see cref="IMeshGenerator.Resolution"/>），再调用 <see cref="IMeshStreams.Setup"/>，否则 <see cref="IMeshGenerator.VertexCount"/> 等与缓冲长度不一致会导致写入越界或未定义。</item>
    /// <item><see cref="IMeshStreams.Setup"/> 在 Job 运行前于调度线程执行：先声明顶点/索引缓冲大小与子网格，再取得 <c>NativeArray</c> 视图；随后各 worker 仅写入这些视图。</item>
    /// </list>
    /// <para><b>流程（从 ScheduleParallel 到网格数据就绪）</b></para>
    /// <list type="number">
    /// <item>写入 <c>job.generator.Resolution</c>（或其它由调度侧传入的配置），使 <see cref="IMeshGenerator.VertexCount"/> / <see cref="IMeshGenerator.IndexCount"/> / <see cref="IMeshGenerator.Bounds"/> 与目标网格一致。</item>
    /// <item><c>job.streams.Setup(meshData, mesh.bounds = generator.Bounds, VertexCount, IndexCount)</c>：在 <paramref name="meshData"/> 上分配缓冲、设置子网格标志，并缓存写入视图；同时设置目标 <see cref="Mesh.bounds"/>。</item>
    /// <item><c>ScheduleParallel(JobLength, 1, dependency)</c>：启动 <c>JobLength</c> 个并行任务；每个任务调用 <see cref="Execute"/> → <c>generator.Execute(i, streams)</c>，其中 <c>i</c> 对 <c>SquareGrid</c> 表示行号 <c>z</c>。</item>
    /// <item>调用方对返回的 <see cref="JobHandle"/> 执行 <c>Complete()</c> 后，缓冲内容完整；再由 <c>Mesh.ApplyAndDisposeWritableMeshData</c> 提交到 <see cref="Mesh"/>（见场景侧组件）。</item>
    /// </list>
    /// </remarks>
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    public struct MeshJob<G, S> : IJobFor
        where G : struct, IMeshGenerator
        where S : struct, IMeshStreams
    {
        public G generator;

        /// <summary>仅写入网格缓冲，便于 Job 依赖分析与优化。</summary>
        [WriteOnly]
        public S streams;

        /// <summary>第 <paramref name="i"/> 个并行单位（含义由 <typeparamref name="G"/> 定义，如按行的 z）。</summary>
        public void Execute(int i) => generator.Execute(i, streams);

        /// <summary>
        /// 调度入口：配置分辨率 → Setup 缓冲 → 并行执行生成器。
        /// </summary>
        public static JobHandle ScheduleParallel(
            Mesh mesh,
            Mesh.MeshData meshData,
            int resolution,
            JobHandle dependency
        )
        {
            var job = new MeshJob<G, S>();
            // 须先于 VertexCount / IndexCount 的读取，否则计数仍为默认 Resolution。
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
