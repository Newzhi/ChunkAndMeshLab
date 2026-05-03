using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace MeshBaseGame.LearnMesh.Day02.ProceduralMeshes.Streams
{
    /// <summary>
    /// 与 <see cref="UnityEngine.Rendering.IndexFormat.UInt16"/> 索引缓冲中「三个 ushort 构成一个三角形」的内存布局一致；
    /// 用于 <see cref="Unity.Collections.NativeArray{T}.Reinterpret{T}"/> 按三角形批量写入。
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TriangleUInt16
    {
        public ushort a, b, c;

        public static implicit operator TriangleUInt16(int3 t) => new TriangleUInt16
        {
            a = (ushort)t.x,
            b = (ushort)t.y,
            c = (ushort)t.z
        };
    }
}
