﻿using JNPF.DependencyInjection;

namespace JNPF.Systems.Entitys.Dto.User;

/// <summary>
/// 用户全列表.
/// </summary>
[SuppressSniffer]
public class UserListAllOutput
{
    /// <summary>
    /// 用户编号.
    /// </summary>
    public string id { get; set; }

    /// <summary>
    /// 用户账号.
    /// </summary>
    public string account { get; set; }

    /// <summary>
    /// 用户名称.
    /// </summary>
    public string realName { get; set; }

    /// <summary>
    /// 用户头像.
    /// </summary>
    public string headIcon { get; set; }

    /// <summary>
    /// 用户性别.
    /// </summary>
    public string? gender { get; set; }

    /// <summary>
    /// 用户部门.
    /// </summary>
    public string department { get; set; }

    /// <summary>
    /// 用户排序.
    /// </summary>
    public long? sortCode { get; set; }

    /// <summary>
    /// 快速查询.
    /// </summary>
    public string quickQuery { get; set; }
}