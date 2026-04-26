using UnityEngine;
using UnityEngine.UI;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class ShowChunkDebugger : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private ChunkManager chunkManager;
    [SerializeField] private Transform player;
    [SerializeField] private Text textLegacy;

    [Header("Format")]
    [SerializeField] private int chunkSizeFallback = 16;

    [Header("Perf")]
    [SerializeField] private float fpsSmoothing = 0.1f;
    [SerializeField] private bool showCpuUsage = true;

    private float smoothedDeltaTime;
    private Process process;
    private long lastCpuTicks;
    private float lastCpuSampleTime;

    private void Awake()
    {
        if (chunkManager == null)
        {
            chunkManager = FindFirstObjectByType<ChunkManager>();
        }

        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) player = playerObj.transform;
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
                Debug.LogWarning($"[ShowChunkDebugger] CPU usage unavailable: {ex.Message}");
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

        if (chunkManager == null)
        {
            textLegacy.text = $"ChunkManager: <missing>\nFPS: {fps:0.0} ({frameMs:0.0} ms)";
            return;
        }

        ChunkCoord center = default;
        if (player != null)
        {
            center = ChunkUtil.WorldToChunkCoord(player.position, chunkSizeFallback);
        }

        int loaded = chunkManager.Chunks.Count;
        int active = 0;
        foreach (var kv in chunkManager.Chunks)
        {
            if (kv.Value != null && kv.Value.State == ChunkState.Active)
            {
                active++;
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

                // CPU% ≈ (cpu_time / (wall_time * cores)) * 100
                double cpuSeconds = deltaCpuTicks / (double)Stopwatch.Frequency;
                double wallSeconds = dt;
                double cores = System.Environment.ProcessorCount;
                double cpuPercent = wallSeconds > 0 ? (cpuSeconds / (wallSeconds * cores)) * 100.0 : 0.0;
                cpuLine = $"CPU: {cpuPercent:0.0}%";
            }
        }

        textLegacy.text =
            $"Chunks: {active} active / {loaded} loaded\n" +
            $"Center: ({center.X}, {center.Z})\n" +
            $"FPS: {fps:0.0} ({frameMs:0.0} ms)\n" +
            (string.IsNullOrEmpty(cpuLine) ? "" : cpuLine);
    }
}
