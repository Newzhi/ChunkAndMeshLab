using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using MeshBaseGame.LearnMesh.Day02.ProceduralMeshes;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace MeshBaseGame.LearnMesh.Day02.ProceduralMeshes.Streams
{
    /// <summary>
    /// 单流交错顶点：每个顶点一条紧密排列的记录，与常见「interleaved VB」一致。
    /// </summary>
    public struct SingleStream : IMeshStreams
    {
        /// <summary>字段顺序必须与 <see cref="VertexAttributeDescriptor"/> 声明顺序一致。</summary>
        [StructLayout(LayoutKind.Sequential)]
        struct Stream0
        {
            public float3 position, normal;
            public float4 tangent;
            public float2 texCoord0;
        }

        /// <summary>
        /// 顶点视图与索引视图源自同一块 MeshData 底层存储的不同区间；Unity 可能误判别名。
        /// 在确认读写区间不重叠的前提下关闭该检查，否则并行写入易触发安全异常。
        /// </summary>
        [NativeDisableContainerSafetyRestriction]
        NativeArray<Stream0> stream0;

        [NativeDisableContainerSafetyRestriction]
        NativeArray<TriangleUInt16> triangles;

        public void Setup(Mesh.MeshData meshData, Bounds bounds, int vertexCount, int indexCount)
        {
            var descriptor = new NativeArray<VertexAttributeDescriptor>(
                4, Allocator.Temp, NativeArrayOptions.UninitializedMemory
            );
            descriptor[0] = new VertexAttributeDescriptor(dimension: 3);
            descriptor[1] = new VertexAttributeDescriptor(VertexAttribute.Normal, dimension: 3);
            descriptor[2] = new VertexAttributeDescriptor(VertexAttribute.Tangent, dimension: 4);
            descriptor[3] = new VertexAttributeDescriptor(VertexAttribute.TexCoord0, dimension: 2);
            meshData.SetVertexBufferParams(vertexCount, descriptor);
            descriptor.Dispose();

            meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt16);

            meshData.subMeshCount = 1;
            // Job 尚未写入索引时缓冲区无效；跳过校验与自动包围盒，改由 bounds / 生成器约定。
            meshData.SetSubMesh(
                0,
                new SubMeshDescriptor(0, indexCount)
                {
                    bounds = bounds,
                    vertexCount = vertexCount
                },
                MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices
            );

            stream0 = meshData.GetVertexData<Stream0>();
            // 2 = sizeof(ushort)；三个 ushort 视作一个 TriangleUInt16。
            triangles = meshData.GetIndexData<ushort>().Reinterpret<TriangleUInt16>(2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetVertex(int index, Vertex vertex) => stream0[index] = new Stream0
        {
            position = vertex.position,
            normal = vertex.normal,
            tangent = vertex.tangent,
            texCoord0 = vertex.texCoord0
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetTriangle(int index, int3 triangle) => triangles[index] = triangle;
    }
}
