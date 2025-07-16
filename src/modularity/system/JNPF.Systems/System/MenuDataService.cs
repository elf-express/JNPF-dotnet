using JNPF.Common.Const;
using JNPF.Common.Core.Manager;
using JNPF.Common.Enums;
using JNPF.Common.Extension;
using JNPF.Common.Manager;
using JNPF.Common.Models.User;
using JNPF.Common.Security;
using JNPF.DependencyInjection;
using JNPF.DynamicApiController;
using JNPF.FriendlyException;
using JNPF.Systems.Entitys.Dto.System.MenuData;
using JNPF.Systems.Entitys.System;
using JNPF.Systems.Interfaces.System;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace JNPF.Systems.System;

/// <summary>
/// 常用功能.
/// </summary>
[ApiDescriptionSettings(Tag = "System", Name = "MenuData", Order = 800)]
[Route("api/System/[controller]")]
public class MenuDataService : IDynamicApiController, ITransient
{
    /// <summary>
    /// 服务基础仓储.
    /// </summary>
    private readonly ISqlSugarRepository<ModuleDataEntity> _repository;

    /// <summary>
    /// 用户管理.
    /// </summary>
    private readonly IUserManager _userManager;

    /// <summary>
    /// 菜单管理.
    /// </summary>
    private readonly IModuleService _moduleService;

    /// <summary>
    /// 缓存管理器.
    /// </summary>
    private readonly ICacheManager _cacheManager;

    /// <summary>
    /// 构造.
    /// </summary>
    /// <param name="repository"></param>
    /// <param name="userManager"></param>
    /// <param name="moduleService"></param>
    /// <param name="cacheManager"></param>
    public MenuDataService(
        ISqlSugarRepository<ModuleDataEntity> repository,
        IUserManager userManager,
        IModuleService moduleService,
        ICacheManager cacheManager)
    {
        _repository = repository;
        _userManager = userManager;
        _moduleService = moduleService;
        _cacheManager = cacheManager;
    }

    #region Get

    /// <summary>
    /// 常用数据.
    /// </summary>
    /// <param name="keyword"></param>
    /// <returns></returns>
    [HttpGet("")]
    public async Task<dynamic> GetList(string keyword)
    {
        var type = _userManager.UserOrigin.Equals("pc") ? "Web" : "App";
        var systemId = await GetSystemId(type);
        var list = await GetEntityList(type, systemId);

        var newList = new List<ModuleDataListOutput>();
        foreach (var item in list)
        {
            var module = await _repository.AsSugarClient().Queryable<ModuleEntity>()
                .Where(it => it.DeleteMark == null && it.EnabledMark == 1 && it.Id.Equals(item.ModuleId))
                .WhereIF(keyword.IsNotEmptyOrNull(), it => it.FullName.Contains(keyword))
                .FirstAsync();
            if (module.IsNotEmptyOrNull())
                newList.Add(module.Adapt<ModuleDataListOutput>());
        }

        var authorIds = (await _moduleService.GetUserModuleList(type, systemId)).Select(it => it.id).ToList();
        newList = newList.Where(it => authorIds.Contains(it.id)).ToList();

        return new { list = newList };
    }

    /// <summary>
    /// 常用数据列表.
    /// </summary>
    /// <param name="keyword"></param>
    /// <returns></returns>
    [HttpGet("getDataList")]
    public async Task<dynamic> GetDataList(string keyword)
    {
        var type = _userManager.UserOrigin.Equals("pc") ? "Web" : "App";
        var list = (await GetAppSecondMenuList(keyword, type)).Adapt<List<ModuleDataListAllOutput>>();
        foreach (var item in list)
        {
            item.isData = await _repository.IsAnyAsync(x => x.ModuleType == type && x.CreatorUserId == _userManager.UserId && x.ModuleId == item.id && x.DeleteMark == null);
        }

        // 删除无子级的目录
        list.RemoveAll(it => it.parentId.Equals("-1") && !list.Any(x => x.parentId.Equals(it.id)));

        var output = list.ToTree("-1");
        return new { list = output };
    }

