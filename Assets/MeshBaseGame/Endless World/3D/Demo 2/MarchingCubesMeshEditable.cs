using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessWorld3D
{
    public class MarchingCubesMeshEditable : Chunk3DManager
    {
        public Camera cam;

        const int threadGroupSize = 8;

        [Header("Marching Cubes")]
        public ComputeShader CSShader;
        public int numPointsPerAxis = 64;
        public float isoLevel = 0;
        public NoiseTerrainGenerator value;

        [Header("Edit")]
        public float editableMaxDistance = 5;
        public LayerMask editableLayer;
        public EditableGenerator editable;
        public ParticleSystem editEffect;

        private ComputeBuffer pointsBuffer;
        private ComputeBuffer triangleBuffer;
        private ComputeBuffer triangleCountBuffer;

        private int numVoxelsPerAxis;
        private int numThreadsPerAxis;
        private int pointCount;
        private Dictionary<Vector3Int, Vector4[]> chunkDatas;
        
        protected override void Start()
        {
            chunkDatas = new Dictionary<Vector3Int, Vector4[]>();
            numVoxelsPerAxis = numPointsPerAxis - 1;
            numThreadsPerAxis = Mathf.CeilToInt(numVoxelsPerAxis / (float)threadGroupSize);
            pointCount = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
            base.Start();
        }

        protected override void Update()
        {
            base.Update();

            if (Input.GetMouseButtonDown(0)) 
            {
                Ray ray = cam.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, editableMaxDistance, editableLayer)) 
                {
                    Chunk3D chunk = hit.transform.GetComponent<Chunk3D>();
                    if (chunk != null) 
                    {
                        if (editEffect != null)
                        {
                            Instantiate(editEffect, hit.point, Quaternion.identity);
                        }
                        Collider[] colliders = Physics.OverlapSphere(hit.point, editable.penSize * 2, editableLayer);
                        for (int i = 0; i < colliders.Length; i++) 
                        {
                            chunk = colliders[i].GetComponent<Chunk3D>();
                            chunk.UpdateMesh(EditMesh(chunk, hit));
                        }
                    }
                }
            }
        }

        protected override void CreateChunkMesh(Chunk3D chunk)
        {
            base.CreateChunkMesh(chunk);
            CreateBuffers();
            chunk.UpdateMesh(CreateMesh(chunk));
        }

        private void CreateBuffers()
        {
            int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
            int numVoxelsPerAxis = numPointsPerAxis - 1;
            int numVoxels = numVoxelsPerAxis * numVoxelsPerAxis * numVoxelsPerAxis;
            int maxTriangleCount = numVoxels * 5;

            if (pointsBuffer == null || numPoints != pointsBuffer.count)
            {
                ReleaseBuffers();
                triangleBuffer = new ComputeBuffer(maxTriangleCount, sizeof(float) * 3 * 3, ComputeBufferType.Append);
                pointsBuffer = new ComputeBuffer(numPoints, sizeof(float) * 4);
                triangleCountBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            }
        }

        private void ReleaseBuffers()
        {
            if (triangleBuffer != null)
            {
                triangleBuffer.Release();
                pointsBuffer.Release();
                triangleCountBuffer.Release();
            }
        }

        private MeshData CreateMesh(Chunk3D chunk)
        {
            // Calculate value (Get + Save)
            pointsBuffer = value.Generate(pointsBuffer, numPointsPerAxis, chunkSize / numVoxelsPerAxis, CenterFromCoord(chunk.coord));         
            Vector4[] posAndValue = new Vector4[pointCount];
            pointsBuffer.GetData(posAndValue, 0, 0, pointCount);
            chunkDatas.Add(chunk.coord, posAndValue);

            return MarchingCubes();
        }

        private MeshData EditMesh(Chunk3D chunk, RaycastHit hit)
        {
            // Calculate value (Get + Save)
            pointsBuffer.SetData(chunkDatas[chunk.coord], 0, 0, pointCount);
            pointsBuffer = editable.Generate(pointsBuffer, numPointsPerAxis, chunkSize / numVoxelsPerAxis, CenterFromCoord(chunk.coord), hit, isoLevel);
            Vector4[] posAndValue = new Vector4[pointCount];
            pointsBuffer.GetData(posAndValue, 0, 0, pointCount);
            chunkDatas[chunk.coord] = posAndValue;

            return MarchingCubes();
        }

        private MeshData MarchingCubes() 
        {
            // Marching Cubes 
            triangleBuffer.SetCounterValue(0);
            CSShader.SetBuffer(0, "points", pointsBuffer);
            CSShader.SetBuffer(0, "triangles", triangleBuffer);
            CSShader.SetInt("numPointsPerAxis", numPointsPerAxis);
            CSShader.SetFloat("isoLevel", isoLevel);
            CSShader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);

            // Get number of triangles in the triangle buffer
            ComputeBuffer.CopyCount(triangleBuffer, triangleCountBuffer, 0);
            int[] triangleCountArray = { 0 };
            triangleCountBuffer.GetData(triangleCountArray);
            int numTris = triangleCountArray[0];
            // Get triangle data from shader
            Triangle[] tris = new Triangle[numTris];
            triangleBuffer.GetData(tris, 0, 0, numTris);
            // Construct Mesh
            var vertices = new Vector3[numTris * 3];
            var colors = new Color[numTris * 3];
            var triangles = new int[numTris * 3];
            for (int i = 0; i < numTris; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    triangles[i * 3 + j] = i * 3 + j;
                    vertices[i * 3 + j] = tris[i][j];
                    colors[i * 3 + j] = new Color(Random.Range(0.1f, 0.9f), Random.Range(0.1f, 0.9f), Random.Range(0.1f, 0.9f), 1);
                }
            }

            return new MeshData(vertices, triangles, colors);
        }

        struct Triangle
        {
            #pragma warning disable 649 // disable unassigned variable warning
            public Vector3 a;
            public Vector3 b;
            public Vector3 c;

            public Vector3 this[int i]
            {
                get
                {
                    switch (i)
                    {
                        case 0:
                            return a;
                        case 1:
                            return b;
                        default:
                            return c;
                    }
                }
            }
        }
    }
}
