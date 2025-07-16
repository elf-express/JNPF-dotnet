using JNPF.Common.Contracts;
using SqlSugar;

namespace JNPF.Systems.Entitys.Permission;

/// <summary>
/// 权限组
/// 版 本：V3.5.0
/// 版 权：引迈信息技术有限公司（https://www.jnpfsoft.com）
/// 作 者：JNPF开发平台组
/// 日 期：2023.06.29.
/// </summary>
[SugarTable("BASE_PERMISSION_GROUP")]
public class PermissionGroupEntity : CLDSEntityBase
{
    /// <summary>
    /// 编码.
    /// </summary>
    [SugarColumn(ColumnName = "F_EN_CODE")]
    public string EnCode { get; set; }

    /// <summary>
    /// 名称.
    /// </summary>
    [SugarColumn(ColumnName = "F_FULL_NAME")]
    public string FullName { get; set; }

    /// <summary>
    /// 类型 (0:全部成员,1或null:自定义).
    /// </summary>
    [SugarColumn(ColumnName = "F_TYPE")]
    public int? Type { get; set; }

    /// <summary>
    /// 成员(组织、角色、岗位、分组、用户 Ids).
    /// </summary>
    [SugarColumn(ColumnName = "F_PERMISSION_MEMBER")]
    public string PermissionMember { get; set; }

    /// <summary>
    /// 描述.
    /// </summary>
    [SugarColumn(ColumnName = "F_DESCRIPTION")]
    public string Description { get; set; }
}