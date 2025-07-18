﻿using JNPF.Common.Enums;
using JNPF.DependencyInjection;
using JNPF.JsonSerialization;
using JNPF.Systems.Entitys.Enum;
using Newtonsoft.Json;

namespace JNPF.Systems.Entitys.Model.SysConfig;

/// <summary>
/// 系统配置输出.
/// </summary>
[SuppressSniffer]
public class SysConfigModel
{
    /// <summary>
    /// 系统名称.
    /// </summary>
    public string sysName { get; set; }

    /// <summary>
    /// 系统描述.
    /// </summary>
    public string sysDescription { get; set; }

    /// <summary>
    /// 系统版本.
    /// </summary>
    public string sysVersion { get; set; }

    /// <summary>
    /// 版权信息.
    /// </summary>
    public string copyright { get; set; }

    /// <summary>
    /// 公司名称.
    /// </summary>
    public string companyName { get; set; }

    /// <summary>
    /// 公司简称.
    /// </summary>
    public string companyCode { get; set; }

    /// <summary>
    /// 公司地址.
    /// </summary>
    public string companyAddress { get; set; }

    /// <summary>
    /// 公司法人.
    /// </summary>
    public string companyContacts { get; set; }

    /// <summary>
    /// 公司电话.
    /// </summary>
    public string companyTelePhone { get; set; }

    /// <summary>
    /// 公司邮箱.
    /// </summary>
    public string companyEmail { get; set; }

    /// <summary>
    /// 单一登录方式（1：后登录踢出先登录 2：同时登录）.
    /// </summary>
    public LoginMethod singleLogin { get; set; }

    /// <summary>
    /// 超时登出.
    /// </summary>
    public long tokenTimeout { get; set; }

    /// <summary>
    /// 是否开启上次登录提醒.
    /// </summary>
    [JsonConverter(typeof(BoolJsonConverter))]
    public bool lastLoginTimeSwitch { get; set; }

    /// <summary>
    /// 是否开启白名单验证.
    /// </summary>
    [JsonConverter(typeof(BoolJsonConverter))]
    public bool whitelistSwitch { get; set; }

    /// <summary>
    /// 白名单.
    /// </summary>
    public string whiteListIp { get; set; }

    /// <summary>
    /// POP3服务主机地址.
    /// </summary>
    public string emailPop3Host { get; set; }

    /// <summary>
    /// POP3服务端口.
    /// </summary>
    public int emailPop3Port { get; set; }

    /// <summary>
    /// SMTP服务主机地址.
    /// </summary>
    public string emailSmtpHost { get; set; }

    /// <summary>
    /// SMTP服务主端口.
    /// </summary>
    public int emailSmtpPort { get; set; }

    /// <summary>
    /// 邮件显示名称.
    /// </summary>
    public string emailSenderName { get; set; }

    /// <summary>
    /// 邮箱账户.
    /// </summary>
    public string emailAccount { get; set; }

    /// <summary>
    /// 邮箱密码.
    /// </summary>
    public string emailPassword { get; set; }

    /// <summary>
    /// 是否开启SSL服务登录.
    /// </summary>
    [JsonConverter(typeof(BoolJsonConverter))]
    public bool emailSsl { get; set; }

    /// <summary>
    /// 授权密钥.
    /// </summary>
    public string registerKey { get; set; }

    /// <summary>
    /// 最后登录时间.
    /// </summary>
    public string lastLoginTime { get; set; }

    /// <summary>
    /// 分页数.
    /// </summary>
    public string pageSize { get; set; }

    /// <summary>
    /// 系统主题.
    /// </summary>
    public string sysTheme { get; set; }

    /// <summary>
    /// 是否开启日志.
    /// </summary>
    public string isLog { get; set; }

    /// <summary>
    /// 厂商.
    /// </summary>
    public string smsCompany { get; set; }

    /// <summary>
    /// 签名内容.
    /// </summary>
    public string smsSignName { get; set; }

    /// <summary>
    /// sms用户编号.
    /// </summary>
    public string smsKeyId { get; set; }

    /// <summary>
    /// sms密钥.
    /// </summary>
    public string smsKeySecret { get; set; }

    /// <summary>
    /// 模板编号.
    /// </summary>
    public string smsTemplateId { get; set; }

    /// <summary>
    /// 应用编号.
    /// </summary>
    public string smsAppId { get; set; }

    /// <summary>
    /// 企业号Id.
    /// </summary>
    public string qyhCorpId { get; set; }

    /// <summary>
    /// 应用凭证.
    /// </summary>
    public string qyhAgentId { get; set; }

    /// <summary>
    /// 凭证密钥.
    /// </summary>
    public string qyhAgentSecret { get; set; }

    /// <summary>
    /// 同步密钥.
    /// </summary>
    public string qyhCorpSecret { get; set; }

    /// <summary>
    /// 启用同步钉钉组织（0：不启用，1：启用）.
    /// </summary>
    [JsonConverter(typeof(BoolJsonConverter))]
    public bool qyhIsSynOrg { get; set; }

    /// <summary>
    /// 启用同步钉钉用户（0：不启用，1：启用）.
    /// </summary>
    [JsonConverter(typeof(BoolJsonConverter))]
    public bool qyhIsSynUser { get; set; }

    /// <summary>
    /// 企业号Id.
    /// </summary>
    public string dingSynAppKey { get; set; }

    /// <summary>
    /// 凭证密钥.
    /// </summary>
    public string dingSynAppSecret { get; set; }

    /// <summary>
    /// 应用凭证.
    /// </summary>
    public string dingAgentId { get; set; }

    /// <summary>
    /// 启用同步钉钉组织（0：不启用，1：启用）.
    /// </summary>
    [JsonConverter(typeof(BoolJsonConverter))]
    public bool dingSynIsSynOrg { get; set; }

    /// <summary>
    /// 启用同步钉钉用户（0：不启用，1：启用）.
    /// </summary>
    [JsonConverter(typeof(BoolJsonConverter))]
    public bool dingSynIsSynUser { get; set; }

    /// <summary>
    /// 错误密码次数.
    /// </summary>
    public int passwordErrorsNumber { get; set; } = 6;

    /// <summary>
    /// 错误策略（1：账号锁定，2：延时登录）.
    /// </summary>
    public ErrorStrategy lockType { get; set; }

    /// <summary>
    /// 延时登录时间(分钟).
    /// </summary>
    public int lockTime { get; set; } = 10;

    /// <summary>
    /// 是否开启验证码（0：不启用，1：启用）.
    /// </summary>
    [JsonConverter(typeof(BoolJsonConverter))]
    public bool enableVerificationCode { get; set; }

    /// <summary>
    /// 验证码位数.
    /// </summary>
    public int? verificationCodeNumber { get; set; }

    /// <summary>
    /// 访问域名.
    /// </summary>
    public string domain { get; set; } = "sms.tencentcloudapi.com";

    /// <summary>
    /// 支持地域.
    /// </summary>
    public string region { get; set; } = "ap-guangzhou";

    /// <summary>
    /// 短信版本.
    /// </summary>
    public string version { get; set; }

    /// <summary>
    /// 登录图标.
    /// </summary>
    public string loginIcon { get; set; }

    /// <summary>
    /// 导航图标.
    /// </summary>
    public string navigationIcon { get; set; }

    /// <summary>
    /// logo图标.
    /// </summary>
    public string logoIcon { get; set; }

    /// <summary>
    /// App图标.
    /// </summary>
    public string appIcon { get; set; }
}