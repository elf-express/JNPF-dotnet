using Microsoft.AspNetCore.Localization;

namespace JNPF.Localization;

/// <summary>
/// 自定义多语言查询参数
/// </summary>
public class CustomizeQueryStringRequestCultureProvider : QueryStringRequestCultureProvider
{
    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="queryStringKey"></param>
    /// <param name="uiQueryStringKey"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public CustomizeQueryStringRequestCultureProvider(string queryStringKey, string uiQueryStringKey = null)
    {
        // 空检查
        if (string.IsNullOrWhiteSpace(queryStringKey)) throw new ArgumentNullException(nameof(queryStringKey));

        QueryStringKey = queryStringKey;
        UIQueryStringKey = string.IsNullOrWhiteSpace(uiQueryStringKey) ? $"ui-{queryStringKey}" : uiQueryStringKey;
    }
}
