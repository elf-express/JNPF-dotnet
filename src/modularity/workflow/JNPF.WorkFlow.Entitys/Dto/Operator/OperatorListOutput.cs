﻿using JNPF.DependencyInjection;
using Newtonsoft.Json;

namespace JNPF.WorkFlow.Entitys.Dto.Operator;

[SuppressSniffer]
public class OperatorListOutput
{
    /// <summary>
    /// 编码.
    /// </summary>
    public string? enCode { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public string? creatorUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTime? creatorTime { get; set; }

    /// <summary>
    /// 当前节点名.
    /// </summary>
    public string? currentNodeName { get; set; }

    /// <summary>
    /// 流程分类.
    /// </summary>
    public string? flowCategory { get; set; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string? fullName { get; set; }

    /// <summary>
    /// 引擎名称.
    /// </summary>
    public string? flowName { get; set; }

    /// <summary>
    /// 状态.
    /// </summary>
    public int? status { get; set; }

    /// <summary>
    /// 发起时间.
    /// </summary>
    public DateTime? startTime { get; set; }

    /// <summary>
    /// id.
    /// </summary>
    public string? id { get; set; }

    /// <summary>
    /// 发起人.
    /// </summary>
    public string? creatorUser { get; set; }

    /// <summary>
    /// 说明.
    /// </summary>
    public string? description { get; set; }

    /// <summary>
    /// 引擎编码.
    /// </summary>
    public string? flowCode { get; set; }

    /// <summary>
    /// 引擎id.
    /// </summary>
    public string? flowId { get; set; }

    /// <summary>
    /// 任务id.
    /// </summary>
    public string? taskId { get; set; }

    /// <summary>
    /// 审批id.
    /// </summary>
    public string? handleId { get; set; }

    /// <summary>
    /// 表单类型.
    /// </summary>
    public int? formType { get; set; }

    /// <summary>
    /// 表单Json.
    /// </summary>
    public string? formData { get; set; }

    /// <summary>
    /// 紧急程度.
    /// </summary>
    public int? flowUrgent { get; set; }

    /// <summary>
    /// 流程进度.
    /// </summary>
    public int? completion { get; set; }

    /// <summary>
    /// 所属节点.
    /// </summary>
    public string? nodeName { get; set; }

    /// <summary>
    /// 节点属性.
    /// </summary>
    public string? approversProperties { get; set; }

    /// <summary>
    /// 所属节点.
    /// </summary>
    [JsonIgnore]
    public string? nodeCode { get; set; }

    /// <summary>
    /// 流程版本.
    /// </summary>
    public string? flowVersion { get; set; }

    /// <summary>
    /// 流程主表id.
    /// </summary>
    public string? templateId { get; set; }

    /// <summary>
    /// 委托发起人.
    /// </summary>
    public string? delegateUser { get; set; }

    /// <summary>
    /// 挂起（0：否，1：是）.
    /// </summary>
    public int? suspend { get; set; }

    /// <summary>
    /// 紧急程度.
    /// </summary>
    public int? isBatch { get; set; }

    /// <summary>
    /// 租户id.
    /// </summary>
    public string? tenantId { get; set; }

    /// <summary>
    /// 已读.
    /// </summary>
    public int? isRead { get; set; }

    /// <summary>
    /// 办理.
    /// </summary>
    public int? isProcessing { get; set; }
}