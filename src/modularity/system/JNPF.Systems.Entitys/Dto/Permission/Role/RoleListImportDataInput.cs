
namespace JNPF.Systems.Entitys.Dto.Role;

public class RoleListImportDataInput
{
    public string id { get; set; }

    public string fullName { get; set; }
    public string enCode { get; set; }
    public string sortCode { get; set; }
    public string description { get; set; }

    /// <summary>
    /// 状态.
    /// </summary>
    public string enabledMark { get; set; }

    /// <summary>
    /// 角色类型 1:全局 0 组织.
    /// </summary>
    public string globalMark { get; set; }

    /// <summary>
    /// 所属组织.
    /// </summary>
    public string organizeId { get; set; }

    public DateTime? creatorTime { get; set; }

    /// <summary>
    /// 异常错误原因.
    /// </summary>
    public string errorsInfo { get; set; } = string.Empty;
}

public class RoleImportDataInput
{
    /// <summary>
    /// 导入的数据列表.
    /// </summary>
    public List<RoleListImportDataInput> list { get; set; }
}
