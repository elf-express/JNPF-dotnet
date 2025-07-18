﻿using JNPF.Common.Security;
using JNPF.DependencyInjection;

namespace JNPF.Systems.Entitys.Dto.User;

/// <summary>
/// 用户下拉框输出.
/// </summary>
[SuppressSniffer]
public class UserSelectorOutput : TreeModel
{
    /// <summary>
    /// 名称.
    /// </summary>
    public string fullName { get; set; }

    /// <summary>
    /// 有效标记.
    /// </summary>
    public int? enabledMark { get; set; }

    /// <summary>
    /// 类型.
    /// </summary>
    public string type { get; set; }

    /// <summary>
    /// 图标.
    /// </summary>
    public string icon { get; set; }

    /// <summary>
    /// 排序.
    /// </summary>
    public long? sortCode { get; set; }

    /// <summary>
    /// 组织树.
    /// </summary>
    public string organizeInfo { get; set; }
}