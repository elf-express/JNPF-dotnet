using JNPF.Common.Security;
using JNPF.Extras.Thirdparty.AI.Internal;
using JNPF.JsonSerialization;
using JNPF.RemoteRequest.Extensions;

namespace JNPF.Extras.Thirdparty.AI;

/// <summary>
/// 阿里云 AI.
/// </summary>
public class AIUtil
{
    private static readonly string? _host = App.Configuration["AI:ApiHost"];
    private static readonly string? _apiKey = App.Configuration["AI:ApiKey"];
    private static readonly string? _model = App.Configuration["AI:Model"];

    /// <summary>
    /// 发起 AI 请求.
    /// </summary>
    public static async Task<string> SendAIRequestAsync(string systemQuestion, string userQuestion)
    {
        var path = string.Format("{0}v1/chat/completions", _host);

        var headers = new Dictionary<string, object>();
        headers.Add("Authorization", "Bearer " + _apiKey);

        var parameter = new AIParameter()
        {
            model = _model,
            messages = new List<MessageModel>
            {
                new MessageModel() { role = "system", content = systemQuestion},
                new MessageModel() { role = "user", content = userQuestion }
            }
        };

        var res = await path.SetJsonSerialization<NewtonsoftJsonSerializerProvider>().SetContentType("application/json").SetHeaders(headers).SetBody(parameter).PostAsStringAsync();
        var output = res.ToObject<AIOutputModel>();

        return output.choices?[0].message.content;
    }
}