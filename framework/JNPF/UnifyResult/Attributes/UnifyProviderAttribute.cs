﻿namespace Microsoft.AspNetCore.Mvc;

/// <summary>
/// 规范化提供器特性
/// </summary>
[SuppressSniffer, AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class UnifyProviderAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public UnifyProviderAttribute()
        : this(string.Empty)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="name"></param>
    public UnifyProviderAttribute(string name)
    {
        Name = name;
    }

    /// <summary>
    /// 提供器名称
    /// </summary>
    public string Name { get; set; }
}