    /// <summary>
    /// 更多App常用数据.
    /// </summary>
    /// <param name="keyword"></param>
    /// <returns></returns>
    [HttpGet("getAppDataList")]
    public async Task<dynamic> GetAppDataList(string keyword)
    {
        var type = _userManager.UserOrigin.Equals("pc") ? "Web" : "App";
        var systemId = await GetSystemId(type);

        var dataList = (await GetEntityList(type, systemId)).Select(it => it.ModuleId).ToList();
        var list = (await GetAppSecondMenuList(keyword, type)).Where(it => it.ParentId.Equals("-1") || (!it.ParentId.Equals("-1") && dataList.Contains(it.Id))).Adapt<List<ModuleDataListAllOutput>>();

        // 删除无子级的目录
        list.RemoveAll(it => it.parentId.Equals("-1") && !list.Any(x => x.parentId.Equals(it.id)));

        return new { list = list.ToTree("-1") };
    }

    #endregion

    #region Post

    /// <summary>
    /// 新增.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpPost("{id}")]
    public async Task Create(string id)
    {
        var type = _userManager.UserOrigin.Equals("pc") ? "Web" : "App";
        var systemId = await GetSystemId(type);

        if (await _repository.AsQueryable().AnyAsync(it => it.DeleteMark == null && it.ModuleType == type && it.SystemId == systemId && it.ModuleId == id && it.CreatorUserId == _userManager.UserId))
            throw Oops.Oh(ErrorCode.D2701);

        var entity = new ModuleDataEntity()
        {
            ModuleId = id,
            ModuleType = type,
            SystemId = systemId
        };
        int isOk = await _repository.AsInsertable(entity).IgnoreColumns(ignoreNullColumn: true).CallEntityMethod(m => m.Creator()).ExecuteCommandAsync();
        if (isOk < 1)
            throw Oops.Oh(ErrorCode.COM1000);
    }

    /// <summary>
    /// 删除.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    public async Task Delete(string id)
    {
        var type = _userManager.UserOrigin.Equals("pc") ? "Web" : "App";
        var systemId = await GetSystemId(type);

        var entity = await _repository.GetFirstAsync(x => x.ModuleId == id && x.ModuleType == type && x.SystemId == systemId && x.CreatorUserId == _userManager.UserId && x.DeleteMark == null);
        var isOk = await _repository.AsUpdateable(entity).CallEntityMethod(m => m.Delete()).UpdateColumns(it => new { it.DeleteMark, it.DeleteTime, it.DeleteUserId }).ExecuteCommandHasChangeAsync();
        if (!isOk)
            throw Oops.Oh(ErrorCode.COM1002);
    }

    #endregion

    #region PrivateMethod

    /// <summary>
    /// 列表.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="systemId"></param>
    /// <returns></returns>
    private async Task<List<ModuleDataEntity>> GetEntityList(string type, string systemId)
    {
        return await _repository.AsQueryable().Where(x => x.ModuleType == type && x.SystemId == systemId && x.CreatorUserId == _userManager.UserId && x.DeleteMark == null).OrderBy(a => a.CreatorTime).ToListAsync();
    }

    /// <summary>
    /// 获取系统id.
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private async Task<string> GetSystemId(string type)
    {
        if (type == "Web")
        {
            var cacheKey = string.Format("{0}:{1}", CommonConst.CACHEKEYONLINEUSER, _userManager.TenantId);
            var allUserOnlineList = await _cacheManager.GetAsync<List<UserOnlineModel>>(cacheKey);
            return allUserOnlineList.IsNotEmptyOrNull() ? allUserOnlineList.Find(it => it.token == _userManager.ToKen)?.systemId : _userManager.User.SystemId;
        }
        else
        {
            return _userManager.User.AppSystemId;
        }
    }

    /// <summary>
    /// 菜单列表（二级树）.
    /// </summary>
    /// <returns></returns>
    private async Task<List<ModuleEntity>> GetAppSecondMenuList(string keyword, string type)
    {
        var menuList = (await _moduleService.GetUserModuleList(type, string.Empty, keyword)).Where(it => !_userManager.CommonModuleEnCodeList.Contains(it.enCode)).Adapt<List<ModuleEntity>>();

        foreach (var item in menuList.Where(it => !it.Type.Equals(1)))
        {
            UpdateParentId(menuList, item);
        }

        menuList.RemoveAll(it => !it.ParentId.Equals("-1") && it.Type.Equals(1));

        return menuList;
    }

    /// <summary>
    /// 递归更改父级id（二级树）.
    /// </summary>
    /// <param name="moduleList"></param>
    /// <param name="module"></param>
    private void UpdateParentId(List<ModuleEntity> moduleList, ModuleEntity module)
    {
        var parent = moduleList.Find(it => it.Id.Equals(module.ParentId));
        if (parent != null && !parent.ParentId.Equals("-1"))
        {
            module.ParentId = parent.ParentId;
            UpdateParentId(moduleList, module);
        }
    }

    #endregion
}