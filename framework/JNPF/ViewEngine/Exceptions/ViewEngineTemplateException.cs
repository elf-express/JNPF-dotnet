using Microsoft.CodeAnalysis;

namespace JNPF.ViewEngine;

/// <summary>
/// 视图引擎模板编译异常类
/// </summary>
[SuppressSniffer]
public class ViewEngineTemplateException : ViewEngineException
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public ViewEngineTemplateException()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="innerException"></param>
    public ViewEngineTemplateException(Exception innerException) : base(null, innerException)
    {
    }

    /// <summary>
    /// 错误信息
    /// </summary>
    public List<Diagnostic> Errors { get; set; }

    /// <summary>
    /// 生成的代码
    /// </summary>
    public string GeneratedCode { get; set; }

    /// <summary>
    /// 重写异常消息
    /// </summary>
    public override string Message => $"Unable to compile template: {string.Join("\n", Errors.Where(w => w.IsWarningAsError || w.Severity == DiagnosticSeverity.Error))}";
}