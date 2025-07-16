namespace JNPF.Common.Models.User;

/// <summary>
/// 用户身份.
/// </summary>
public class UserStanding
{
    /// <summary>
    /// 1.管理员 2.超管 3.普通用户.
    /// </summary>
    public string id { get; set; }

    /// <summary>
    /// 身份描述.
    /// </summary>
    public string name { get; set; }

    /// <summary>
    /// 是否当前身份.
    /// </summary>
    public bool currentStanding { get; set; }

    /// <summary>
    /// 是否当前应用.
    /// </summary>
    public bool currentSystem { get; set; }

    public string icon { get; set; }
}
