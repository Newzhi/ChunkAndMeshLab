using MeshBaseGame.LearnMesh.Day02.ProceduralMeshes;
using MeshBaseGame.LearnMesh.Day02.ProceduralMeshes.Generators;
using MeshBaseGame.LearnMesh.Day02.ProceduralMeshes.Streams;
using UnityEngine;

/// <summary>
/// 场景侧胶水代码：分配可写 <see cref="Mesh.MeshData"/>、调度 <see cref="MeshJob{G,S}"/>、再应用到 <see cref="Mesh"/>。
/// 不属于核心框架命名空间，便于与其它教程脚本同样在 Inspector 中以简单类名出现。
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralMesh : MonoBehaviour
{
    [SerializeField, Range(1, 10)]
    int resolution = 1;

    Mesh mesh;

    void Awake()
    {
        mesh = new Mesh { name = "Procedural Mesh" };
        GetComponent<MeshFilter>().mesh = mesh;
    }

    /// <summary>Inspector 变更时启用组件，配合 <see cref="Update"/> 触发一次重建。</summary>
    void OnValidate() => enabled = true;

    void Update()
    {
        GenerateMesh();
        enabled = false;
    }

    void GenerateMesh()
    {
        // 申请临时构建缓冲；可与批量网格（meshCount > 1）扩展。
        Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
        Mesh.MeshData meshData = meshDataArray[0];

        MeshJob<SquareGrid, MultiStream>.ScheduleParallel(mesh, meshData, resolution, default)
            .Complete();

        // 将 MeshData 提交到 mesh 并释放 Allocate 的资源。
        Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
    }
}
