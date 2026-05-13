using MeshBaseGame.LearnMesh.Day03.Code.ProceduralMeshes;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Mathematics.math;

namespace MeshBaseGame.LearnMesh.Day03.Code.ProceduralMeshes.Generators
{
    /// <summary>
    /// 与 <see cref="SquareGrid"/> 同拓扑的平面网格，但相邻四边形共享顶点；UV 在整块网格上连续 0–1。
    /// </summary>
    public struct SharedSquareGrid : IMeshGenerator
    {
        public int Resolution { get; set; }

        public int VertexCount => (Resolution + 1) * (Resolution + 1);

        public int IndexCount => 6 * Resolution * Resolution;

        public int JobLength => Resolution + 1;

        public Bounds Bounds => new Bounds(Vector3.zero, new Vector3(1f, 0f, 1f));

        public void Execute<S>(int z, S streams) where S : struct, IMeshStreams
        {
            int vi = (Resolution + 1) * z;
            int ti = 2 * Resolution * (z - 1);

            var vertex = new Vertex();
            vertex.normal.y = 1f;
            vertex.tangent.xw = float2(1f, -1f);

            vertex.position.x = -0.5f;
            vertex.position.z = (float)z / Resolution - 0.5f;
            vertex.texCoord0.y = (float)z / Resolution;
            streams.SetVertex(vi, vertex);
            vi += 1;

            for (int x = 1; x <= Resolution; x++, vi++, ti += 2)
            {
                vertex.position.x = (float)x / Resolution - 0.5f;
                vertex.texCoord0.x = (float)x / Resolution;
                streams.SetVertex(vi, vertex);

                if (z > 0)
                {
                    streams.SetTriangle(ti + 0, vi + int3(-Resolution - 2, -1, -Resolution - 1));
                    streams.SetTriangle(ti + 1, vi + int3(-Resolution - 1, -1, 0));
                }
            }
        }
    }
}
