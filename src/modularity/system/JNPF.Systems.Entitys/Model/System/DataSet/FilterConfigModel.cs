using JNPF.DependencyInjection;

namespace JNPF.Systems.Entitys.Model.System.DataSet;

/// <summary>
/// 条件筛选.
/// </summary>
[SuppressSniffer]
public class FilterConfigModel
{
    /// <summary>
    /// 分组逻辑： 并且、或者.
    /// </summary>
    public string matchLogic { get; set; }

    /// <summary>
    /// 分组.
    /// </summary>
    public List<RuleModel> ruleList { get; set; }
}

/// <summary>
/// 分组.
/// </summary>
[SuppressSniffer]
public class RuleModel
{
    /// <summary>
    /// 分组条件.
    /// </summary>
    public string logic { get; set; }

    /// <summary>
    /// 分组内条件.
    /// </summary>
    public List<GroupsModel> groups { get; set; }
}

/// <summary>
/// 分组内条件.
/// </summary>
[SuppressSniffer]
public class GroupsModel
{
    /// <summary>
    /// 字段过滤值.
    /// </summary>
    public object fieldValue { get; set; }

    /// <summary>
    /// 字段.
    /// </summary>
    public string field { get; set; }

    /// <summary>
    /// 条件符号标识.
    /// </summary>
    public string symbol { get; set; }

    /// <summary>
    /// 数据类型.
    /// </summary>
    public string dataType { get; set; }

    /// <summary>
    /// 字段类型.
    /// 1:自定义,2:系统参数.
    /// </summary>
    public int fieldValueType { get; set; }
}