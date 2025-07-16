
namespace JNPF.Systems.Entitys.Dto.Position;

public class PositionListImportDataInput
{
    public string fullName { get; set; }
    public string enCode { get; set; }
    public string sortCode { get; set; }
    public string description { get; set; }

    /// <summary>
    /// 状态.
    /// </summary>
    public string enabledMark { get; set; }

    /// <summary>
    /// 岗位类型.
    /// </summary>
    public string type { get; set; }

    /// <summary>
    /// 所属组织.
    /// </summary>
    public string organizeId { get; set; }

    /// <summary>
    /// 异常错误原因.
    /// </summary>
    public string errorsInfo { get; set; } = string.Empty;
}

public class PositionImportDataInput
{
    /// <summary>
    /// 导入的数据列表.
    /// </summary>
    public List<PositionListImportDataInput> list { get; set; }
}
