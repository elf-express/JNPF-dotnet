using JNPF.DependencyInjection;

namespace JNPF.Systems.Entitys.Model.System.DataSet;

/// <summary>
/// 数据集数据条件模型.
/// </summary>
[SuppressSniffer]
public class DataSetDataQueryModel
{
    /// <summary>
    /// id.
    /// </summary>
    public string id { get; set; }

    /// <summary>
    /// 字段.
    /// </summary>
    public string vModel { get; set; }

    /// <summary>
    /// 列名.
    /// </summary>
    public string label { get; set; }

    /// <summary>
    /// 输入类型.
    /// </summary>
    public string type { get; set; }

    /// <summary>
    /// 条件类型.
    /// </summary>
    public int? searchType { get; set; }

    /// <summary>
    /// 是否多选.
    /// </summary>
    public bool searchMultiple { get; set; }

    /// <summary>
    /// 是否查询子组织.
    /// </summary>
    public bool isIncludeSubordinate { get; set; }

    /// <summary>
    /// 配置.
    /// </summary>
    public object config { get; set; }
}
