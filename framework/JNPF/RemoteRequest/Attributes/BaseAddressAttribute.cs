namespace JNPF.RemoteRequest;

/// <summary>
/// 配置客户端 BaseAddress
/// </summary>
[SuppressSniffer, AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method)]
public class BaseAddressAttribute : Attribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="baseAddress"></param>
    public BaseAddressAttribute(string baseAddress)
    {
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// 客户端名称
    /// </summary>
    public string BaseAddress { get; set; }
}