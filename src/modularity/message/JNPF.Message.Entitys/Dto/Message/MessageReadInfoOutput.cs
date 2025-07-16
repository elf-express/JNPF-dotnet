﻿using JNPF.DependencyInjection;
using SqlSugar;

namespace JNPF.Message.Entitys.Dto.Message;

/// <summary>
/// 读取消息信息输出.
/// </summary>
[SuppressSniffer]
public class MessageReadInfoOutput
{
    /// <summary>
    /// id.
    /// </summary>
    public string id { get; set; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string title { get; set; }

    /// <summary>
    /// 正文内容.
    /// </summary>
    public string bodyText { get; set; }

    /// <summary>
    /// 发送人员.
    /// </summary>
    public string releaseUser { get; set; }

    /// <summary>
    /// 发送时间.
    /// </summary>
    public DateTime? releaseTime { get; set; }

    /// <summary>
    /// 附件.
    /// </summary>
    public string files { get; set; }

    /// <summary>
    /// 摘要.
    /// </summary>
    public string excerpt { get; set; }

    /// <summary>
    /// 流程跳转类型 1:审批 2:委托.
    /// </summary>
    public int? flowType { get; set; }

    /// <summary>
    /// 流程跳转类型 1:审批 2:委托.
    /// </summary>
    public int? type { get; set; }
}