using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessWorld3D
{
    public class NoiseTerrainGenerator : MonoBehaviour
    {
        const int threadGroupSize = 8;

        public ComputeShader valueShader;

        [Header("Noise Settings")]
        public float scale = 1;
        public Vector3 offset = new Vector3(100, 100, 100);
        [Min(1)] public int octaves = 4;
        [Min(0)] public float roughness = 3;
        [Min(0)] public float persistance = 0.4f;
        public float heightScale;

        public ComputeBuffer Generate(ComputeBuffer pointsBuffer, int numPointsPerAxis, float cubeSize, Vector3 worldCenter)
        {
            valueShader.SetVector("scaleAndOffset", new Vector4(scale, offset.x, offset.y, offset.z));
            valueShader.SetInt("octaves", octaves);
            valueShader.SetFloat("roughness", roughness);
            valueShader.SetFloat("persistance", persistance);
            valueShader.SetFloat("heightScale", heightScale);

            int numPoints = numPointsPerAxis * numPointsPerAxis * numPointsPerAxis;
            int numThreadsPerAxis = Mathf.CeilToInt(numPointsPerAxis / (float)threadGroupSize);
            // Points buffer is populated inside shader with pos (xyz) + density (w).
            // Set paramaters
            valueShader.SetBuffer(0, "points", pointsBuffer);
            valueShader.SetInt("numPointsPerAxis", numPointsPerAxis);
            valueShader.SetVector("center", worldCenter);
            valueShader.SetFloat("cubeSize", cubeSize);

            // Dispatch shader
            valueShader.Dispatch(0, numThreadsPerAxis, numThreadsPerAxis, numThreadsPerAxis);

            // Return voxel data buffer so it can be used to generate mesh
            return pointsBuffer;
        }
    }
}