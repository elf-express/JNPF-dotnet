﻿using JNPF.Common.Dtos.Datainterface;
using JNPF.DependencyInjection;

namespace JNPF.WorkFlow.Entitys.Model.Conifg;

[SuppressSniffer]
public class TimeOutConfig
{
    /// <summary>
    /// 0：关闭 1：自定义  2：同步发起配置.
    /// </summary>
    public int on { get; set; } = 0;

    /// <summary>
    /// 起始时间类型（0：接收时间 1：发起时间  2：表单变量）.
    /// </summary>
    public int nodeLimit { get; set; } = 0;

    /// <summary>
    /// 表单字段.
    /// </summary>
    public string? formField { get; set; }

    /// <summary>
    /// 处理限定时长默认24小时.
    /// </summary>
    public int duringDeal { get; set; } = 24;

    /// <summary>
    /// 第一次触发时长.
    /// </summary>
    public int firstOver { get; set; } = 0;

    /// <summary>
    /// 间隔.
    /// </summary>
    public int overTimeDuring { get; set; } = 2;

    /// <summary>
    /// 通知.
    /// </summary>
    public bool overNotice { get; set; }

    /// <summary>
    /// 事件.
    /// </summary>
    public bool overEvent { get; set; }

    /// <summary>
    /// 事件触发次数.
    /// </summary>
    public int overEventTime { get; set; } = 5;

    /// <summary>
    /// 自动审批.
    /// </summary>
    public bool overAutoApprove { get; set; }

    /// <summary>
    /// 自动审批触发次数.
    /// </summary>
    public int overAutoApproveTime { get; set; } = 5;

    /// <summary>
    /// 自动转审.
    /// </summary>
    public bool overAutoTransfer { get; set; }

    /// <summary>
    /// 自动转审触发次数.
    /// </summary>
    public int overAutoTransferTime { get; set; } = 5;

    /// <summary>
    /// 转审人类型 6-指定成员 9-数据接口 11- 超时审批人 .
    /// </summary>
    public int overTimeType { get; set; } = 5;

    /// <summary>
    /// 自动转审触发次数.
    /// </summary>
    public string interfaceId { get; set; }

    /// <summary>
    /// 超时审批人过滤规则.
    /// </summary>
    public string overTimeExtraRule { get; set; } 

    /// <summary>
    /// 超时审批人过滤规则.
    /// </summary>
    public string reApprovers { get; set; } 

    /// <summary>
    /// .
    /// </summary>
    public List<DataInterfaceParameter> templateJson { get; set; } = new List<DataInterfaceParameter>();
}
