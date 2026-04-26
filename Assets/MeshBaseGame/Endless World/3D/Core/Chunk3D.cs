using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessWorld3D
{
    public class Chunk3D : MonoBehaviour
    {
        public Vector3Int coord;

        Mesh mesh;
        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        MeshCollider meshCollider;

        public void DestroyOrDisable()
        {
            if (Application.isPlaying)
            {
                mesh.Clear();
                gameObject.SetActive(false);
            }
            else
            {
                DestroyImmediate(gameObject, false);
            }
        }

        // Add components/get references in case lost (references can be lost when working in the editor)
        public void SetUp(Material mat)
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshCollider = GetComponent<MeshCollider>();

            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (meshCollider == null)
            {
                meshCollider = gameObject.AddComponent<MeshCollider>();
            }

            mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                meshFilter.sharedMesh = mesh;
            }

            if (meshCollider.sharedMesh == null)
            {
                meshCollider.sharedMesh = mesh;
            }
            // force update
            meshCollider.enabled = false;
            meshCollider.enabled = true;

            meshRenderer.material = mat;
        }

        public void UpdateMesh(MeshData meshData) 
        {
            this.mesh.Clear();
            this.mesh.vertices = meshData.vertices;
            this.mesh.triangles = meshData.triangles;
            this.mesh.colors = meshData.colors;
            this.mesh.RecalculateNormals();
            meshCollider.enabled = false;
            meshCollider.enabled = true;
        }

        public void SetCollider(bool active) 
        {
            meshCollider.enabled = active;
        }
    }
}