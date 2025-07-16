﻿using JNPF.DependencyInjection;

namespace JNPF.WorkFlow.Entitys.Dto.Comment;

[SuppressSniffer]
public class CommentInfoOutput
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
    /// 流程id.
    /// </summary>
    public string? flowId { get; set; }

    /// <summary>
    /// 流程名称.
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
    /// 评论人.
    /// </summary>
    public string? toUserName { get; set; }

    /// <summary>
    /// 评论人id.
    /// </summary>
    public string? toUserId { get; set; }
}

