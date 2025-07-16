namespace JNPF.Logging;

/// <summary>
/// 日志上下文
/// </summary>
[SuppressSniffer]
public sealed class LogContext : IDisposable
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public LogContext()
    {
    }

    /// <summary>
    /// 日志上下文数据
    /// </summary>
    public IDictionary<object, object> Properties { get; set; }

    /// <summary>
    /// 原生日志上下文数据
    /// </summary>
    public IList<object> Scopes { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        Properties?.Clear();
        Scopes?.Clear();
        Properties = null;
        Scopes = null;
    }
}