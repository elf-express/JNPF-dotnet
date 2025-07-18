﻿using JNPF.DependencyInjection;

namespace JNPF.Systems.Entitys.Dto.Department;

/// <summary>
/// 部门信息输出 .
/// </summary>
[SuppressSniffer]
public class DepartmentInfoOutput
{
    /// <summary>
    /// id.
    /// </summary>
    public string id { get; set; }

    /// <summary>
    /// 上级ID.
    /// </summary>
    public string parentId { get; set; }

    /// <summary>
    /// 部门名称.
    /// </summary>
    public string fullName { get; set; }

    /// <summary>
    /// 部门编码.
    /// </summary>
    public string enCode { get; set; }

    /// <summary>
    /// 状态.
    /// </summary>
    public int? enabledMark { get; set; }

    /// <summary>
    /// 备注.
    /// </summary>
    public string description { get; set; }

    /// <summary>
    /// 主管ID.
    /// </summary>
    public string managerId { get; set; }

    /// <summary>
    /// 排序码.
    /// </summary>
    public long? sortCode { get; set; }

    /// <summary>
    /// 所属组织 组织树.
    /// </summary>
    public List<string> organizeIdTree { get; set; }
}