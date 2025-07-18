﻿using JNPF.DependencyInjection;

namespace JNPF.Systems.Entitys.Dto.ComFields;

/// <summary>
/// 常用字段信息输出.
/// </summary>
[SuppressSniffer]
public class ComFieldsInfoOutput
{
    /// <summary>
    /// id.
    /// </summary>
    public string id { get; set; }

    /// <summary>
    /// 长度.
    /// </summary>
    public string dataLength { get; set; }

    /// <summary>
    /// 类型.
    /// </summary>
    public string dataType { get; set; }

    /// <summary>
    /// 允许空(1-允许，0-不允许).
    /// </summary>
    public int allowNull { get; set; }

    /// <summary>
    /// 添加时间.
    /// </summary>
    public DateTime? creatorTime { get; set; }

    /// <summary>
    /// 列名.
    /// </summary>
    public string field { get; set; }

    /// <summary>
    /// 字段注释.
    /// </summary>
    public string fieldName { get; set; }
}