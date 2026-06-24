namespace Bi.Application.Services;

/// <summary>
/// 向量嵌入服务接口
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// 生成单个文本的向量嵌入
    /// </summary>
    /// <param name="text">输入文本</param>
    /// <returns>向量数组</returns>
    Task<float[]> GetEmbeddingAsync(string text);
    
    /// <summary>
    /// 批量生成文本的向量嵌入
    /// </summary>
    /// <param name="texts">输入文本列表</param>
    /// <returns>向量数组列表</returns>
    Task<List<float[]>> GetEmbeddingsAsync(List<string> texts);
    
    /// <summary>
    /// 获取向量维度
    /// </summary>
    int Dimensions { get; }
}

