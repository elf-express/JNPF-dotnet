﻿using JNPF.DependencyInjection;

namespace JNPF.Extend.Entitys.Dto.Order;

/// <summary>
/// 获取订单列表(带分页).
/// </summary>
[SuppressSniffer]
public class OrderListOutput
{
    /// <summary>
    /// 主键id.
    /// </summary>
    public string? id { get; set; }

    /// <summary>
    /// 订单编码.
    /// </summary>
    public string? orderCode { get; set; }

    /// <summary>
    /// 订单日期.
    /// </summary>
    public DateTime? orderDate { get; set; }

    /// <summary>
    /// 客户名称.
    /// </summary>
    public string? customerName { get; set; }

    /// <summary>
    /// 业务人员.
    /// </summary>
    public string? salesmanName { get; set; }

    /// <summary>
    /// 付款金额.
    /// </summary>
    public decimal? receivableMoney { get; set; }

    /// <summary>
    /// 制单人员.
    /// </summary>
    public string? creatorUser { get; set; }

    /// <summary>
    /// 备注.
    /// </summary>
    public string? description { get; set; }

    /// <summary>
    /// 状态.
    /// </summary>
    public int? currentState { get; set; } = 0;

    /// <summary>
    /// 创建人.
    /// </summary>
    public string? creatorUserId { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTime? creatorTime { get; set; }

    /// <summary>
    /// 修改时间.
    /// </summary>
    public DateTime? lastModifyTime { get; set; }

    /// <summary>
    /// 排序码.
    /// </summary>
    public long? sortCode { get; set; }

    /// <summary>
    /// 流程id.
    /// </summary>
    public string? flowId { get; set; }
}
