namespace JNPF.Schedule;

/// <summary>
/// 作业执行记录事件参数
/// </summary>
[SuppressSniffer]
public sealed class JobExecutionRecordEventArgs : EventArgs
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="context">作业执行记录持久上下文</param>
    public JobExecutionRecordEventArgs(PersistenceExecutionRecordContext context)
    {
        Context = context;
    }

    /// <summary>
    /// 作业执行记录持久上下文
    /// </summary>
    public PersistenceExecutionRecordContext Context { get; }
}