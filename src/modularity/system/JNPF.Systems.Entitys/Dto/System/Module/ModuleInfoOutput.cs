﻿using JNPF.DependencyInjection;

namespace JNPF.Systems.Entitys.Dto.Module;

/// <summary>
/// 功能信息输出.
/// </summary>
[SuppressSniffer]
public class ModuleInfoOutput
{
    /// <summary>
    /// id.
    /// </summary>
    public string id { get; set; }

    /// <summary>
    /// 上级菜单.
    /// </summary>
    public string parentId { get; set; }

    /// <summary>
    /// 菜单名称.
    /// </summary>
    public string fullName { get; set; }

    /// <summary>
    /// 菜单编码.
    /// </summary>
    public string enCode { get; set; }

    /// <summary>
    /// 菜单图标.
    /// </summary>
    public string icon { get; set; }

    /// <summary>
    /// 菜单类型(1-目录，2-页面).
    /// </summary>
    public int? type { get; set; }

    /// <summary>
    /// 链接地址.
    /// </summary>
    public string urlAddress { get; set; }

    /// <summary>
    /// 链接方式(_self,_blank).
    /// </summary>
    public string linkTarget { get; set; }

    /// <summary>
    /// 菜单分类.
    /// </summary>
    public string category { get; set; }

    /// <summary>
    /// 状态.
    /// </summary>
    public int? enabledMark { get; set; }

    /// <summary>
    /// 是否开启数据权限(1-开启,0-未开启).
    /// </summary>
    public int? isDataAuthorize { get; set; }

    /// <summary>
    ///  是否开启列表权限(1-开启,0-未开启).
    /// </summary>
    public int? isColumnAuthorize { get; set; }

    /// <summary>
    /// 是否开启按钮权限(1-开启,0-未开启).
    /// </summary>
    public int? isButtonAuthorize { get; set; }

    /// <summary>
    /// 排序.
    /// </summary>
    public long? sortCode { get; set; }

    /// <summary>
    /// 说明.
    /// </summary>
    public string description { get; set; }

    /// <summary>
    /// 扩展信息.
    /// </summary>
    public string propertyJson { get; set; }

    /// <summary>
    /// 系统id.
    /// </summary>
    public string systemId { get; set; }
}