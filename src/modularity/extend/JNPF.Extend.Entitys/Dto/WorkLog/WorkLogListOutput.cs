﻿using JNPF.DependencyInjection;

namespace JNPF.Extend.Entitys.Dto.WorkLog;

/// <summary>
/// 
/// </summary>
[SuppressSniffer]
public class WorkLogListOutput
{
    /// <summary>
    /// id.
    /// </summary>
    public string? id { get; set; }

    /// <summary>
    /// 标题.
    /// </summary>
    public string? title { get; set; }

    /// <summary>
    /// 问题内容.
    /// </summary>
    public string? question { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTime? creatorTime { get; set; }

    /// <summary>
    /// 今日内容.
    /// </summary>
    public string? todayContent { get; set; }

    /// <summary>
    /// 明日内容.
    /// </summary>
    public string? tomorrowContent { get; set; }

    /// <summary>
    /// 接收人.
    /// </summary>
    public string? toUserId { get; set; }

    /// <summary>
    /// 修改时间.
    /// </summary>
    public DateTime? lastModifyTime { get; set; }

    /// <summary>
    /// 排序码.
    /// </summary>
    public long? sortCode { get; set; }
}
