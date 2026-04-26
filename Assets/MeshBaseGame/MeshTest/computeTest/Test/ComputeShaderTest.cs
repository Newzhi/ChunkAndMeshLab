using UnityEngine;

public class ComputeBufferTest : MonoBehaviour
{
    public ComputeShader computeShader; // 在 Inspector 中关联上面的 .compute 文件
    private ComputeBuffer inputBuffer;  // 输入缓冲区
    private ComputeBuffer outputBuffer; // 输出缓冲区

    void Start()
    {
        // 1. 准备数据
        int count = 1024;
        float[] inputData = new float[count];
        for (int i = 0; i < count; i++)
        {
            inputData[i] = i; // 填充数据 0, 1, 2...
        }

        // 2. 创建缓冲区
        // 参数1：元素数量
        // 参数2：单个元素的字节大小 (float 是 4 字节)
        inputBuffer = new ComputeBuffer(count, sizeof(float));
        outputBuffer = new ComputeBuffer(count, sizeof(float));

        // 3. 将数据从 CPU 拷贝到 GPU (输入缓冲区)
        inputBuffer.SetData(inputData);

        // 4. 设置 Shader 参数
        int kernelHandle = computeShader.FindKernel("CSMain");
        
        // 将缓冲区绑定到 Shader 中的变量名
        computeShader.SetBuffer(kernelHandle, "InputBuffer", inputBuffer);
        computeShader.SetBuffer(kernelHandle, "OutputBuffer", outputBuffer);
        
        // 传递数组长度
        computeShader.SetInt("Count", count);

        // 5. 调度执行 (Dispatch)
        // 计算需要多少个线程组：总数 / 每组线程数 (向上取整)
        int threadGroups = Mathf.CeilToInt(count / 64.0f);
        computeShader.Dispatch(kernelHandle, threadGroups, 1, 1);

        // 6. 读取结果 (从 GPU 读回 CPU)
        float[] outputData = new float[count];
        outputBuffer.GetData(outputData);

        // 验证结果
        Debug.Log($"输入: {inputData[0]}, {inputData[1]}, {inputData[2]}");
        Debug.Log($"输出: {outputData[0]}, {outputData[1]}, {outputData[2]}");

        // 7. 释放资源 (非常重要！否则会导致内存泄漏)
        inputBuffer.Release();
        outputBuffer.Release();
    }
}