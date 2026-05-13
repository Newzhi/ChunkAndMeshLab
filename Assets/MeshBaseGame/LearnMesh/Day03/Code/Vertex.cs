using Unity.Mathematics;

namespace MeshBaseGame.LearnMesh.Day03.Code.ProceduralMeshes
{
    /// <summary>
    /// 逻辑顶点：生成器只依赖此类型，与 GPU 上「单流交错 / 多流拆分」等物理布局解耦。
    /// </summary>
    public struct Vertex
    {
        public float3 position, normal;
        public float4 tangent;
        public float2 texCoord0;
    }
}
