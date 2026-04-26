using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessWorld3D
{
    public class MarchingCubesMesh : Chunk3DReuseManager
    {
        const int threadGroupSize = 8;

        [Header("Marching Cubes")]
        public ComputeShader CSShader;
        public int numPointsPerAxis = 64;
        public float isoLevel = 0;
        public NoiseTerrainGenerator value;

        private ComputeBuffer pointsBuffer;
        private ComputeBuffer triangleBuffer;
        private ComputeBuffer triangleCountBuffer;

        protected override void UpdateChunkMesh(Chunk3D chunk)
        {
            base.UpdateChunkMesh(chunk);
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
            int numVoxelsPerAxis = numPointsPerAxis - 1;
            int numThreadsPerAxis = Mathf.CeilToInt(numVoxelsPerAxis / (float)threadGroupSize);
            // Calculate value
            value.Generate(pointsBuffer, numPointsPerAxis, chunkSize / numVoxelsPerAxis, CenterFromCoord(chunk.coord));
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
            var triangles = new int[numTris * 3];
            for (int i = 0; i < numTris; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    triangles[i * 3 + j] = i * 3 + j;
                    vertices[i * 3 + j] = tris[i][j];
                }
            }

            return new MeshData(vertices, triangles);
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

    public struct MeshData
    {
        public Vector3[] vertices;
        public int[] triangles;
        public Color[] colors;

        public MeshData(Vector3[] vertices, int[] triangles)
        {
            this.vertices = vertices;
            this.triangles = triangles;
            this.colors = null;
        }

        public MeshData(Vector3[] vertices, int[] triangles, Color[] colors)
        {
            this.vertices = vertices;
            this.triangles = triangles;
            this.colors = colors;
        }
    }
}