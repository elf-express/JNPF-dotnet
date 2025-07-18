﻿using JNPF.Common.Security;
using JNPF.DependencyInjection;
using System.Text.Json.Serialization;

namespace JNPF.Systems.Entitys.Model.UsersCurrent;

/// <summary>
/// 当前用户权限模型.
/// </summary>
[SuppressSniffer]
public class UsersCurrentAuthorizeMoldel : TreeModel
{
    /// <summary>
    /// 名称.
    /// </summary>
    public string fullName { get; set; }

    /// <summary>
    /// 图标.
    /// </summary>
    public string icon { get; set; }

    /// <summary>
    /// 排序.
    /// </summary>
    public long? sortCode { get; set; }

    /// <summary>
    /// 功能主键.
    /// </summary>
    [JsonIgnore]
    public string moduleId { get; set; }
}