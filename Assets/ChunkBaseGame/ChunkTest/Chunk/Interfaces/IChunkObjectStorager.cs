// 区块对象持久化策略契约：路径格式、序列化格式、同步/异步写回等由实现决定；
// 由 <see cref="IChunkObjectGenerator"/> 编排何时调用，Manager 不直接调用本接口。

/// <summary>
/// 区块对象存档数据的持久化策略。
/// 职责边界：只处理 <see cref="ChunkObjectSaveData"/> 与配置的 IO；
/// 不参与区块窗口与内容生成逻辑（由 <see cref="IChunkObjectGenerator"/> 编排何时调用本接口）。
/// </summary>
public interface IChunkObjectStorager
{
    /// <summary>尝试读取已存在的区块对象存档；未命中或禁用时返回 false。</summary>
    bool TryLoad(long chunkId, ChunkSettings settings, out ChunkObjectSaveData data);

    /// <summary>同步写入（阻塞当前线程）。</summary>
    bool Save(ChunkObjectSaveData data, ChunkSettings settings);

    /// <summary>异步/队列写入；实现可合并同一 chunk 的多次保存。</summary>
    void SaveAsync(ChunkObjectSaveData data, ChunkSettings settings);
}
