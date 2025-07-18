﻿using JNPF.DependencyInjection;

namespace JNPF.Systems.Entitys.Dto.User;

/// <summary>
/// 用户数据导入 输入.
/// </summary>
[SuppressSniffer]
public class UserListImportDataInput
{
    /// <summary>
    /// Id 主键.
    /// </summary>
    public string id { get; set; }

    /// <summary>
    /// 性别.
    /// </summary>
    public string gender { get; set; }

    /// <summary>
    /// 启用标识 【0-停用、1-启用】.
    /// </summary>
    public string enabledMark { get; set; }

    /// <summary>
    /// 账号.
    /// </summary>
    public string account { get; set; }

    /// <summary>
    /// 姓名.
    /// </summary>
    public string realName { get; set; }

    /// <summary>
    /// 邮箱.
    /// </summary>
    public string email { get; set; }

    /// <summary>
    /// 所属组织.
    /// </summary>
    public string organizeId { get; set; }

    /// <summary>
    /// 直属主管/账号.
    /// </summary>
    public string managerId { get; set; }

    /// <summary>
    /// 岗位.
    /// </summary>
    public string positionId { get; set; }

    /// <summary>
    /// 角色.
    /// </summary>
    public string roleId { get; set; }

    /// <summary>
    /// 排序码.
    /// </summary>
    public string sortCode { get; set; }

    /// <summary>
    /// 描述.
    /// </summary>
    public string description { get; set; }

    /// <summary>
    /// 民族.
    /// </summary>
    public string nation { get; set; }

    /// <summary>
    /// 籍贯.
    /// </summary>
    public string nativePlace { get; set; }

    /// <summary>
    /// 入职日期.
    /// </summary>
    public string entryDate { get; set; }

    /// <summary>
    /// 证件类型.
    /// </summary>
    public string certificatesType { get; set; }

    /// <summary>
    /// 证件号码.
    /// </summary>
    public string certificatesNumber { get; set; }

    /// <summary>
    /// 文化程度.
    /// </summary>
    public string education { get; set; }

    /// <summary>
    /// 生日.
    /// </summary>
    public string birthday { get; set; }

    /// <summary>
    /// 电话.
    /// </summary>
    public string telePhone { get; set; }

    /// <summary>
    /// 固定电话
    /// </summary>
    public string landline { get; set; }

    /// <summary>
    /// 手机.
    /// </summary>
    public string mobilePhone { get; set; }

    /// <summary>
    /// 紧急联系人.
    /// </summary>
    public string urgentContacts { get; set; }

    /// <summary>
    /// 紧急联系电话.
    /// </summary>
    public string urgentTelePhone { get; set; }

    /// <summary>
    /// 通讯地址.
    /// </summary>
    public string postalAddress { get; set; }

    /// <summary>
    /// 职级.
    /// </summary>
    public string ranks { get; set; }

    /// <summary>
    /// 异常错误原因.
    /// </summary>
    public string errorsInfo { get; set; } = string.Empty;
}