using JNPF.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace JNPF.Common.Const;

/// <summary>
/// 公共常量.
/// </summary>
[SuppressSniffer]
public class CommonConst
{
    // 不带自定义转换器
    public static JsonSerializerSettings options => new JsonSerializerSettings
    {
        MaxDepth = 64,

        // 格式化JSON文本
        Formatting = Formatting.Indented,

        // 默认命名规则
        ContractResolver = new DefaultContractResolver(),

        // 设置时区为 Utc
        DateTimeZoneHandling = DateTimeZoneHandling.Utc,

        // 格式化json输出的日期格式
        DateFormatString = "yyyy-MM-dd HH:mm:ss",

        // 忽略循环引用
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
    };

    /// <summary>
    /// 全局租户缓存.
    /// </summary>
    public const string GLOBALTENANT = "jnpf:global:tenant";

    /// <summary>
    /// 集成助手缓存.
    /// </summary>
    public const string INTEASSISTANT = "jnpf:global:integrate";

    /// <summary>
    /// 集成助手重试缓存.
    /// </summary>
    public const string INTEASSISTANTRETRY = "jnpf:global:integrate:retry";

    /// <summary>
    /// 集成助手WebHook.
    /// </summary>
    public const string INTEGRATEWEBHOOK = "jnpf:global:integrate:webhook";

    /// <summary>
    /// 默认密码.
    /// </summary>
    public const string DEFAULTPASSWORD = "0000";

    /// <summary>
    /// 用户缓存.
    /// </summary>
    public const string CACHEKEYUSER = "jnpf:permission:user";

    /// <summary>
    /// 菜单缓存.
    /// </summary>
    public const string CACHEKEYMENU = "menu_";

    /// <summary>
    /// 权限缓存.
    /// </summary>
    public const string CACHEKEYPERMISSION = "permission_";

    /// <summary>
    /// 数据范围缓存.
    /// </summary>
    public const string CACHEKEYDATASCOPE = "datascope_";

    /// <summary>
    /// 验证码缓存.
    /// </summary>
    public const string CACHEKEYCODE = "vercode_";

    /// <summary>
    /// 单据编码缓存.
    /// </summary>
    public const string CACHEKEYBILLRULE = "billrule_";

    /// <summary>
    /// 在线用户缓存.
    /// </summary>
    public const string CACHEKEYONLINEUSER = "jnpf:user:online";

    /// <summary>
    /// 全局组织树缓存.
    /// </summary>
    public const string CACHEKEYORGANIZE = "jnpf:global:organize";

    /// <summary>
    /// 岗位缓存.
    /// </summary>
    public const string CACHEKEYPOSITION = "position_";

    /// <summary>
    /// 角色缓存.
    /// </summary>
    public const string CACHEKEYROLE = "role_";

    /// <summary>
    /// 在线开发缓存.
    /// </summary>
    public const string VISUALDEV = "visualdev_";

    /// <summary>
    /// 代码生成远端数据缓存.
    /// </summary>
    public const string CodeGenDynamic = "codegendynamic_";

    /// <summary>
    /// 定时任务缓存.
    /// </summary>
    public const string CACHEKEYTIMERJOB = "timerjob_";

    /// <summary>
    /// 第三方登录 票据缓存key.
    /// </summary>
    public const string PARAMS_JNPF_TICKET = "jnpf_ticket";

    /// <summary>
    /// Cas Key.
    /// </summary>
    public const string CAS_Ticket = "ticket";

    /// <summary>
    /// Code.
    /// </summary>
    public const string Code = "code";

    /// <summary>
    /// 外链密码开关(1：开 ,0：关).
    /// </summary>
    public const int OnlineDevData_State_Enable = 1;

    /// <summary>
    /// 门户日程缓存key.
    /// </summary>
    public const string CACHEKEYSCHEDULE = "jnpf:portal:schedule";

    /// <summary>
    /// 系统关键词.
    /// </summary>
    public const string SYSTEMKEY = "PUBLIC,STRING,VOID,BASE,THIS,USING,CLASS,STRUCT,ABSTRACT,INTERFACE,IS,AS,IN,OUT,REF,OBJECT,INT,DECIMAL,DOUBLE,FLOAT,BOOL,PRIVATE,DELEGATE,DEFAULT,DO,IF,ELSE,SWITCH,CASE,FOR,FOREACH,FALSE,FINALLY,FIXED,INTERNAL,NAMESPACE,OVERRIDE,RETURN,NULL,TRUE,WHILE,CONST,BREAK,CONTINUE,VIRTUAL,TENANTID,ID,FOREIGNID,FLOWID,FLOWTASKID,DELETEUSERID,DELETETIME,DELETEMARK,VERSION,TENANT_ID,FOREIGN_ID,FLOW_ID,FLOW_TASK_ID,DELETE_USER_ID,DELETE_TIME,DELETE_MARK,F_TENANT_ID,F_ID,F_FOREIGN_ID,F_FLOW_ID,F_FLOW_TASK_ID,F_DELETE_USER_ID,F_DELETE_TIME,F_DELETE_MARK,F_VERSION";

    /// <summary>
    /// 动态查询标识.
    /// </summary>
    public const string DYNAMICQUERYKEY = "@currentTime,@positionId,@userId,@userAndSubordinates,@organizeId,@organizationAndSuborganization,@depId,@depAndSubordinates,@branchManageOrganize";
}