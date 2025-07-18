﻿using JNPF.DependencyInjection;

namespace JNPF.Systems.Entitys.Dto.Role;

/// <summary>
/// 角色缓存列表输出.
/// </summary>
[SuppressSniffer]
public class RoleCacheListOutput
{
    /// <summary>
    /// id.
    /// </summary>
    public string id { get; set; }

    /// <summary>
    /// 角色名称.
    /// </summary>
    public string fullName { get; set; }
}