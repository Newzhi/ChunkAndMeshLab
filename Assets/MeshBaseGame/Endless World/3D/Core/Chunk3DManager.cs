using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EndlessWorld3D
{
    public class Chunk3DManager : MonoBehaviour
    {
        public Transform viewer;
        public float viewDistance = 30;
        public float chunkSize;
        public Material chunkMaterials;

        public bool onlyDebugBounds;
        public bool showBoundsGizmo;
        public Color boundsGizmoColor;

        protected GameObject chunkHolder;
        protected const string chunkHolderName = "Chunks Holder";
        protected List<Chunk3D> chunks;
        protected Dictionary<Vector3Int, Chunk3D> chunkCoords;

        protected virtual void Start()
        {
            chunks = new List<Chunk3D>();
            chunkCoords = new Dictionary<Vector3Int, Chunk3D>();

            InitVisibleChunks();
        }

        protected virtual void Update()
        {
            InitVisibleChunks();
        }

        public void InitVisibleChunks()
        {
            if (chunks == null)
            {
                return;
            }

            CreateChunkHolder();

            Vector3 p = viewer.position;
            Vector3Int viewerCoord = GetChunkCoord(p);
            int maxChunksInView = Mathf.CeilToInt(viewDistance / chunkSize);
            float sqrViewDistance = viewDistance * viewDistance;

            // 更新新出现的chunk
            for (int x = -maxChunksInView; x <= maxChunksInView; x++)
            {
                for (int y = -maxChunksInView; y <= maxChunksInView; y++)
                {
                    for (int z = -maxChunksInView; z <= maxChunksInView; z++)
                    {
                        Vector3Int coord = new Vector3Int(x, y, z) + viewerCoord;

                        if (chunkCoords.ContainsKey(coord))
                        {
                            continue;
                        }

                        Vector3 centre = CenterFromCoord(coord);
                        Vector3 viewerOffset = p - centre;
                        Vector3 o = new Vector3(Mathf.Abs(viewerOffset.x), Mathf.Abs(viewerOffset.y), Mathf.Abs(viewerOffset.z)) - Vector3.one * chunkSize * 0.5f;
                        float sqrDst = new Vector3(Mathf.Max(o.x, 0), Mathf.Max(o.y, 0), Mathf.Max(o.z, 0)).sqrMagnitude;

                        // Chunk is within view distance and should be created (if it doesn't already exist)
                        if (sqrDst <= sqrViewDistance)
                        {
                            Bounds bounds = new Bounds(CenterFromCoord(coord), Vector3.one * chunkSize);
                            if (IsVisibleFrom(bounds, Camera.main))
                            {
                                Chunk3D chunk = CreateChunk(coord);
                                chunk.coord = coord;
                                chunk.SetUp(chunkMaterials);
                                chunkCoords.Add(coord, chunk);
                                chunks.Add(chunk);
                                CreateChunkMesh(chunk);
                            }
                        }
                    }
                }
            }
        }

        public Vector3Int GetChunkCoord(Vector3 pos)
        {
            Vector3 chunkCoord = pos / chunkSize;
            return new Vector3Int(Mathf.RoundToInt(chunkCoord.x), Mathf.RoundToInt(chunkCoord.y), Mathf.RoundToInt(chunkCoord.z));
        }

        protected void CreateChunkHolder()
        {
            // Create/find mesh holder object for organizing chunks under in the hierarchy
            if (chunkHolder == null)
            {
                if (GameObject.Find(chunkHolderName))
                {
                    chunkHolder = GameObject.Find(chunkHolderName);
                }
                else
                {
                    chunkHolder = new GameObject(chunkHolderName);
                }
            }
        }

        protected Vector3 CenterFromCoord(Vector3Int coord)
        {
            return new Vector3(coord.x, coord.y, coord.z) * chunkSize;
        }

        protected bool IsVisibleFrom(Bounds bounds, Camera camera)
        {
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }

        protected Chunk3D CreateChunk(Vector3Int coord)
        {
            GameObject chunk = new GameObject($"Chunk ({coord.x}, {coord.y}, {coord.z})");
            chunk.transform.parent = chunkHolder.transform;
            Chunk3D newChunk = chunk.AddComponent<Chunk3D>();
            newChunk.coord = coord;
            return newChunk;
        }

        protected virtual void CreateChunkMesh(Chunk3D chunk)
        {

        }

        void OnDrawGizmos()
        {
            if (showBoundsGizmo)
            {
                Gizmos.color = boundsGizmoColor;
                if (chunks != null)
                {
                    foreach (var chunk in chunks)
                    {
                        Gizmos.DrawWireCube(CenterFromCoord(chunk.coord), Vector3.one * chunkSize);
                    }
                }
            }
        }
    }
}
