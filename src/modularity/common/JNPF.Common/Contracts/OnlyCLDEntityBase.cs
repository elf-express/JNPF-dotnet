﻿using JNPF.Common.Const;
using JNPF.Common.Security;
using JNPF.DependencyInjection;
using SqlSugar;

namespace JNPF.Common.Contracts;

/// <summary>
/// 创更删实体基类.
/// </summary>
[SuppressSniffer]
public abstract class OnlyCLDEntityBase : EntityBase<string>
{
    #region 必备字段

    /// <summary>
    /// 获取或设置 创建时间.
    /// </summary>
    [SugarColumn(ColumnName = "F_CREATOR_TIME", ColumnDescription = "创建时间")]
    public virtual DateTime? CreatorTime { get; set; }

    /// <summary>
    /// 获取或设置 创建用户.
    /// </summary>
    [SugarColumn(ColumnName = "F_CREATOR_USER_ID", ColumnDescription = "创建用户")]
    public virtual string CreatorUserId { get; set; }

    /// <summary>
    /// 获取或设置 修改时间.
    /// </summary>
    [SugarColumn(ColumnName = "F_LAST_MODIFY_TIME", ColumnDescription = "修改时间")]
    public virtual DateTime? LastModifyTime { get; set; }

    /// <summary>
    /// 获取或设置 修改用户.
    /// </summary>
    [SugarColumn(ColumnName = "F_LAST_MODIFY_USER_ID", ColumnDescription = "修改用户")]
    public virtual string LastModifyUserId { get; set; }

    /// <summary>
    /// 获取或设置 删除标志.
    /// </summary>
    [SugarColumn(ColumnName = "F_DELETE_MARK", ColumnDescription = "删除标志")]
    public virtual int? DeleteMark { get; set; }

    /// <summary>
    /// 获取或设置 删除时间.
    /// </summary>
    [SugarColumn(ColumnName = "F_DELETE_TIME", ColumnDescription = "删除时间")]
    public virtual DateTime? DeleteTime { get; set; }

    /// <summary>
    /// 获取或设置 删除用户.
    /// </summary>
    [SugarColumn(ColumnName = "F_DELETE_USER_ID", ColumnDescription = "删除用户")]
    public virtual string DeleteUserId { get; set; }

    #endregion

    /// <summary>
    /// 创建.
    /// </summary>
    public virtual void Creator()
    {
        var userId = App.User?.FindFirst(ClaimConst.CLAINMUSERID)?.Value;
        this.CreatorTime = DateTime.Now;
        this.Id = SnowflakeIdHelper.NextId();
        if (!string.IsNullOrEmpty(userId))
        {
            this.CreatorUserId = userId;
        }
    }

    /// <summary>
    /// 创建.
    /// </summary>
    public virtual void Create()
    {
        var userId = App.User?.FindFirst(ClaimConst.CLAINMUSERID)?.Value;
        this.CreatorTime = DateTime.Now;
        this.Id = this.Id == null ? SnowflakeIdHelper.NextId() : this.Id;
        if (!string.IsNullOrEmpty(userId))
        {
            this.CreatorUserId = CreatorUserId == null ? userId : CreatorUserId;
        }
    }

    /// <summary>
    /// 修改.
    /// </summary>
    public virtual void LastModify()
    {
        var userId = App.User?.FindFirst(ClaimConst.CLAINMUSERID)?.Value;
        this.LastModifyTime = DateTime.Now;
        if (!string.IsNullOrEmpty(userId))
        {
            this.LastModifyUserId = userId;
        }
    }

    /// <summary>
    /// 删除.
    /// </summary>
    public virtual void Delete()
    {
        var userId = App.User?.FindFirst(ClaimConst.CLAINMUSERID)?.Value;
        this.DeleteTime = DateTime.Now;
        this.DeleteMark = 1;
        if (!string.IsNullOrEmpty(userId))
        {
            this.DeleteUserId = userId;
        }
    }
}