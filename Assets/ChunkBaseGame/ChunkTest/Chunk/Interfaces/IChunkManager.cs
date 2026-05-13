using System.Collections.Generic;

/// <summary>
/// 区块缓存与 Load/Unload 的对外契约；内容读档/生成/实例化由 <see cref="IChunkObjectGenerator"/> 在实现内部完成，不暴露为接口成员。
/// </summary>
public interface IChunkManager
{
    IReadOnlyDictionary<long, ChunkData> Chunks { get; }

    ChunkData LoadChunk(ChunkCoord coord);

    bool UnloadChunk(ChunkCoord coord);

    bool TryGetChunk(long chunkId, out ChunkData chunk);
}
