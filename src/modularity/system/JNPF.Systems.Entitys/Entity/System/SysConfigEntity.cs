﻿using JNPF.Common.Contracts;
using SqlSugar;

namespace JNPF.Systems.Entitys.System;

/// <summary>
/// 系统设置.
/// </summary>
[SugarTable("BASE_SYS_CONFIG")]
public class SysConfigEntity : CLDEntityBase
{
    /// <summary>
    /// 名称.
    /// </summary>
    [SugarColumn(ColumnName = "F_FULL_NAME", ColumnDescription = "名称")]
    public string FullName { get; set; }

    /// <summary>
    /// 键.
    /// </summary>
    [SugarColumn(ColumnName = "F_KEY", ColumnDescription = "键")]
    public string Key { get; set; }

    /// <summary>
    /// 值.
    /// </summary>
    [SugarColumn(ColumnName = "F_VALUE", ColumnDescription = "值")]
    public string Value { get; set; }

    /// <summary>
    /// 分类.
    /// </summary>
    [SugarColumn(ColumnName = "F_CATEGORY", ColumnDescription = "分类")]
    public string Category { get; set; }
}