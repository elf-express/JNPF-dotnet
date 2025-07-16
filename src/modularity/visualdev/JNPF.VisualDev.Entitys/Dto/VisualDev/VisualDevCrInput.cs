﻿using JNPF.DependencyInjection;

namespace JNPF.VisualDev.Entitys.Dto.VisualDev;

/// <summary>
/// 新建功能输入.
/// </summary>
[SuppressSniffer]
public class VisualDevCrInput
{
    /// <summary>
    /// 功能名称.
    /// </summary>
    public string? fullName { get; set; }

    /// <summary>
    /// 功能编码.
    /// </summary>
    public string? enCode { get; set; }

    /// <summary>
    /// 分类id.
    /// </summary>
    public string? category { get; set; }

    /// <summary>
    /// 功能类型
    /// 1-自定义表单，2-系统表单.
    /// </summary>
    public int type { get; set; }

    /// <summary>
    /// 说明.
    /// </summary>
    public string? description { get; set; }

    /// <summary>
    /// 表单JSON包.
    /// </summary>
    public string? formData { get; set; }

    /// <summary>
    /// 列表JSON包.
    /// </summary>
    public string? columnData { get; set; }

    /// <summary>
    /// App列表JSON包.
    /// </summary>
    public string? appColumnData { get; set; }

    /// <summary>
    /// 数据表JSON包,无表传空.
    /// </summary>
    public string? tables { get; set; }

    /// <summary>
    /// 1-纯表单,2-列表表单,3-工作流表单.
    /// </summary>
    public int webType { get; set; }

    /// <summary>
    /// 数据源id.
    /// </summary>
    public string? dbLinkId { get; set; }

    /// <summary>
    /// 工作流模板Json.
    /// </summary>
    public string? flowTemplateJson { get; set; }

    /// <summary>
    /// 排序.
    /// </summary>
    public long? sortCode { get; set; }

    /// <summary>
    /// 接口id.
    /// </summary>
    public string interfaceId { get; set; }

    /// <summary>
    /// 接口参数.
    /// </summary>
    public string interfaceParam { get; set; }

    /// <summary>
    /// Web地址.
    /// </summary>
    public string urlAddress { get; set; }

    /// <summary>
    /// APP地址.
    /// </summary>
    public string appUrlAddress { get; set; }

    /// <summary>
    /// 接口路径.
    /// </summary>
    public string interfaceUrl { get; set; }
}
