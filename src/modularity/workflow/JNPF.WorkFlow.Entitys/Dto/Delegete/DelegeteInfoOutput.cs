﻿using JNPF.DependencyInjection;

namespace JNPF.WorkFlow.Entitys.Dto.Delegete;

[SuppressSniffer]
public class DelegeteInfoOutput
{
    /// <summary>
    /// 描述.
    /// </summary>
    public string? description { get; set; }

    /// <summary>
    /// 结束时间.
    /// </summary>
    public DateTime? endTime { get; set; }

    /// <summary>
    /// 流程分类.
    /// </summary>
    public string? flowCategory { get; set; }

    /// <summary>
    /// 委托流程.
    /// </summary>
    public string? flowId { get; set; }

    /// <summary>
    /// 委托名称.
    /// </summary>
    public string? flowName { get; set; }

    /// <summary>
    /// 主键id.
    /// </summary>
    public string? id { get; set; }

    /// <summary>
    /// 开始时间.
    /// </summary>
    public DateTime? startTime { get; set; }

    /// <summary>
    /// 被委托人名.
    /// </summary>
    public string toUserName { get; set; }

    /// <summary>
    /// 被委托人id.
    /// </summary>
    public List<string> toUserId { get; set; }

    /// <summary>
    /// 委托类型(0:发起,1:审批).
    /// </summary>
    public string? type { get; set; }

    /// <summary>
    /// 委托人名.
    /// </summary>
    public string? userName { get; set; }

    /// <summary>
    /// 委托人id.
    /// </summary>
    public string? userId { get; set; }
}

