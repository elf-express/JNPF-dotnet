namespace JNPF.SensitiveDetection;

/// <summary>
/// 脱敏词汇构建器
/// </summary>
[SuppressSniffer]
public sealed class SensitiveDetectionBuilder
{
    /// <summary>
    /// 脱敏词汇数据文件名
    /// </summary>
    public string EmbedFileName { get; set; } = "sensitive-words.txt";
}