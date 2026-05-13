using MeshBaseGame.LearnMesh.Day03.Code.ProceduralMeshes;
using MeshBaseGame.LearnMesh.Day03.Code.ProceduralMeshes.Generators;
using MeshBaseGame.LearnMesh.Day03.Code.ProceduralMeshes.Streams;
using UnityEngine;

namespace MeshBaseGame.LearnMesh.Day03.Code
{
    /// <summary>
    /// Day03：通过 <see cref="MeshType"/> 在独立四边形网格与共享顶点网格之间切换（教程 Modified Grid）。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ProceduralMesh : MonoBehaviour
    {
        static readonly MeshJobScheduleDelegate[] Jobs =
        {
            MeshJob<SquareGrid, SingleStream>.ScheduleParallel,
            MeshJob<SharedSquareGrid, SingleStream>.ScheduleParallel
        };

        public enum MeshType
        {
            SquareGrid,
            SharedSquareGrid
        }

        [SerializeField]
        MeshType meshType;

        [SerializeField, Range(1, 50)]
        int resolution = 1;

        Mesh mesh;

        void Awake()
        {
            mesh = new Mesh { name = "Procedural Mesh" };
            GetComponent<MeshFilter>().mesh = mesh;
        }

        void OnValidate() => enabled = true;

        void Update()
        {
            GenerateMesh();
            enabled = false;
        }

        void GenerateMesh()
        {
            Mesh.MeshDataArray meshDataArray = Mesh.AllocateWritableMeshData(1);
            Mesh.MeshData meshData = meshDataArray[0];

            Jobs[(int)meshType](mesh, meshData, resolution, default).Complete();

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
        }
    }
}
