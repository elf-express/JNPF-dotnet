﻿using JNPF.Schedule;
using SqlSugar;

namespace JNPF.TaskScheduler.Entitys;

[SugarTable("JobTriggers", "作业触发器表")]
[Tenant("JNPF-Job")]
public class JobTriggers
{
    /// <summary>
    /// Id.
    /// </summary>
    [SugarColumn(ColumnDescription = "Id", IsPrimaryKey = true, IsIdentity = true)]
    public virtual long Id { get; set; }

    /// <summary>
    /// 触发器 Id.
    /// </summary>
    [SugarColumn(ColumnDescription = "触发器Id")]
    public virtual string TriggerId { get; set; }

    /// <summary>
    /// 作业 Id.
    /// </summary>
    [SugarColumn(ColumnDescription = "作业Id")]
    public virtual string JobId { get; set; }

    /// <summary>
    /// 触发器类型 FullName.
    /// </summary>
    [SugarColumn(ColumnDescription = "触发器类型")]
    public string? TriggerType { get; set; }

    /// <summary>
    /// 程序集 Name.
    /// </summary>
    [SugarColumn(ColumnDescription = "程序集")]
    public string? AssemblyName { get; set; }

    /// <summary>
    /// 参数.
    /// </summary>
    [SugarColumn(ColumnDescription = "参数")]
    public string? Args { get; set; }

    /// <summary>
    /// 描述信息.
    /// </summary>
    [SugarColumn(ColumnDescription = "描述信息")]
    public string? Description { get; set; }

    /// <summary>
    /// 状态.
    /// </summary>
    [SugarColumn(ColumnDescription = "状态")]
    public TriggerStatus Status { get; set; } = TriggerStatus.Ready;

    /// <summary>
    /// 起始时间.
    /// </summary>
    [SugarColumn(ColumnDescription = "起始时间")]
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// 结束时间.
    /// </summary>
    [SugarColumn(ColumnDescription = "结束时间")]
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// 最近运行时间.
    /// </summary>
    [SugarColumn(ColumnDescription = "最近运行时间")]
    public DateTime? LastRunTime { get; set; }

    /// <summary>
    /// 下一次运行时间.
    /// </summary>
    [SugarColumn(ColumnDescription = "下一次运行时间")]
    public DateTime? NextRunTime { get; set; }

    /// <summary>
    /// 触发次数.
    /// </summary>
    [SugarColumn(ColumnDescription = "触发次数")]
    public long NumberOfRuns { get; set; }

    /// <summary>
    /// 最大触发次数（0:不限制，n:N次.
    /// </summary>
    [SugarColumn(ColumnDescription = "最大触发次数")]
    public long MaxNumberOfRuns { get; set; }

    /// <summary>
    /// 出错次数.
    /// </summary>
    [SugarColumn(ColumnDescription = "出错次数")]
    public long NumberOfErrors { get; set; }

    /// <summary>
    /// 最大出错次数（0:不限制，n:N次）.
    /// </summary>
    [SugarColumn(ColumnDescription = "最大出错次数")]
    public long MaxNumberOfErrors { get; set; }

    /// <summary>
    /// 重试次数.
    /// </summary>
    [SugarColumn(ColumnDescription = "重试次数")]
    public int NumRetries { get; set; }

    /// <summary>
    /// 重试间隔时间（ms）.
    /// </summary>
    [SugarColumn(ColumnDescription = "重试间隔时间(ms)")]
    public int RetryTimeout { get; set; } = 1000;

    /// <summary>
    /// 是否立即启动.
    /// </summary>
    [SugarColumn(ColumnDescription = "是否立即启动")]
    public bool StartNow { get; set; } = true;

    /// <summary>
    /// 是否启动时执行一次.
    /// </summary>
    [SugarColumn(ColumnDescription = "是否启动时执行一次")]
    public bool RunOnStart { get; set; } = false;

    /// <summary>
    /// 是否在启动时重置最大触发次数等于一次的作业.
    /// </summary>
    /// <remarks>解决因持久化数据已完成一次触发但启动时不再执行的问题</remarks>
    [SugarColumn(ColumnDescription = "是否在启动时重置最大触发次数等于一次的作业")]
    public bool ResetOnlyOnce { get; set; } = true;

    /// <summary>
    /// 更新时间.
    /// </summary>
    [SugarColumn(ColumnDescription = "更新时间")]
    public DateTime? UpdatedTime { get; set; }
}