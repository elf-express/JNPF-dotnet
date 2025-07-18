﻿using JNPF.DependencyInjection;

namespace JNPF.Systems.Entitys.Dto.ModuleDataAuthorizeScheme;

/// <summary>
/// 功能权限数据计划列表输出.
/// </summary>
[SuppressSniffer]
public class ModuleDataAuthorizeSchemeListOutput
{
    /// <summary>
    /// id.
    /// </summary>
    public string id { get; set; }

    /// <summary>
    /// 方案名称.
    /// </summary>
    public string fullName { get; set; }

    /// <summary>
    /// 方案名称.
    /// </summary>
    public string enCode { get; set; }

    /// <summary>
    /// 过滤条件.
    /// </summary>
    public string conditionText { get; set; }
}