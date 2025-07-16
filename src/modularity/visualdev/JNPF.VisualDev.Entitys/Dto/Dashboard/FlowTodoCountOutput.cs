namespace JNPF.VisualDev.Entitys.Dto.Dashboard;

/// <summary>
/// 我的待办输出实体类.
/// </summary>
public class FlowTodoCountOutput
{
    /// <summary>
    /// 待签.
    /// </summary>
    public int flowToSign { get; set; }

    /// <summary>
    /// 待办.
    /// </summary>
    public int flowTodo { get; set; }

    /// <summary>
    /// 在办.
    /// </summary>
    public int flowDoing { get; set; }

    /// <summary>
    /// 已办.
    /// </summary>
    public int flowDone { get; set; }

    /// <summary>
    /// 抄送.
    /// </summary>
    public int flowCirculate { get; set; }
}
