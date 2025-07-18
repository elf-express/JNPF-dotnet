namespace JNPF.TimeCrontab;

/// <summary>
/// TimeCrontab 模块异常类
/// </summary>
[SuppressSniffer]
public sealed class TimeCrontabException : Exception
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public TimeCrontabException()
        : base()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    public TimeCrontabException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message">异常消息</param>
    /// <param name="innerException">内部异常</param>
    public TimeCrontabException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}