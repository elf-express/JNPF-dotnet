﻿using JNPF.Common.Security;
using JNPF.DependencyInjection;

namespace JNPF.Systems.Entitys.Dto.DictionaryType;

/// <summary>
/// 字典类型列表输出.
/// </summary>
[SuppressSniffer]
public class DictionaryTypeListOutput : TreeModel
{
    /// <summary>
    /// 名称.
    /// </summary>
    public string fullName { get; set; }

    /// <summary>
    /// 编码.
    /// </summary>
    public string enCode { get; set; }

    /// <summary>
    /// 判断字典列表展示方式(1-树形列表，0-普通列表).
    /// </summary>
    public int isTree { get; set; }

    /// <summary>
    /// 排序.
    /// </summary>
    public long? sortCode { get; set; }
}