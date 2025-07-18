﻿using JNPF.Common.Models.User;

namespace JNPF.Systems.Interfaces.Permission;

/// <summary>
/// 分级管理
/// 版 本：V3.2.5
/// 版 权：引迈信息技术有限公司（https://www.jnpfsoft.com）
/// 作 者：JNPF开发平台组
/// 日 期：2021.09.27.
/// </summary>
public interface IOrganizeAdministratorService
{
    /// <summary>
    /// 获取用户数据范围.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    Task<List<UserDataScopeModel>> GetUserDataScopeModel(string userId);
}