using JNPF.Apps.Entitys.Dto;
using JNPF.Common.Core.Manager;
using JNPF.Common.Security;
using JNPF.DependencyInjection;
using JNPF.DynamicApiController;
using JNPF.Systems.Entitys.Permission;
using JNPF.Systems.Entitys.System;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace JNPF.Apps;

/// <summary>
/// App菜单
/// 版 本：V3.2
/// 版 权：引迈信息技术有限公司（https://www.jnpfsoft.com）
/// 作 者：JNPF开发平台组
/// 日 期：2021-06-01 .
/// </summary>
[ApiDescriptionSettings(Tag = "App", Name = "Menu", Order = 800)]
[Route("api/App/[controller]")]
public class AppMenuService : IDynamicApiController, ITransient
{
    /// <summary>
    /// 服务基础仓储.
    /// </summary>
    private readonly ISqlSugarRepository<ModuleEntity> _repository;

    /// <summary>
    /// 用户管理.
    /// </summary>
    private readonly IUserManager _userManager;

    /// <summary>
    /// 初始化一个<see cref="AppMenuService"/>类型的新实例.
    /// </summary>
    public AppMenuService(ISqlSugarRepository<ModuleEntity> repository, IUserManager userManager)
    {
        _repository = repository;
        _userManager = userManager;
    }

    #region Get

    /// <summary>
    /// 获取菜单列表.
    /// </summary>
    /// <returns></returns>
    [HttpGet("")]
    public async Task<dynamic> GetList(string keyword)
    {
        var list = (await GetAppMenuList(keyword)).Adapt<List<AppMenuListOutput>>();

        // 删除无子级的目录
        list.RemoveAll(it => it.parentId.Equals("-1") && !list.Any(x => x.parentId.Equals(it.id)));

        return new { list = list.ToTree("-1") };
    }

    /// <summary>
    /// 获取菜单的子菜单.
    /// </summary>
    /// <returns></returns>
    [HttpGet("getChildList/{id}")]
    public async Task<dynamic> GetChildList(string id)
    {
        var list = (await GetAppMenuList(string.Empty)).Adapt<List<AppMenuChildListOutput>>();

        list.RemoveAll(it => it.parentId.Equals("-1"));
        var data = list.Find(it => it.id.Equals(id));
        if (data != null) data.parentId = "-1";

        return list.ToTree("-1");
    }

    #endregion

    #region Private

    /// <summary>
    /// 菜单列表.
    /// </summary>
    /// <returns></returns>
    [NonAction]
    private async Task<List<ModuleEntity>> GetAppMenuList(string keyword)
    {
        var menuList = new List<ModuleEntity>();
        var userSystemId = _userManager.User.AppSystemId;
        var userStanding = _userManager.User.AppStanding;
        if (!_userManager.IsAdministrator || userStanding.Equals(2) || userStanding.Equals(3))
        {
            if (userStanding.Equals(3))
            {
                var pIds = _userManager.PermissionGroup;
                var mIdList = await _repository.AsSugarClient().Queryable<AuthorizeEntity>().Where(a => pIds.Contains(a.ObjectId)).Where(a => a.ItemType == "module").Select(a => a.ItemId).ToListAsync();

                // 当前系统的有权限的菜单
                menuList = await _repository.AsQueryable()
                    .Where(a => a.SystemId.Equals(userSystemId) && mIdList.Contains(a.Id) && a.EnabledMark == 1 && a.Category.Equals("App") && a.DeleteMark == null)
                    .WhereIF(!string.IsNullOrEmpty(keyword), x => x.FullName.Contains(keyword) || x.ParentId == "-1")
                    .OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc)
                    .OrderByIF(!string.IsNullOrEmpty(keyword), a => a.LastModifyTime, OrderByType.Desc).ToListAsync();
            }
            else if (userStanding.Equals(2))
            {
                // 获取所有分管系统Ids
                var dataScop = _userManager.DataScope;
                var objectIdList = dataScop.Where(x => x.organizeType != null && (x.organizeType.Equals("System") || x.organizeType.Equals("Module"))).Select(x => x.organizeId).ToList();

                // 当前系统在分管范围内
                if (objectIdList.Any(x => x.Equals(userSystemId)))
                {
                    if (await _repository.AsSugarClient().Queryable<SystemEntity>().AnyAsync(a => a.Id.Equals(userSystemId) && a.EnCode.Equals("mainSystem")))
                    {
                        // 当前系统的有分管权限的菜单
                        menuList = await _repository.AsQueryable()
                            .Where(a => a.SystemId.Equals(userSystemId) && objectIdList.Contains(a.Id) && a.EnabledMark == 1 && a.Category.Equals("App") && a.DeleteMark == null)
                            .WhereIF(!string.IsNullOrEmpty(keyword), x => x.FullName.Contains(keyword) || x.ParentId == "-1")
                            .OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc)
                            .OrderByIF(!string.IsNullOrEmpty(keyword), a => a.LastModifyTime, OrderByType.Desc).ToListAsync();
                    }
                    else
                    {
                        // 当前系统的所有菜单
                        menuList = await _repository.AsQueryable()
                            .Where(a => a.SystemId.Equals(userSystemId) && a.EnabledMark == 1 && a.Category.Equals("App") && a.DeleteMark == null)
                            .WhereIF(!string.IsNullOrEmpty(keyword), x => x.FullName.Contains(keyword) || x.ParentId == "-1")
                            .OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc)
                            .OrderByIF(!string.IsNullOrEmpty(keyword), a => a.LastModifyTime, OrderByType.Desc).ToListAsync();
                    }
                }
            }
        }
        else
        {
            menuList = await _repository.AsQueryable()
                .Where(a => a.SystemId.Equals(userSystemId) && a.EnabledMark == 1 && a.Category.Equals("App") && a.DeleteMark == null)
                .WhereIF(!string.IsNullOrEmpty(keyword), x => x.FullName.Contains(keyword) || x.ParentId == "-1")
                .OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc)
                .OrderByIF(!string.IsNullOrEmpty(keyword), a => a.LastModifyTime, OrderByType.Desc).ToListAsync();
        }

        return menuList;
    }

    #endregion
}