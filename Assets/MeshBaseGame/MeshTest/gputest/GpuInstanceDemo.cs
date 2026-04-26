using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 最小 GPU Instancing：同一网格 + 同一材质，一次 Draw 调用画很多个「副本」，由 GPU 按矩阵数组展开。
/// （与「每个立方体一个 GameObject」相比：CPU 不创建海量 Transform，适合大量重复物体。）
/// </summary>
[DisallowMultipleComponent]
public sealed class GpuInstanceDemo : MonoBehaviour
{
    // 要画的「原型」网格。留空则在运行时取内置 Cube 的共享网格（演示省事；正式项目建议显式拖引用，避免 CreatePrimitive）。
    [SerializeField] private Mesh instanceMesh;

    // 必须支持 GPU Instancing（材质 Inspector 勾选 Enable GPU Instancing，或用本文件夹的 GpuInstanceDemo.mat）。
    // DrawMeshInstanced 走的是「实例化绘制」路径，材质不支持时会退化成低效路径或显示异常。
    [SerializeField] private Material instanceMaterial;

    [Header("网格")]
    [Min(1)] public int gridSize = 24;
    [Min(0.01f)] public float spacing = 1f;
    [Min(0.01f)] public float cubeScale = 0.45f;

    // Unity 的 Graphics.DrawMeshInstanced 单次最多接收 1023 个矩阵（API 硬限制），多出来的必须拆成多批。
    private const int MaxPerBatch = 1023;

    // 复用固定长度数组，避免每帧 new，减少 GC（分批时反复填入前 n 个元素即可）。
    private static readonly Matrix4x4[] Batch = new Matrix4x4[MaxPerBatch];

    // 所有实例的世界变换矩阵列表；GPU Instancing 本质就是「很多个 TRS 矩阵」共享同一个 Mesh/Material。
    private readonly List<Matrix4x4> matrices = new List<Matrix4x4>();

    // 若在运行时克隆了材质（只为打开 enableInstancing），需要在 OnDestroy 里 Destroy，否则会泄漏。
    private Material materialOwnedByScript;

    private void Awake()
    {
        // ---------- 准备 Mesh ----------
        if (instanceMesh == null)
        {
            // 临时建一个 Cube，只借用它的 sharedMesh（是 Unity 内置资源，不要 Destroy 网格本身）。
            // 立刻 Destroy 临时物体，否则场景里会多一个看不见的 Cube。
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instanceMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
        }

        // ---------- 准备 Material ----------
        if (instanceMaterial == null)
        {
            Debug.LogError("[GpuInstanceDemo] 请在 Inspector 指定 Instance Material（例如 GpuInstanceDemo.mat）。");
            enabled = false;
            return;
        }

        if (!instanceMaterial.enableInstancing)
        {
            // 不能改 Project 里的资源材质，克隆一份再开 Instancing，避免改坏资源文件。
            materialOwnedByScript = new Material(instanceMaterial) { enableInstancing = true };
            instanceMaterial = materialOwnedByScript;
        }

        // 矩阵只与 grid 参数有关，启动时算一遍即可（本示例不在运行时改 transform）。
        BuildMatrices();
    }

    private void OnDestroy()
    {
        if (materialOwnedByScript != null)
        {
            Destroy(materialOwnedByScript);
            materialOwnedByScript = null;
        }
    }

    private void BuildMatrices()
    {
        matrices.Clear();

        // 让整个方阵以世界原点为中心铺开，方便相机对准 (0,0,0) 就能看到全貌。
        float half = (gridSize - 1) * spacing * 0.5f;

        for (int z = 0; z < gridSize; z++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                Vector3 pos = new Vector3(x * spacing - half, 0f, z * spacing - half);

                // TRS = 位置、旋转、缩放；每个实例一个 4×4 矩阵，GPU 用它在顶点阶段把「一个 Cube」变到不同位置。
                matrices.Add(Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * cubeScale));
            }
        }
    }

    private void LateUpdate()
    {
        // 放在 LateUpdate：若将来有相机跟物体，先让别的脚本改完相机再画，减少一帧错位（本示例不强制，只是常见习惯）。
        if (instanceMesh == null || instanceMaterial == null)
        {
            return;
        }

        int total = matrices.Count;
        int offset = 0;

        // 超过 1023 个实例必须循环多调几次 DrawMeshInstanced，每次最多 1023 个。
        while (offset < total)
        {
            int n = Mathf.Min(MaxPerBatch, total - offset);

            // 把这一批矩阵拷进 Batch 的前 n 个槽位（API 要求传数组 + 有效数量 n）。
            for (int i = 0; i < n; i++)
            {
                Batch[i] = matrices[offset + i];
            }

            // 核心：一次提交 n 个实例；submesh 0；不传 MaterialPropertyBlock 时各实例外观一致。
            // 最后一个 camera 传 null 表示用当前渲染流程下的默认行为（Game 视图可见）。
            Graphics.DrawMeshInstanced(
                instanceMesh,
                0,
                instanceMaterial,
                Batch,
                n,
                properties: null,
                ShadowCastingMode.On,
                receiveShadows: true,
                layer: gameObject.layer,
                camera: null);

            offset += n;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 仅在编辑器、且已在 Play 模式下：改 Inspector 数字时立刻重建矩阵，不用重启 Play。
        if (Application.isPlaying && matrices != null && instanceMesh != null && instanceMaterial != null)
        {
            BuildMatrices();
        }
    }
#endif
}
