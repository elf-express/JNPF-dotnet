using Microsoft.AspNetCore.Mvc;

namespace JNPF.AspNetCore;

/// <summary>
/// 数组 URL 地址参数模型绑定特性
/// </summary>
[SuppressSniffer]
public class FlexibleArrayAttribute<T> : ModelBinderAttribute
{
    /// <summary>
    /// 构造函数
    /// </summary>
    public FlexibleArrayAttribute()
        : base(typeof(FlexibleArrayModelBinder<T>))
    {
    }
}
