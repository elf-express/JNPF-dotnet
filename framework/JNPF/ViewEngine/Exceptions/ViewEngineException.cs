﻿namespace JNPF.ViewEngine;

/// <summary>
/// 视图引擎异常类
/// </summary>
[SuppressSniffer]
public class ViewEngineException : Exception
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public ViewEngineException()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    public ViewEngineException(string message) : base(message)
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    public ViewEngineException(string message, Exception innerException) : base(message, innerException)
    {
    }
}