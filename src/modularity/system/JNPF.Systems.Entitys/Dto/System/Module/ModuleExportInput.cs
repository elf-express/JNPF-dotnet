﻿using JNPF.DependencyInjection;

namespace JNPF.Systems.Entitys.Dto.Module;

/// <summary>
/// 功能导出基类.
/// </summary>
[SuppressSniffer]
public class ModuleExportBase
{
    /// <summary>
    /// id.
    /// </summary>
    public string id { get; set; }

    /// <summary>
    /// 名称.
    /// </summary>
    public string fullName { get; set; }

    /// <summary>
    /// 标识.
    /// </summary>
    public int? enabledMark { get; set; }

    /// <summary>
    /// 编码.
    /// </summary>
    public string enCode { get; set; }

    /// <summary>
    /// 菜单id.
    /// </summary>
    public string moduleId { get; set; }

    /// <summary>
    /// 属性.
    /// </summary>
    public string propertyJson { get; set; }

    /// <summary>
    /// 排序.
    /// </summary>
    public long? sortCode { get; set; }

    /// <summary>
    /// 创建时间.
    /// </summary>
    public DateTime? creatorTime { get; set; }

    /// <summary>
    /// 创建人.
    /// </summary>
    public string creatorUserId { get; set; }

    /// <summary>
    /// 修改时间.
    /// </summary>
    public DateTime? lastModifyTime { get; set; }

    /// <summary>
    /// 修改人.
    /// </summary>
    public string lastModifyUserId { get; set; }

    /// <summary>
    /// 删除标识.
    /// </summary>
    public int? deleteMark { get; set; }

    /// <summary>
    /// 删除时间.
    /// </summary>
    public DateTime? deleteTime { get; set; }

    /// <summary>
    /// 删除人.
    /// </summary>
    public string deleteUserId { get; set; }

    /// <summary>
    /// 备注.
    /// </summary>
    public string description { get; set; }
}

/// <summary>
/// 功能导出输入.
/// </summary>
[SuppressSniffer]
public class ModuleExportInput : ModuleExportBase
{
    /// <summary>
    /// 权限.
    /// </summary>
    public List<AuthorizeEntityListItem> authorizeEntityList { get; set; }

    /// <summary>
    /// 按钮.
    /// </summary>
    public List<ButtonEntityListItem> buttonEntityList { get; set; }

    /// <summary>
    /// 列表.
    /// </summary>
    public List<ColumnEntityListItem> columnEntityList { get; set; }

    /// <summary>
    /// 表单.
    /// </summary>
    public List<FromEntityListItem> formEntityList { get; set; }

    /// <summary>
    /// 数据权限方案.
    /// </summary>
    public List<SchemeEntityListItem> schemeEntityList { get; set; }

    /// <summary>
    /// 分类.
    /// </summary>
    public string category { get; set; }

    /// <summary>
    /// 图标.
    /// </summary>
    public string icon { get; set; }

    /// <summary>
    /// 按钮.
    /// </summary>
    public int? isButtonAuthorize { get; set; }

    /// <summary>
    /// 列表.
    /// </summary>
    public int? isColumnAuthorize { get; set; }

    /// <summary>
    /// 权限.
    /// </summary>
    public int? isDataAuthorize { get; set; }

    /// <summary>
    /// 表单.
    /// </summary>
    public int? isFormAuthorize { get; set; }

    /// <summary>
    /// 连接.
    /// </summary>
    public string linkTarget { get; set; }

    /// <summary>
    /// 父id.
    /// </summary>
    public string parentId { get; set; } = "0";

    /// <summary>
    /// 类型.
    /// </summary>
    public int? type { get; set; }

    /// <summary>
    /// 地址.
    /// </summary>
    public string urlAddress { get; set; }

    /// <summary>
    /// 系统id.
    /// </summary>
    public string systemId { get; set; }
}

/// <summary>
/// 权限方案.
/// </summary>
[SuppressSniffer]
public class AuthorizeEntityListItem : ModuleExportBase
{
    /// <summary>
    /// 条件明细.
    /// </summary>
    public string conditionSymbol { get; set; }

    /// <summary>
    /// 条件.
    /// </summary>
    public string conditionSymbolJson { get; set; }

    /// <summary>
    /// 条件内容.
    /// </summary>
    public string conditionText { get; set; }

    /// <summary>
    /// 类型.
    /// </summary>
    public string type { get; set; }

    /// <summary>
    /// 规则(0:主表，1：副表).
    /// </summary>
    public int? fieldRule { get; set; }

    /// <summary>
    /// 表名.
    /// </summary>
    public string bindTable { get; set; }

    /// <summary>
    /// 子表关联字段.
    /// </summary>
    public string childTableKey { get; set; }
}

/// <summary>
/// 按钮.
/// </summary>
[SuppressSniffer]
public class ButtonEntityListItem : ModuleExportBase
{
    /// <summary>
    /// 图标.
    /// </summary>
    public string icon { get; set; }

    /// <summary>
    /// 父id.
    /// </summary>
    public string parentId { get; set; }

    /// <summary>
    /// 连接地址.
    /// </summary>
    public string urlAddress { get; set; }
}

/// <summary>
/// 列表列.
/// </summary>
[SuppressSniffer]
public class ColumnEntityListItem : ModuleExportBase
{
    /// <summary>
    /// 表.
    /// </summary>
    public string bindTable { get; set; }

    /// <summary>
    /// 表名称.
    /// </summary>
    public string bindTableName { get; set; }

    /// <summary>
    /// 规则(0:主表，1：副表).
    /// </summary>
    public int? fieldRule { get; set; }

    /// <summary>
    /// 子表关联字段.
    /// </summary>
    public string childTableKey { get; set; }
}

/// <summary>
/// 权限资源.
/// </summary>
[SuppressSniffer]
public class SchemeEntityListItem : ModuleExportBase
{
    /// <summary>
    /// 条件json.
    /// </summary>
    public string conditionJson { get; set; }

    /// <summary>
    /// 条件文本.
    /// </summary>
    public string conditionText { get; set; }

    /// <summary>
    /// 分组逻辑条件.
    /// </summary>
    public string matchLogic { get; set; }
}

/// <summary>
/// 表单.
/// </summary>
[SuppressSniffer]
public class FromEntityListItem : ModuleExportBase
{
    /// <summary>
    /// 父id.
    /// </summary>
    public string parentId { get; set; }

    /// <summary>
    /// 规则(0:主表，1：副表).
    /// </summary>
    public int? fieldRule { get; set; }

    /// <summary>
    /// 表名.
    /// </summary>
    public string bindTable { get; set; }

    /// <summary>
    /// 子表关联字段.
    /// </summary>
    public string childTableKey { get; set; }
}