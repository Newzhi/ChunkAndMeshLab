using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessWorld3D
{
    public class EditableGenerator : MonoBehaviour
    {
        const int threadGroupSize = 8;

        public ComputeShader valueShader;

        public float penSize = 1f;

        public ComputeBuffer Generate(ComputeBuffer pointsBuffer, int numPointsPerAxis, float cubeSize, Vector3 worldCenter, RaycastHit hit, float isoLevel)
        {
            valueShader.SetVector("penPosAndSize", new Vector4(hit.point.x, hit.point.y, hit.point.z, penSize));
            valueShader.SetVector("penNormal", hit.normal);
            valueShader.SetFloat("isoLevel", isoLevel);

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
