namespace JNPF.UnifyResult.Internal;

/// <summary>
/// flowable结果集
/// </summary>
/// <typeparam name="T"></typeparam>
[SuppressSniffer]
public class FlowableResult<T>
{
    /// <summary>
    /// 状态码
    /// </summary>
    public string? code { get; set; }

    /// <summary>
    /// 信息
    /// </summary>
    public object msg { get; set; }

    /// <summary>
    /// 数据
    /// </summary>
    public T data { get; set; }

    /// <summary>
    /// 结果
    /// </summary>
    public bool success { get; set; }
}
