namespace Bi.Api.Models;

/// <summary>
/// 统一API响应模型
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class ApiResponse<T>
{
    /// <summary>
    /// 响应码：0表示成功，非0表示失败
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// 响应消息
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 响应数据
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static ApiResponse<T> Success(T? data, string message = "操作成功")
    {
        return new ApiResponse<T>
        {
            Code = 0,
            Message = message,
            Data = data
        };
    }

    /// <summary>
    /// 创建失败响应
    /// </summary>
    public static ApiResponse<T> Fail(string message, int code = -1)
    {
        return new ApiResponse<T>
        {
            Code = code,
            Message = message,
            Data = default
        };
    }
}

/// <summary>
/// 无数据的统一响应
/// </summary>
public class ApiResponse : ApiResponse<object>
{
    /// <summary>
    /// 创建成功响应
    /// </summary>
    public static ApiResponse Success(string message = "操作成功")
    {
        return new ApiResponse
        {
            Code = 0,
            Message = message,
            Data = null
        };
    }

    /// <summary>
    /// 创建失败响应
    /// </summary>
    public new static ApiResponse Fail(string message, int code = -1)
    {
        return new ApiResponse
        {
            Code = code,
            Message = message,
            Data = null
        };
    }
}

/// <summary>
/// 分页响应模型
/// </summary>
/// <typeparam name="T">数据类型</typeparam>
public class PagedResponse<T>
{
    /// <summary>
    /// 数据列表
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// 总记录数
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// 当前页码
    /// </summary>
    public int PageIndex { get; set; }

    /// <summary>
    /// 每页大小
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// 总页数
    /// </summary>
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)Total / PageSize) : 0;
}

