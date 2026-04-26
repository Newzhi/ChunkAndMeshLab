using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class ShowChunkDebugger2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ChunkManager2D chunkManager;
    [SerializeField] private Transform player;
    [SerializeField] private Text textLegacy;

    [Header("Chunk Format")]
    [SerializeField] private int chunkSizeFallback = 16;

    [Header("Perf")]
    [SerializeField] private float fpsSmoothing = 0.1f;
    [SerializeField] private bool showCpuUsage = true;

    private float smoothedDeltaTime;
    private Process process;
    private long lastCpuTicks;
    private float lastCpuSampleTime;
    private float lastCpuPercent;

    private void Awake()
    {
        if (chunkManager == null)
        {
            chunkManager = FindFirstObjectByType<ChunkManager2D>();
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        smoothedDeltaTime = Time.unscaledDeltaTime;

        if (showCpuUsage)
        {
            try
            {
                process = Process.GetCurrentProcess();
                lastCpuTicks = process.TotalProcessorTime.Ticks;
                lastCpuSampleTime = Time.unscaledTime;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ShowChunkDebugger2D] CPU usage unavailable: {ex.Message}");
                showCpuUsage = false;
            }
        }
    }

    private void Update()
    {
        if (textLegacy == null)
        {
            return;
        }

        smoothedDeltaTime = Mathf.Lerp(smoothedDeltaTime, Time.unscaledDeltaTime, fpsSmoothing);
        float fps = smoothedDeltaTime > 0.000001f ? 1f / smoothedDeltaTime : 0f;
        float frameMs = smoothedDeltaTime * 1000f;

        ChunkCoord2D center = default;
        if (player != null)
        {
            Vector2 pos2 = new Vector2(player.position.x, player.position.y);
            center = ChunkUtil2D.WorldToChunkCoord(pos2, chunkSizeFallback);
        }

        int loaded = chunkManager != null ? chunkManager.Chunks.Count : 0;
        int active = 0;
        if (chunkManager != null)
        {
            foreach (var kv in chunkManager.Chunks)
            {
                if (kv.Value != null && kv.Value.State == ChunkState2D.Active)
                {
                    active++;
                }
            }
        }

        string cpuLine = string.Empty;
        if (showCpuUsage && process != null)
        {
            float now = Time.unscaledTime;
            float dt = now - lastCpuSampleTime;
            if (dt > 0.25f)
            {
                long cpuTicks = process.TotalProcessorTime.Ticks;
                long deltaCpuTicks = cpuTicks - lastCpuTicks;
                lastCpuTicks = cpuTicks;
                lastCpuSampleTime = now;

                // 说明：Ticks=100ns。CPU% ≈ cpu_time / (wall_time * cores) * 100
                double cpuSeconds = deltaCpuTicks / 10_000_000.0;
                double wallSeconds = dt;
                double cores = System.Environment.ProcessorCount;
                lastCpuPercent = wallSeconds > 0 ? (float)((cpuSeconds / (wallSeconds * cores)) * 100.0) : 0f;
            }

            cpuLine = $"CPU: {lastCpuPercent:0.0}%";
        }

        textLegacy.text =
            $"Chunks: {active} active / {loaded} loaded\n" +
            $"Center: ({center.X}, {center.Y})\n" +
            $"FPS: {fps:0.0} ({frameMs:0.0} ms)\n" +
            (string.IsNullOrEmpty(cpuLine) ? "" : cpuLine);
    }
}

