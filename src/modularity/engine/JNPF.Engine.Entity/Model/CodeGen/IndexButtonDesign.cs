﻿using JNPF.DependencyInjection;

namespace JNPF.Engine.Entity.Model.CodeGen;

/// <summary>
/// 代码生成常规Index列表页面头部按钮配置.
/// </summary>
[SuppressSniffer]
public class IndexButtonDesign
{
    /// <summary>
    /// 按钮类型.
    /// </summary>
    public string @Type { get; set; }

    /// <summary>
    /// 图标.
    /// </summary>
    public string Icon { get; set; }

    /// <summary>
    /// 方法.
    /// </summary>
    public string Method { get; set; }

    /// <summary>
    /// 按钮值.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// 按钮文本.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// 是否禁用.
    /// </summary>
    public string Disabled { get; set; }

    /// <summary>
    /// 是否显示.
    /// </summary>
    public bool Show { get; set; } = true;
}