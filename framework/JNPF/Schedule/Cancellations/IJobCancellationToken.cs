﻿namespace JNPF.Schedule;

/// <summary>
/// 取消作业执行 Token 器
/// </summary>
public interface IJobCancellationToken
{
    /// <summary>
    /// 获取或创建取消作业执行 Token
    /// </summary>
    /// <param name="jobId">作业 Id</param>
    /// <param name="runId">作业触发器触发的唯一标识</param>
    /// <param name="stoppingToken">后台主机服务停止时取消任务 Token</param>
    /// <returns><see cref="CancellationToken"/></returns>
    CancellationTokenSource GetOrCreate(string jobId, string runId, CancellationToken stoppingToken);

    /// <summary>
    /// 取消（完成）正在执行的执行
    /// </summary>
    /// <param name="jobId">作业 Id</param>
    /// <param name="triggerId">作业触发器 Id</param>
    /// <param name="outputLog">是否显示日志</param>
    void Cancel(string jobId, string triggerId = null, bool outputLog = true);
}