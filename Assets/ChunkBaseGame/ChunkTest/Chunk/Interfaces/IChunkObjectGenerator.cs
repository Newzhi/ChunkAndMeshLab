// 区块内容生成策略契约：与 Day02 程序化网格思路类似，用窄接口拆分职责；
// Manager 只做区块窗口调度与缓存，具体「读档 / 生成 / 写盘 / 场景实例化 / 卸载回写」由实现类完成。

/// <summary>
/// 区块内容生成与场景化策略（可替换为体素+Mesh、纯数据等）。
/// 职责边界：在 <see cref="ChunkData"/> 上完成本策略所需的全部内容侧工作；
/// 持久化通过注入的 <see cref="IChunkObjectStorager"/> 完成，Manager 不介入细节。
/// </summary>
public interface IChunkObjectGenerator
{
    /// <summary>加载：读档、必要时生成并写盘、创建/填充场景表现（由实现定义）。</summary>
    void LoadContent(ChunkData chunk, ChunkSettings settings, IChunkObjectStorager storager);

    /// <summary>卸载：回写运行时状态、按需持久化、释放场景对象（由实现定义）。</summary>
    void UnloadContent(ChunkData chunk, ChunkSettings settings, IChunkObjectStorager storager);
}
