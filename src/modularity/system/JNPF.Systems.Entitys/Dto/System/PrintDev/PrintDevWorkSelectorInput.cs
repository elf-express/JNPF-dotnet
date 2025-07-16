using JNPF.Common.Filter;
using JNPF.DependencyInjection;

namespace JNPF.Systems.Entitys.Dto.PrintDev;

/// <summary>
/// 打印模板业务列表输入.
/// </summary>
[SuppressSniffer]
public class PrintDevWorkSelectorInput : PageInputBase
{
    /// <summary>
    /// 分类.
    /// </summary>
    public string category { get; set; }
}