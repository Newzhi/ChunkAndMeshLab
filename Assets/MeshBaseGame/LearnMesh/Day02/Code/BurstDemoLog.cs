using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// 挂到物体上，Play 后在 Console 看：同样算法，无 Burst vs 有 Burst 谁更快。
/// </summary>
public class BurstDemoLog : MonoBehaviour
{
    private const int Count = 200_000;
    private const int Batch = 64;
    /// <summary>同一种 Job 多跑几次再算总耗时，减少偶然误差。</summary>
    private const int Times = 20;

    private void Start()
    {
        var inputs = new NativeArray<float3>(Count, Allocator.TempJob);
        var outputs = new NativeArray<float>(Count, Allocator.TempJob);
        for (int i = 0; i < Count; i++)
            inputs[i] = new float3(i, 1f, 2f);

        // 先跑一次 Burst，避免「第一次编译」算进下面的计时。
        new BurstSquareSumJob { Inputs = inputs, Outputs = outputs }.Schedule(Count, Batch).Complete();

        double t0 = Time.realtimeSinceStartupAsDouble;
        for (int k = 0; k < Times; k++)
            new PlainSquareSumJob { Inputs = inputs, Outputs = outputs }.Schedule(Count, Batch).Complete();
        double t1 = Time.realtimeSinceStartupAsDouble;

        for (int k = 0; k < Times; k++)
            new BurstSquareSumJob { Inputs = inputs, Outputs = outputs }.Schedule(Count, Batch).Complete();
        double t2 = Time.realtimeSinceStartupAsDouble;

        float mid = outputs[Count / 2];
        inputs.Dispose();
        outputs.Dispose();

        float msPlain = (float)((t1 - t0) * 1000.0);
        float msBurst = (float)((t2 - t1) * 1000.0);
        Debug.Log(
            $"[BurstDemo] 长度={Count}，各跑 {Times} 次合计 — " +
            $"无 Burst: {msPlain:F1} ms | 有 Burst: {msBurst:F1} ms | 校验 outputs[mid]={mid:F2}");
    }

    private struct PlainSquareSumJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Inputs;
        public NativeArray<float> Outputs;

        public void Execute(int i)
        {
            float3 v = Inputs[i];
            Outputs[i] = v.x * v.x + v.y * v.y + v.z * v.z;
        }
    }

    [BurstCompile]
    private struct BurstSquareSumJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> Inputs;
        public NativeArray<float> Outputs;

        public void Execute(int i)
        {
            float3 v = Inputs[i];
            Outputs[i] = v.x * v.x + v.y * v.y + v.z * v.z;
        }
    }
}
