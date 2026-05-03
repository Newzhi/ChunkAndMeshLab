using MeshBaseGame.LearnMesh.Day02.ProceduralMeshes;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace MeshBaseGame.LearnMesh.Day02.ProceduralMeshes.Generators
{
    /// <summary>
    /// XZ 平面上的 R×R 四边形网格生成器（R = <see cref="Resolution"/>）。
    /// </summary>
    /// <remarks>
    /// <para><b>原理（几何）</b></para>
    /// <list type="bullet">
    /// <item>在整数域上取列索引 <c>x</c>、行索引 <c>z</c>，各从 0 到 R−1，共 <c>R×R</c> 个「格子」。</item>
    /// <item>每个格子在 XZ 平面上是一块轴对齐四边形：X 方向跨度 <c>[x/R,(x+1)/R]</c>，Z 方向 <c>[z/R,(z+1)/R]</c>；再整体减 <c>0.5</c>，使网格居中于原点附近，水平范围约 <c>[-0.5,0.5]</c>（与 <see cref="Bounds"/> 一致）。</item>
    /// <item>Y 分量恒为 0：网格落在水平面内。</item>
    /// <item>每个格子使用 <b>4 个独立顶点</b>（相邻格子不共享顶点），故总顶点数 <c>4·R²</c>。</item>
    /// <item>每个格子用 <b>2 个三角形</b> 剖分四边形，共 <c>2·R²</c> 个三角形；索引缓冲以「标量索引」计为 <c>6·R²</c>（每三角形 3 个索引）。</item>
    /// <item>法线为平面法线：统一 <c>(0,1,0)</c>（未做「由三角形叉积累加」的数值解算，因整体为平坦 XZ 平面）。</item>
    /// </list>
    /// <para><b>流程（执行）</b></para>
    /// <list type="number">
    /// <item><see cref="IMeshGenerator.JobLength"/> = R：并行下标 <paramref name="z"/> 表示「第 z 行」，一次 <see cref="Execute"/> 处理该行所有列 <c>x</c>。</item>
    /// <item>行首全局顶点索引 <c>vi = 4·R·z</c>，行首三角形槽位 <c>ti = 2·R·z</c>（每格占用 4 顶点、2 三角形）。</item>
    /// <item>对 <c>x = 0…R−1</c>：根据 <c>xCoordinates</c>、<c>zCoordinates</c> 写出四角 <see cref="Vertex"/>（位置与 UV），再写入两个三角形的顶点索引（相对本格 + <c>vi</c>）。</item>
    /// <item>每格结束后 <c>vi += 4</c>、<c>ti += 2</c>，供下一格使用。</item>
    /// </list>
    /// </remarks>
    public struct SquareGrid : IMeshGenerator
    {
        public int Resolution { get; set; }

        /// <summary>每格 4 顶点，共 R² 格 → 4·R²。</summary>
        public int VertexCount => 4 * Resolution * Resolution;

        /// <summary>每格 2 三角形 × 3 索引 × R² 格 → 6·R²。</summary>
        public int IndexCount => 6 * Resolution * Resolution;

        /// <summary>按行并行：R 次调度，每次处理一整行格子。</summary>
        public int JobLength => Resolution;

        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(1f, 0f, 1f));

        public void Execute<S>(int z, S streams) where S : struct, IMeshStreams
        {
            // 第 z 行：全局起始顶点索引 / 三角形索引（每格 4 顶点、2 三角形）。
            int vi = 4 * Resolution * z;
            int ti = 2 * Resolution * z;

            var vertex = new Vertex();
            // 平坦朝上平面：与 (+Y) 一致，无需 RecalculateNormals。
            vertex.normal.y = 1f;
            // Tangent：xyz 为切线方向（此处 xy 默认 0，配合 shader）；xw 常用来存 handedness（与法线、UV 配合）。
            vertex.tangent.xw = float2(1f, -1f);

            // 本行在 Z 上的区间两端（已归一化并居中）；与循环内的 xCoordinates 对称。
            float2 zCoordinates = float2(z, z + 1f) / Resolution - 0.5f;

            for (int x = 0; x < Resolution; x++, vi += 4, ti += 2)
            {
                // 本格在 X 上的区间两端：[x/R - 0.5, (x+1)/R - 0.5]
                float2 xCoordinates = float2(x, x + 1f) / Resolution - 0.5f;

                // 四边形四角局部编号 0..3（逆时针/与下面两个三角形绕序一致）：
                // 0:(xmin,zmin)  1:(xmax,zmin)  2:(xmin,zmax)  3:(xmax,zmax)
                vertex.position.x = xCoordinates.x;
                vertex.position.z = zCoordinates.x;
                streams.SetVertex(vi + 0, vertex);

                vertex.position.x = xCoordinates.y;
                vertex.texCoord0 = float2(1f, 0f);
                streams.SetVertex(vi + 1, vertex);

                vertex.position.x = xCoordinates.x;
                vertex.position.z = zCoordinates.y;
                vertex.texCoord0 = float2(0f, 1f);
                streams.SetVertex(vi + 2, vertex);

                vertex.position.x = xCoordinates.y;
                vertex.texCoord0 = 1f;
                streams.SetVertex(vi + 3, vertex);

                // 三角形索引：全局索引 = 本格基址 vi + 角点在 0..3 内的偏移。
                // (0,2,1) 与 (1,2,3) 将四边形拆成两个三角形（绕序决定正面朝向，需与材质剔除一致）。
                streams.SetTriangle(ti + 0, vi + int3(0, 2, 1));
                streams.SetTriangle(ti + 1, vi + int3(1, 2, 3));
            }
        }
    }
}
