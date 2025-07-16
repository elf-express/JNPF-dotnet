﻿using JNPF.Common.Const;
using JNPF.Common.Core.Manager;
using JNPF.Common.Core.Manager.Files;
using JNPF.Common.Enums;
using JNPF.Common.Extension;
using JNPF.Common.Filter;
using JNPF.Common.Manager;
using JNPF.Common.Models.User;
using JNPF.Common.Security;
using JNPF.DatabaseAccessor;
using JNPF.DependencyInjection;
using JNPF.DynamicApiController;
using JNPF.FriendlyException;
using JNPF.Systems.Entitys.Dto.System.Portal;
using JNPF.Systems.Entitys.Permission;
using JNPF.Systems.Entitys.System;
using JNPF.VisualDev.Entitys;
using JNPF.VisualDev.Entitys.Dto.Portal;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace JNPF.VisualDev;

/// <summary>
///  业务实现：门户设计.
/// </summary>
[ApiDescriptionSettings(Tag = "VisualDev", Name = "Portal", Order = 173)]
[Route("api/visualdev/[controller]")]
public class PortalService : IDynamicApiController, ITransient
{
    /// <summary>
    /// 服务基础仓储.
    /// </summary>
    private readonly ISqlSugarRepository<PortalEntity> _repository;

    /// <summary>
    /// 缓存管理器.
    /// </summary>
    private readonly ICacheManager _cacheManager;

    /// <summary>
    /// 用户管理.
    /// </summary>
    private readonly IUserManager _userManager;

    /// <summary>
    /// 文件服务.
    /// </summary>
    private readonly IFileManager _fileManager;

    /// <summary>
    /// 初始化一个<see cref="PortalService"/>类型的新实例.
    /// </summary>
    public PortalService(
        ISqlSugarRepository<PortalEntity> repository,
        IUserManager userManager,
        IFileManager fileManager,
        ICacheManager cacheManager)
    {
        _repository = repository;
        _userManager = userManager;
        _fileManager = fileManager;
        _cacheManager = cacheManager;
    }

    #region Get

    /// <summary>
    /// 列表.
    /// </summary>
    /// <param name="input">请求参数.</param>
    /// <returns>返回列表.</returns>
    [HttpGet("")]
    public async Task<dynamic> GetList([FromQuery] PortalListQueryInput input)
    {
        var data = await _repository.AsQueryable()
           .WhereIF(input.keyword.IsNotEmptyOrNull(), p => p.FullName.Contains(input.keyword) || p.EnCode.Contains(input.keyword))
           .WhereIF(input.category.IsNotEmptyOrNull(), p => p.Category == input.category)
           .WhereIF(input.type.IsNotEmptyOrNull(), p => p.Type == input.type)
           .WhereIF(input.enabledLock.IsNotEmptyOrNull(), p => p.EnabledLock == input.enabledLock)
           .WhereIF(input.isRelease.IsNotEmptyOrNull(), p => p.State == input.isRelease)
           .Where(p => p.DeleteMark == null)
           .OrderBy(p => p.SortCode)
           .OrderBy(p => p.CreatorTime, OrderByType.Desc)
           .OrderByIF(!input.keyword.IsNullOrEmpty(), p => p.LastModifyTime, OrderByType.Desc)
           .Select(p => new PortalListOutput
           {
               id = p.Id,
               fullName = p.FullName,
               enCode = p.EnCode,
               category = SqlFunc.Subqueryable<DictionaryDataEntity>().EnableTableFilter().Where(it => it.Id.Equals(p.Category)).Select(it => it.FullName),
               creatorTime = p.CreatorTime,
               creatorUser = SqlFunc.Subqueryable<UserEntity>().EnableTableFilter().Where(it => it.Id.Equals(p.CreatorUserId)).Select(it => SqlFunc.MergeString(it.RealName, "/", it.Account)),
               lastModifyTime = p.LastModifyTime,
               state = p.State,
               isRelease = p.State,
               enabledLock = p.EnabledLock,
               type = p.Type,
               sortCode = p.SortCode,
               platformRelease = p.PlatformRelease,
           })
           .ToPagedListAsync(input.currentPage, input.pageSize);

        return PageResult<PortalListOutput>.SqlSugarPageResult(data);
    }

    /// <summary>
    /// 获取门户侧边框列表.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Selector")]
    public async Task<dynamic> GetSelector([FromQuery] PortalSelectInput input)
    {
        List<PortalSelectOutput>? data = new List<PortalSelectOutput>();

        // 侧边栏需要系统id过滤
        if (input.type.IsNotEmptyOrNull() && input.type.Equals(1))
        {
            var cacheKey = string.Format("{0}:{1}", CommonConst.CACHEKEYONLINEUSER, _userManager.TenantId);
            var allUserOnlineList = await _cacheManager.GetAsync<List<UserOnlineModel>>(cacheKey);

            var sysId = string.Empty;
            if (_userManager.UserOrigin.Equals("pc"))
            {
                var userOnline = allUserOnlineList.Find(it => it.token.Equals(_userManager.ToKen));
                sysId = userOnline.IsNotEmptyOrNull() ? userOnline.systemId : _userManager.User.SystemId;
            }
            else
            {
                sysId = _userManager.User.AppSystemId;
            }

            if (_userManager.Standing == 3)
            {
                var permissionGroupIds = await _repository.AsSugarClient().Queryable<PermissionGroupEntity>().In(it => it.Id, _userManager.PermissionGroup).Where(it => it.EnabledMark == 1 && it.DeleteMark == null).Select(it => it.Id).ToListAsync();
                var items = await _repository.AsSugarClient().Queryable<AuthorizeEntity>().Where(a => permissionGroupIds.Contains(a.ObjectId) && a.ItemType == "portalManage").GroupBy(it => it.ItemId).Select(it => it.ItemId).ToListAsync();
                if (items.Any())
                {
                    data = await _repository.AsSugarClient().Queryable<PortalEntity, PortalManageEntity>((p, pm) => new JoinQueryInfos(JoinType.Left, p.Id == pm.PortalId))
                        .In((p, pm) => pm.Id, items.ToArray())
                        .Where((p, pm) => p.EnabledMark == 1 && p.DeleteMark == null && pm.EnabledMark == 1 && pm.DeleteMark == null)
                        .Where((p, pm) => pm.Platform.Equals(input.platform))
                        .Where((p, pm) => pm.SystemId.Equals(sysId))
                        .OrderBy((p, pm) => pm.SortCode)
                        .OrderBy((p, pm) => pm.CreatorTime, OrderByType.Desc)
                        .OrderBy((p, pm) => pm.LastModifyTime, OrderByType.Desc)
                        .Select(p => new PortalSelectOutput
                        {
                            id = p.Id,
                            fullName = p.FullName,
                            parentId = p.Category
                        }).ToListAsync();
                }
            }
            else
            {
                data = await _repository.AsSugarClient().Queryable<PortalEntity, PortalManageEntity>((p, pm) => new JoinQueryInfos(JoinType.Left, p.Id == pm.PortalId))
                    .Where((p, pm) => p.EnabledMark == 1 && p.DeleteMark == null && pm.EnabledMark == 1 && pm.DeleteMark == null)
                    .Where((p, pm) => pm.Platform.Equals(input.platform))
                    .Where((p, pm) => pm.SystemId.Equals(sysId))
                    .OrderBy((p, pm) => pm.SortCode)
                    .OrderBy((p, pm) => pm.CreatorTime, OrderByType.Desc)
                    .OrderBy((p, pm) => pm.LastModifyTime, OrderByType.Desc)
                    .Select(p => new PortalSelectOutput
                    {
                        id = p.Id,
                        fullName = p.FullName,
                        parentId = p.Category
                    }).ToListAsync();
            }
        }
        else
        {
            data = await _repository.AsQueryable()
                .Where(it => it.DeleteMark == null && it.EnabledMark == 1)
                .OrderBy(it => it.SortCode)
                .OrderBy(it => it.CreatorTime, OrderByType.Desc)
                .OrderBy(it => it.LastModifyTime, OrderByType.Desc)
                .Select(it => new PortalSelectOutput
                {
                    id = it.Id,
                    fullName = it.FullName,
                    parentId = it.Category
                })
                .ToListAsync();
        }

        List<string>? parentIds = data.Select(it => it.parentId).Distinct().ToList();
        List<PortalSelectOutput>? treeList = new List<PortalSelectOutput>();
        if (parentIds.Any())
        {
            treeList = await _repository.AsSugarClient().Queryable<DictionaryDataEntity>().In(it => it.Id, parentIds.ToArray())
                .Where(d => d.DeleteMark == null && d.EnabledMark == 1)
                .OrderBy(o => o.SortCode)
                .OrderBy(o => o.CreatorTime, OrderByType.Desc)
                .Select(d => new PortalSelectOutput
                {
                    id = d.Id,
                    parentId = "0",
                    fullName = d.FullName
                }).ToListAsync();
        }

        return new { list = treeList.Union(data).ToList().ToTree("0") };
    }

    /// <summary>
    /// 门户获取系统下拉.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="category"></param>
    /// <returns></returns>
    [HttpGet("systemFilter/{id}")]
    public async Task<dynamic> GetSystemFilter(string id, string category)
    {
        var pmSysemIdList = await _repository.AsSugarClient().Queryable<PortalManageEntity>().Where(it => it.DeleteMark == null && it.Platform.Equals(category) && it.PortalId.Equals(id)).Select(it => it.SystemId).ToListAsync();
        var systemList = (await _repository.AsSugarClient().Queryable<SystemEntity>().Where(it => it.DeleteMark == null && !it.EnCode.Equals("mainSystem")).OrderBy(it => it.SortCode).OrderByDescending(it => it.CreatorTime).ToListAsync()).Adapt<List<PortalSystemFilterOutput>>();

        foreach (var item in systemList)
        {
            if (pmSysemIdList.Contains(item.id))
                item.disabled = true;
        }

        return new { list = systemList };
    }

    /// <summary>
    /// 获取门户信息.
    /// </summary>
    /// <param name="id">主键id.</param>
    /// <returns></returns>
    [HttpGet("{id}")]
    public async Task<dynamic> GetInfo(string id)
    {
        var systemList = await _repository.AsSugarClient().Queryable<SystemEntity>().Where(it => it.DeleteMark == null).ToListAsync();

        var data = await _repository.AsQueryable()
            .Where(it => it.DeleteMark == null && it.Id == id)
            .Select(it => new PortalInfoOutput()
            {
                id = it.Id,
                category = it.Category,
                customUrl = it.CustomUrl,
                description = it.Description,
                enCode = it.EnCode,
                enabledLock = it.EnabledLock,
                fullName = it.FullName,
                linkType = it.LinkType,
                sortCode = it.SortCode,
                type = it.Type,
                formData = SqlFunc.Subqueryable<PortalDataEntity>().EnableTableFilter().Where(x => x.DeleteMark == null && x.Type.Equals("model") && x.PortalId.Equals(id)).Select(x => x.FormData),
                platformRelease = it.PlatformRelease,
                pcIsRelease = SqlFunc.Subqueryable<ModuleEntity>().EnableTableFilter().Where(x => x.DeleteMark == null && systemList.Select(s => s.Id).Contains(x.SystemId) && x.Category.Equals("Web") && x.PropertyJson.Contains(it.Id)).Any() ? 1 : 0,
                appIsRelease = SqlFunc.Subqueryable<ModuleEntity>().EnableTableFilter().Where(x => x.DeleteMark == null && systemList.Select(s => s.Id).Contains(x.SystemId) && x.Category.Equals("App") && x.PropertyJson.Contains(it.Id)).Any() ? 1 : 0,
                pcPortalIsRelease = SqlFunc.Subqueryable<PortalManageEntity>().EnableTableFilter().Where(x => x.DeleteMark == null && systemList.Select(s => s.Id).Contains(x.SystemId) && x.Platform.Equals("Web") && x.PortalId.Equals(it.Id)).Any() ? 1 : 0,
                appPortalIsRelease = SqlFunc.Subqueryable<PortalManageEntity>().EnableTableFilter().Where(x => x.DeleteMark == null && systemList.Select(s => s.Id).Contains(x.SystemId) && x.Platform.Equals("App") && x.PortalId.Equals(it.Id)).Any() ? 1 : 0
            })
            .FirstAsync();

        var moduleList = await _repository.AsSugarClient().Queryable<ModuleEntity>().Where(it => it.DeleteMark == null).ToListAsync();
        var pcModuleList = moduleList.Where(it => it.Category.Equals("Web")).ToList();
        var appModuleList = moduleList.Where(it => it.Category.Equals("App")).ToList();
        var portalManageList = await _repository.AsSugarClient().Queryable<PortalManageEntity>().Where(it => it.DeleteMark == null).ToListAsync();

        var pcList = new List<string>();
        foreach (var module in pcModuleList.Where(it => it.PropertyJson.Contains(data.id)))
        {
            GetReleaseName(pcList, pcModuleList, systemList, module, string.Empty);
        }
        data.pcReleaseName = string.Join("；", pcList);

        var appList = new List<string>();
        foreach (var module in appModuleList.Where(it => it.PropertyJson.Contains(data.id)))
        {
            GetReleaseName(appList, appModuleList, systemList, module, string.Empty);
        }
        data.appReleaseName = string.Join("；", appList);

        var pcPortalList = new List<string>();
        foreach (var pm in portalManageList.Where(it => it.Platform.Equals("Web") && it.PortalId.Equals(data.id)))
        {
            var sys = systemList.Find(it => it.Id.Equals(pm.SystemId));
            if (sys.IsNotEmptyOrNull())
            {
                pcPortalList.Add(sys.FullName);
            }
        }
        data.pcPortalReleaseName = string.Join("；", pcPortalList);

        var appPortalList = new List<string>();
        foreach (var pm in portalManageList.Where(it => it.Platform.Equals("App") && it.PortalId.Equals(data.id)))
        {
            var sys = systemList.Find(it => it.Id.Equals(pm.SystemId));
            if (sys.IsNotEmptyOrNull())
            {
                appPortalList.Add(sys.FullName);
            }
        }
        data.appPortalReleaseName = string.Join("；", appPortalList);

        return data;
    }

    /// <summary>
    /// 信息.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpGet("{id}/auth")]
    public async Task<dynamic> GetInfoAuth(string id, [FromQuery] PortalAuthInput input)
    {
        var systemId = input.platform.Equals("App") ? _userManager.User.AppSystemId : _userManager.User.SystemId;

        if (!await _repository.AsQueryable().AnyAsync(it => it.DeleteMark == null && it.Id.Equals(id)))
            throw Oops.Oh(ErrorCode.D1919);

        var entity = new PortalInfoAuthOutput();
        if (_userManager.Standing == 3)
        {
            var permissionGroupIds = await _repository.AsSugarClient().Queryable<PermissionGroupEntity>()
                .Where(it => _userManager.PermissionGroup.Contains(it.Id))
                .Where(it => it.EnabledMark == 1 && it.DeleteMark == null)
                .Select(it => it.Id)
                .ToListAsync();
            var items = await _repository.AsSugarClient().Queryable<AuthorizeEntity>()
                .Where(a => permissionGroupIds.Contains(a.ObjectId))
                .Where(a => a.ItemType == "portalManage")
                .GroupBy(it => it.ItemId)
                .Select(it => it.ItemId)
                .ToListAsync();
            if (items.Count == 0) return null;

            var portalIdList = await _repository.AsSugarClient().Queryable<PortalManageEntity>()
                .Where(it => it.DeleteMark == null && it.EnabledMark == 1 && items.Contains(it.Id) && it.SystemId.Equals(systemId))
                .Select(it => it.PortalId)
                .ToListAsync();
            if (portalIdList.Contains(id))
            {
                // 判断子门户是否存在
                if (!await _repository.AsSugarClient().Queryable<PortalDataEntity>().AnyAsync(it => it.DeleteMark == null && it.Platform.Equals(input.platform) && it.Type.Equals("custom") && it.SystemId.Equals(systemId) && it.PortalId.Equals(id) && it.CreatorUserId.Equals(_userManager.UserId)))
                {
                    var data = await _repository.AsSugarClient().Queryable<PortalDataEntity>()
                        .Where(it => it.DeleteMark == null && it.Type.Equals("release") && it.PortalId.Equals(id) && it.SystemId == null)
                        .FirstAsync();
                    if (data.IsNotEmptyOrNull())
                    {
                        data.Id = SnowflakeIdHelper.NextId();
                        data.Type = "custom";
                        data.Platform = input.platform;
                        data.SystemId = systemId;
                        data.CreatorTime = DateTime.Now;
                        data.CreatorUserId = _userManager.UserId;
                        await _repository.AsSugarClient().Insertable(data).ExecuteCommandAsync();
                    }
                }

                entity = await _repository.AsQueryable()
                    .Where(it => it.DeleteMark == null && it.EnabledMark == 1 && it.Id.Equals(id))
                    .Select(it => new PortalInfoAuthOutput
                    {
                        type = it.Type,
                        customUrl = it.CustomUrl,
                        linkType = it.LinkType,
                        enabledLock = it.EnabledLock,
                        formData = SqlFunc.Subqueryable<PortalDataEntity>().EnableTableFilter().Where(x => x.DeleteMark == null && x.CreatorUserId.Equals(_userManager.UserId) && x.Platform.Equals(input.platform) && x.Type.Equals("custom") && x.PortalId.Equals(id) && x.SystemId.Equals(systemId)).Select(x => x.FormData)
                    })
                    .FirstAsync();
            }
        }
        else
        {
            // 判断子门户是否存在
            if (!await _repository.AsSugarClient().Queryable<PortalDataEntity>().AnyAsync(it => it.DeleteMark == null && it.Platform.Equals(input.platform) && it.Type.Equals("custom") && it.SystemId.Equals(systemId) && it.PortalId.Equals(id) && it.CreatorUserId.Equals(_userManager.UserId)))
            {
                var data = await _repository.AsSugarClient().Queryable<PortalDataEntity>()
                    .Where(it => it.DeleteMark == null && it.Type.Equals("release") && it.PortalId.Equals(id) && it.SystemId == null)
                    .FirstAsync();
                if (data.IsNotEmptyOrNull())
                {
                    data.Id = SnowflakeIdHelper.NextId();
                    data.Type = "custom";
                    data.Platform = input.platform;
                    data.SystemId = systemId;
                    data.CreatorTime = DateTime.Now;
                    data.CreatorUserId = _userManager.UserId;
                    await _repository.AsSugarClient().Insertable(data).ExecuteCommandAsync();
                }
            }

            entity = await _repository.AsQueryable()
                .Where(it => it.DeleteMark == null && it.EnabledMark == 1 && it.Id.Equals(id))
                .Select(it => new PortalInfoAuthOutput
                {
                    type = it.Type,
                    customUrl = it.CustomUrl,
                    linkType = it.LinkType,
                    enabledLock = it.EnabledLock,
                    formData = SqlFunc.Subqueryable<PortalDataEntity>().EnableTableFilter().Where(x => x.DeleteMark == null && x.CreatorUserId.Equals(_userManager.UserId) && x.Platform.Equals(input.platform) && x.Type.Equals("custom") && x.PortalId.Equals(id) && x.SystemId.Equals(systemId)).Select(x => x.FormData)
                })
                .FirstAsync();
        }

        return entity;
    }

    /// <summary>
    /// 门户选择.
    /// </summary>
    /// <param name="systemId"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpGet("manage/Selector/{systemId}")]
    public async Task<dynamic> GetManageSelector(string systemId, [FromQuery] PortalManageInput input)
    {
        // 系统已添加的门户
        var portalManageIdList = await _repository.AsSugarClient().Queryable<PortalManageEntity>()
            .Where(it => it.DeleteMark == null && it.SystemId.Equals(systemId) && it.Platform.Equals(input.platform))
            .Select(it => it.PortalId)
            .ToListAsync();

        var portalList = await _repository.AsSugarClient().Queryable<PortalEntity, DictionaryDataEntity>((p, d) => new JoinQueryInfos(JoinType.Left, p.Category == d.Id))
            .Where(p => p.EnabledMark == 1 && p.DeleteMark == null && !portalManageIdList.Contains(p.Id))
            .WhereIF(!string.IsNullOrEmpty(input.keyword), p => p.FullName.Contains(input.keyword) || p.EnCode.Contains(input.keyword))
            .OrderBy(p => p.SortCode)
            .OrderBy(p => p.CreatorTime, OrderByType.Desc)
            .OrderBy(p => p.LastModifyTime, OrderByType.Desc)
            .Select((p, d) => new PortalManageOutput
            {
                id = p.Id,
                fullName = p.FullName,
                enCode = p.EnCode,
                type = p.Type,
                sortCode = p.SortCode,
                category = d.FullName,
                categoryId = p.Category,
                categoryName = d.FullName
            })
            .ToPagedListAsync(input.currentPage, input.pageSize);

        return PageResult<PortalManageOutput>.SqlSugarPageResult(portalList);
    }

    #endregion

    #region Post

    /// <summary>
    /// 门户导出.
    /// </summary>
    /// <param name="modelId"></param>
    /// <returns></returns>
    [HttpPost("{modelId}/Actions/Export")]
    public async Task<dynamic> ActionsExportData(string modelId)
    {
        // 模板实体
        var templateEntity = await _repository.AsQueryable()
            .Where(it => it.Id == modelId)
            .Select<PortalExportOutput>()
            .FirstAsync();
        templateEntity.formData = await _repository.AsSugarClient().Queryable<PortalDataEntity>()
            .Where(it => it.DeleteMark == null && it.PortalId.Equals(modelId) && it.Type.Equals("model"))
            .Select(it => it.FormData)
            .FirstAsync();

        string? jsonStr = templateEntity.ToJsonString();
        return await _fileManager.Export(jsonStr, templateEntity.fullName, ExportFileType.vp);
    }

    /// <summary>
    /// 门户导入.
    /// </summary>
    /// <param name="file"></param>
    /// <param name="type">识别重复（0：跳过，1：追加）.</param>
    /// <returns></returns>
    [HttpPost("Actions/Import")]
    public async Task ActionsImportData(IFormFile file, int type)
    {
        string? fileType = Path.GetExtension(file.FileName).Replace(".", string.Empty);
        if (!fileType.ToLower().Equals(ExportFileType.vp.ToString())) throw Oops.Oh(ErrorCode.D3006);
        string? josn = _fileManager.Import(file);
        PortalExportOutput? templateEntity;
        try
        {
            templateEntity = josn.ToObject<PortalExportOutput>();
        }
        catch
        {
            throw Oops.Oh(ErrorCode.D3006);
        }
        if (templateEntity == null) throw Oops.Oh(ErrorCode.D3006);
        if (templateEntity != null && templateEntity.formData.IsNotEmptyOrNull() && templateEntity.formData.IndexOf("layoutId") <= 0)
            throw Oops.Oh(ErrorCode.D3006);

        var errorMsgList = new List<string>();
        var errorList = new List<string>();
        if (await _repository.AsQueryable().AnyAsync(it => it.DeleteMark == null && it.Id.Equals(templateEntity.id))) errorList.Add("ID");
        if (await _repository.AsQueryable().AnyAsync(it => it.DeleteMark == null && it.EnCode.Equals(templateEntity.enCode))) errorList.Add("编码");
        if (await _repository.AsQueryable().AnyAsync(it => it.DeleteMark == null && it.FullName.Equals(templateEntity.fullName))) errorList.Add("名称");

        if (errorList.Any())
        {
            if (type.Equals(0))
            {
                var error = string.Join("、", errorList);
                errorMsgList.Add(string.Format("{0}重复", error));
            }
            else
            {
                var random = new Random().NextLetterAndNumberString(5);
                templateEntity.id = SnowflakeIdHelper.NextId();
                templateEntity.fullName = string.Format("{0}.副本{1}", templateEntity.fullName, random);
                templateEntity.enCode += random;
            }
        }
        if (errorMsgList.Any() && type.Equals(0)) throw Oops.Oh(ErrorCode.COM1018, string.Join(";", errorMsgList));

        var portalEntity = templateEntity.Adapt<PortalEntity>();
        portalEntity.State = 0;
        portalEntity.EnabledMark = 0;
        portalEntity.CreatorTime = DateTime.Now;
        portalEntity.CreatorUserId = _userManager.UserId;
        try
        {
            var storModuleModel = _repository.AsSugarClient().Storageable(portalEntity).WhereColumns(it => it.Id).Saveable().ToStorage(); // 存在更新不存在插入 根据主键
            await storModuleModel.AsInsertable.ExecuteCommandAsync(); // 执行插入
            await storModuleModel.AsUpdateable.ExecuteCommandAsync(); // 执行更新
        }
        catch (Exception ex)
        {
            throw Oops.Oh(ErrorCode.COM1020, ex.Message);
        }

        // 门户数据
        var dataEntity = new PortalDataEntity()
        {
            Id = SnowflakeIdHelper.NextId(),
            PortalId = portalEntity.Id,
            FormData = templateEntity.formData,
            Type = "model",
            CreatorTime = DateTime.Now,
            CreatorUserId = _userManager.UserId
        };
        try
        {
            await _repository.AsSugarClient().Insertable(dataEntity).ExecuteCommandAsync();
        }
        catch (Exception ex)
        {
            throw Oops.Oh(ErrorCode.COM1020, ex.Message);
        }
    }

    /// <summary>
    /// 新建门户信息.
    /// </summary>
    /// <param name="input">实体对象.</param>
    /// <returns></returns>
    [HttpPost("")]
    public async Task<string> Create([FromBody] PortalCrInput input)
    {
        var entity = input.Adapt<PortalEntity>();
        entity.Id = SnowflakeIdHelper.NextId();
        entity.State = 0;
        entity.EnabledMark = 0;
        entity.CreatorTime = DateTime.Now;
        entity.CreatorUserId = _userManager.UserId;
        if (entity.Type.Equals(1))
        {
            entity.EnabledLock = null;
        }

        if (string.IsNullOrEmpty(entity.Category)) throw Oops.Oh(ErrorCode.D1901);
        else if (string.IsNullOrEmpty(entity.FullName)) throw Oops.Oh(ErrorCode.D1902);
        else if (string.IsNullOrEmpty(entity.EnCode)) throw Oops.Oh(ErrorCode.D1903);
        else if (await _repository.AsQueryable().Where(it => it.DeleteMark == null && it.FullName.Equals(input.fullName)).AnyAsync())
            throw Oops.Oh(ErrorCode.D1915);
        else if (await _repository.AsQueryable().Where(it => it.DeleteMark == null && it.EnCode.Equals(input.enCode)).AnyAsync())
            throw Oops.Oh(ErrorCode.D1916);
        else await _repository.AsInsertable(entity).ExecuteCommandAsync();

        var dataEntity = new PortalDataEntity()
        {
            Id = SnowflakeIdHelper.NextId(),
            PortalId = entity.Id,
            FormData = input.formData,
            CreatorTime = DateTime.Now,
            CreatorUserId = _userManager.UserId,
            Type = "model"
        };
        var isOk = await _repository.AsSugarClient().Insertable(dataEntity).ExecuteCommandAsync();
        if (!(isOk > 0))
            throw Oops.Oh(ErrorCode.COM1000);

        // 确定并设计
        return entity.Id;
    }

    /// <summary>
    /// 修改接口.
    /// </summary>
    /// <param name="id">主键id.</param>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPut("{id}")]
    public async Task Update(string id, [FromBody] PortalUpInput input)
    {
        var entity = input.Adapt<PortalEntity>();
        if (await _repository.AsQueryable().Where(it => it.DeleteMark == null && !it.Id.Equals(id) && it.FullName.Equals(input.fullName)).AnyAsync())
            throw Oops.Oh(ErrorCode.D1915);
        else if (await _repository.AsQueryable().Where(it => it.DeleteMark == null && !it.Id.Equals(id) && it.EnCode.Equals(input.enCode)).AnyAsync())
            throw Oops.Oh(ErrorCode.D1916);
        if (entity.Type.Equals(1))
        {
            entity.EnabledLock = null;
        }

        var state = await _repository.AsQueryable()
            .Where(it => it.DeleteMark == null && it.Id.Equals(id))
            .Select(it => it.State)
            .FirstAsync();
        if (state.Equals(1))
            entity.State = 2;

        entity.LastModifyTime = DateTime.Now;
        entity.LastModifyUserId = _userManager.UserId;

        int isOk = await _repository.AsSugarClient().Updateable(entity)
            .UpdateColumns(it => new {
                it.CustomUrl,
                it.EnCode,
                it.LinkType,
                it.SortCode,
                it.FullName,
                it.Description,
                it.Type,
                it.Category,
                it.State,
                it.EnabledLock,
                it.LastModifyTime,
                it.LastModifyUserId
            }).IgnoreColumns(ignoreAllNullColumns: true).ExecuteCommandAsync();

        if (!(isOk > 0))
            throw Oops.Oh(ErrorCode.COM1001);

        if (input.formData.IsNotEmptyOrNull())
        {
            await _repository.AsSugarClient().Updateable<PortalDataEntity>()
                .Where(it => it.DeleteMark == null && it.PortalId.Equals(id) && it.Type.Equals("model"))
                .SetColumns(it => new PortalDataEntity()
                {
                    FormData = input.formData,
                    LastModifyTime = SqlFunc.GetDate(),
                    LastModifyUserId = _userManager.UserId
                })
                .ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// 删除接口.
    /// </summary>
    /// <param name="id">主键id.</param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    public async Task Delete(string id)
    {
        var entity = await _repository.AsQueryable()
            .Where(it => it.Id == id && it.DeleteMark == null)
            .FirstAsync();
        _ = entity ?? throw Oops.Oh(ErrorCode.COM1005);

        var entityLinkSystemId = await _repository.AsSugarClient().Queryable<PortalManageEntity>()
            .Where(it => it.DeleteMark == null && it.PortalId.Equals(id))
            .Select(it => it.SystemId)
            .FirstAsync();
        var systemName = await _repository.AsSugarClient().Queryable<SystemEntity>()
            .Where(it => it.DeleteMark == null && it.Id.Equals(entityLinkSystemId))
            .Select(it => it.FullName)
            .FirstAsync();
        if (entityLinkSystemId.IsNotEmptyOrNull() && systemName.IsNotEmptyOrNull())
            throw Oops.Oh(ErrorCode.D1917, systemName);

        entity.DeleteMark = 1;
        entity.DeleteTime = DateTime.Now;
        entity.DeleteUserId = _userManager.UserId;
        await _repository.AsSugarClient().Updateable(entity).UpdateColumns(it => new { it.DeleteMark, it.DeleteTime, it.DeleteUserId }).ExecuteCommandAsync();

        var isOk = await _repository.AsSugarClient().Updateable<PortalDataEntity>()
            .Where(it => it.DeleteMark == null && it.PortalId.Equals(id))
            .SetColumns(it => new PortalDataEntity()
            {
                DeleteMark = 1,
                DeleteTime = SqlFunc.GetDate(),
                DeleteUserId = _userManager.UserId
            })
            .ExecuteCommandAsync();
        if (!(isOk > 0)) throw Oops.Oh(ErrorCode.COM1002);
    }

    /// <summary>
    /// 复制.
    /// </summary>
    /// <param name="id">主键值.</param>
    /// <returns></returns>
    [HttpPost("{id}/Actions/Copy")]
    public async Task ActionsCopy(string id)
    {
        string? random = new Random().NextLetterAndNumberString(5);
        var entity = await _repository.AsQueryable()
            .Where(it => it.Id == id && it.DeleteMark == null)
            .FirstAsync();
        var newEntity = new PortalEntity()
        {
            Id = SnowflakeIdHelper.NextId(),
            CreatorTime = DateTime.Now,
            CreatorUserId = _userManager.UserId,
            FullName = entity.FullName + ".副本" + random,
            EnCode = entity.EnCode + random,
            Category = entity.Category,
            Description = entity.Description,
            EnabledMark = 0,
            EnabledLock = entity.EnabledLock,
            State = 0,
            SortCode = entity.SortCode,
            Type = entity.Type,
            LinkType = entity.LinkType,
            CustomUrl = entity.CustomUrl,
        };

        var dataEntity = await _repository.AsSugarClient().Queryable<PortalDataEntity>()
            .Where(it => it.DeleteMark == null && it.PortalId.Equals(id) && it.Type.Equals("model"))
            .FirstAsync();
        var newDataEntity = new PortalDataEntity()
        {
            Id = SnowflakeIdHelper.NextId(),
            CreatorTime = DateTime.Now,
            CreatorUserId = _userManager.UserId,
            PortalId = newEntity.Id,
            FormData = dataEntity.FormData,
            Type = "model"
        };

        try
        {
            await _repository.AsSugarClient().Insertable(newEntity).ExecuteCommandAsync();
            await _repository.AsSugarClient().Insertable(newDataEntity).ExecuteCommandAsync();
        }
        catch
        {
            if (entity.FullName.Length >= 100 || entity.EnCode.Length >= 50) throw Oops.Oh(ErrorCode.COM1009); // 数据长度超过 字段设定长度
            else throw;
        }
    }

    /// <summary>
    /// 设置默认门户.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="platform"></param>
    /// <returns></returns>
    [HttpPut("{id}/Actions/SetDefault")]
    public async Task SetDefault(string id, string platform)
    {
        var portalDic = (await _repository.AsSugarClient().Queryable<UserEntity>()
            .Where(it => it.Id.Equals(_userManager.UserId))
            .Select(it => it.PortalId)
            .FirstAsync())
            .ToObject<Dictionary<string, string>>();

        var cacheKey = string.Format("{0}:{1}", CommonConst.CACHEKEYONLINEUSER, _userManager.TenantId);
        var allUserOnlineList = await _cacheManager.GetAsync<List<UserOnlineModel>>(cacheKey);

        var curSystemId = string.Empty;
        if (_userManager.UserOrigin.Equals("pc"))
        {
            var userOnline = allUserOnlineList.Find(it => it.token.Equals(_userManager.ToKen));
            curSystemId = userOnline.IsNotEmptyOrNull() ? userOnline.systemId : _userManager.User.SystemId;
        }
        else
        {
            curSystemId = _userManager.User.AppSystemId;
        }

        var key = string.Format("{0}:{1}", platform, curSystemId);
        if (portalDic.ContainsKey(key))
        {
            portalDic[key] = id;
        }
        else
        {
            portalDic.Add(key, id);
        }

        var portalId = portalDic.ToJsonString();
        var isOk = await _repository.AsSugarClient().Updateable<UserEntity>()
            .Where(it => it.Id.Equals(_userManager.UserId))
            .SetColumns(it => new UserEntity()
            {
                PortalId = portalId,
                LastModifyTime = SqlFunc.GetDate(),
                LastModifyUserId = _userManager.UserId
            })
            .ExecuteCommandAsync();
        if (!(isOk > 0))
            throw Oops.Oh(ErrorCode.D5014);
    }

    /// <summary>
    /// 实时保存门户.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpPut("Custom/Save/{id}")]
    public async Task SavePortal(string id, [FromBody] PortalSaveInput input)
    {
        // 修改子门户的数据
        var isOk = await _repository.AsSugarClient().Updateable<PortalDataEntity>()
            .Where(it => it.DeleteMark == null && it.Platform == "Web" && it.Type.Equals("custom") && it.PortalId.Equals(id) && it.SystemId.Equals(_userManager.User.SystemId) && it.CreatorUserId.Equals(_userManager.UserId))
            .SetColumns(it => new PortalDataEntity()
            {
                FormData = input.formData,
                LastModifyTime = SqlFunc.GetDate(),
                LastModifyUserId = _userManager.UserId
            })
            .ExecuteCommandAsync();
        if (!(isOk > 0))
            throw Oops.Oh(ErrorCode.D1906);
    }

    /// <summary>
    /// 同步门户.
    /// </summary>
    /// <param name="id"></param>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpPut("Actions/release/{id}")]
    [UnitOfWork]
    public async Task SyncPortal(string id, [FromBody] PortalSyncInput input)
    {
        var entity = await _repository.AsQueryable().FirstAsync(it => it.DeleteMark == null && it.Id.Equals(id));
        var sysIdList = await _repository.AsSugarClient().Queryable<SystemEntity>().Where(it => it.DeleteMark == null).Select(it => it.Id).ToListAsync();
        List<PortalDataEntity> portalDataList = new List<PortalDataEntity>();
        List<PortalManageEntity> portalManageList = new List<PortalManageEntity>();

        // 主门户数据
        var modelData = await _repository.AsSugarClient().Queryable<PortalDataEntity>()
            .Where(it => it.DeleteMark == null && it.Type.Equals("model") && it.PortalId.Equals(id))
            .FirstAsync();

        // 发布门户数据
        var releaseData = await _repository.AsSugarClient().Queryable<PortalDataEntity>()
            .Where(it => it.DeleteMark == null && it.Type.Equals("release") && it.PortalId.Equals(id))
            .FirstAsync();
        if (releaseData.IsNotEmptyOrNull())
        {
            releaseData.FormData = modelData.FormData;
            releaseData.LastModifyTime = DateTime.Now;
            releaseData.LastModifyUserId = _userManager.UserId;
            await _repository.AsSugarClient().Updateable(releaseData)
                .UpdateColumns(it => new { it.FormData, it.LastModifyTime, it.LastModifyUserId })
                .ExecuteCommandAsync();
        }
        else
        {
            var newReleaseData = modelData.Adapt<PortalDataEntity>();
            newReleaseData.Id = SnowflakeIdHelper.NextId();
            newReleaseData.Type = "release";
            await _repository.AsSugarClient().Insertable(newReleaseData).ExecuteCommandAsync();
        }

        #region 主页门户

        if (input.pcPortal == 1)
        {
            if (input.pcPortalSystemId.Any())
            {
                foreach (var item in input.pcPortalSystemId)
                {
                    if (!sysIdList.Contains(item)) throw Oops.Oh(ErrorCode.D4022);

                    portalManageList.Add(new PortalManageEntity()
                    {
                        Id = SnowflakeIdHelper.NextId(),
                        PortalId = id,
                        SystemId = item,
                        EnabledMark = 1,
                        Platform = "Web",
                        SortCode = 0,
                        CreatorTime = DateTime.Now,
                        CreatorUserId = _userManager.UserId,
                    });
                }
            }
            else
            {
                if (!await _repository.AsSugarClient().Queryable<PortalManageEntity>().AnyAsync(it => it.DeleteMark == null && sysIdList.Contains(it.SystemId) && it.PortalId.Equals(id) && it.Platform.Equals("Web")))
                    throw Oops.Oh(ErrorCode.D4023);
            }
        }

        if (input.appPortal == 1)
        {
            if (input.appPortalSystemId.Any())
            {
                foreach (var item in input.appPortalSystemId)
                {
                    if (!sysIdList.Contains(item)) throw Oops.Oh(ErrorCode.D4022);

                    portalManageList.Add(new PortalManageEntity()
                    {
                        Id = SnowflakeIdHelper.NextId(),
                        PortalId = id,
                        SystemId = item,
                        EnabledMark = 1,
                        Platform = "App",
                        SortCode = 0,
                        CreatorTime = DateTime.Now,
                        CreatorUserId = _userManager.UserId,
                    });
                }
            }
            else
            {
                if (!await _repository.AsSugarClient().Queryable<PortalManageEntity>().AnyAsync(it => it.DeleteMark == null && sysIdList.Contains(it.SystemId) && it.PortalId.Equals(id) && it.Platform.Equals("App")))
                    throw Oops.Oh(ErrorCode.D4023);
            }
        }

        // 添加门户管理
        await _repository.AsSugarClient().Insertable(portalManageList).ExecuteCommandAsync();

        #endregion

        #region 菜单门户

        var moduleList = await _repository.AsSugarClient().Queryable<ModuleEntity>().Where(it => it.DeleteMark == null).ToListAsync();
        var allModuleList = moduleList.Where(it => it.PropertyJson.Contains(id)).ToList();
        var portalModuleList = new List<ModuleEntity>();
        if (input.pc == 1)
        {
            if (!input.pcModuleParentId.Any() && !await _repository.AsSugarClient().Queryable<ModuleEntity>().AnyAsync(it => it.DeleteMark == null && sysIdList.Contains(it.SystemId) && it.Category.Equals("Web") && it.PropertyJson.Contains(id)))
                throw Oops.Oh(ErrorCode.D4017);

            // 新发布的菜单
            var dic = new List<Dictionary<string, string>>();
            foreach (var item in input.pcModuleParentId)
            {
                if (sysIdList.Contains(item))
                {
                    dic.Add(new Dictionary<string, string> { { item, "-1" } });
                }
                else
                {
                    var module = moduleList.Find(it => it.Id.Equals(item));
                    if (module.IsNullOrEmpty()) throw Oops.Oh(ErrorCode.D4021);
                    dic.Add(new Dictionary<string, string> { { module.SystemId, module.Id } });
                }
            }

            foreach (var item in dic)
            {
                var data = item.First();
                var fullName = entity.FullName;
                var enCode = entity.EnCode + new Random().NextLetterAndNumberString(5);

                if (_repository.AsSugarClient().Queryable<ModuleEntity>().Any(x => x.FullName == fullName && x.ParentId == data.Value && x.SystemId == data.Key && x.Category == "Web" && x.DeleteMark == null))
                    throw Oops.Oh(ErrorCode.COM1032);
                if (_repository.AsSugarClient().Queryable<ModuleEntity>().Any(x => x.EnCode == enCode && x.Category == "Web" && x.DeleteMark == null))
                    throw Oops.Oh(ErrorCode.COM1031);

                var module = new ModuleEntity()
                {
                    Id = SnowflakeIdHelper.NextId(),
                    FullName = fullName,
                    EnCode = enCode,
                    UrlAddress = string.Format("portal/{0}", enCode),
                    ParentId = data.Value,
                    SystemId = data.Key,
                    ModuleId = id,
                    PropertyJson = new { moduleId = id, iconBackgroundColor = string.Empty, isTree = 0 }.ToJsonString(),
                    Icon = "icon-ym icon-ym-webForm",
                    Category = "Web",
                    IsButtonAuthorize = 0,
                    IsColumnAuthorize = 0,
                    IsDataAuthorize = 0,
                    IsFormAuthorize = 0,
                    EnabledMark = 1,
                    CreatorTime = DateTime.Now,
                    CreatorUserId = _userManager.UserId,
                    SortCode = 999,
                    Type = 8
                };

                await _repository.AsSugarClient().Insertable(module).ExecuteCommandAsync();
            }
        }
        if (input.app == 1)
        {
            if (!input.appModuleParentId.Any() && !await _repository.AsSugarClient().Queryable<ModuleEntity>().AnyAsync(it => it.DeleteMark == null && sysIdList.Contains(it.SystemId) && it.Category.Equals("App") && it.PropertyJson.Contains(id)))
                throw Oops.Oh(ErrorCode.D4017);

            // 新发布的菜单
            var dic = new List<Dictionary<string, string>>();
            foreach (var item in input.appModuleParentId)
            {
                if (sysIdList.Contains(item))
                {
                    dic.Add(new Dictionary<string, string> { { item, "-1" } });
                }
                else
                {
                    var module = moduleList.Find(it => it.Id.Equals(item));
                    if (module.IsNullOrEmpty()) throw Oops.Oh(ErrorCode.D4021);
                    dic.Add(new Dictionary<string, string> { { module.SystemId, module.Id } });
                }
            }

            foreach (var item in dic)
            {
                var data = item.First();
                var fullName = entity.FullName;
                var enCode = entity.EnCode + new Random().NextLetterAndNumberString(5);

                if (_repository.AsSugarClient().Queryable<ModuleEntity>().Any(x => x.FullName == fullName && x.ParentId == data.Value && x.SystemId == data.Key && x.Category == "App" && x.DeleteMark == null))
                    throw Oops.Oh(ErrorCode.COM1032);
                if (_repository.AsSugarClient().Queryable<ModuleEntity>().Any(x => x.EnCode == enCode && x.Category == "App" && x.DeleteMark == null))
                    throw Oops.Oh(ErrorCode.COM1031);

                var module = new ModuleEntity()
                {
                    Id = SnowflakeIdHelper.NextId(),
                    FullName = fullName,
                    EnCode = enCode,
                    UrlAddress = enCode,
                    ParentId = data.Value,
                    SystemId = data.Key,
                    ModuleId = id,
                    PropertyJson = new { moduleId = id, iconBackgroundColor = string.Empty, isTree = 0 }.ToJsonString(),
                    Icon = "icon-ym icon-ym-webForm",
                    Category = "App",
                    IsButtonAuthorize = 0,
                    IsColumnAuthorize = 0,
                    IsDataAuthorize = 0,
                    IsFormAuthorize = 0,
                    EnabledMark = 1,
                    CreatorTime = DateTime.Now,
                    CreatorUserId = _userManager.UserId,
                    SortCode = 999,
                    Type = 8
                };

                await _repository.AsSugarClient().Insertable(module).ExecuteCommandAsync();
            }
        }

        #endregion

        var updateList = new List<string>();
        if (input.pc.Equals(1) || input.pcPortal.Equals(1))
            updateList.Add("Web");
        if (input.app.Equals(1) || input.appPortal.Equals(1))
            updateList.Add("App");

        foreach (var item in updateList)
        {
            var dataList = await _repository.AsSugarClient().Queryable<PortalDataEntity>()
                .Where(it => it.DeleteMark == null && it.PortalId.Equals(id) && it.Platform.Equals(item) && it.Type.Equals("custom"))
                .ToListAsync();

            foreach (var upData in dataList)
            {
                upData.FormData = modelData.FormData;
                upData.LastModifyTime = DateTime.Now;
                upData.LastModifyUserId = _userManager.UserId;

                portalDataList.Add(upData);
            }
        }

        // 更新门户数据
        await _repository.AsSugarClient().Updateable(portalDataList)
            .UpdateColumns(it => new {
                it.FormData,
                it.LastModifyTime,
                it.LastModifyUserId
            }).ExecuteCommandAsync();

        // 更新门户发布状态
        await _repository.AsUpdateable()
            .SetColumns(it => new PortalEntity
            {
                State = 1,
                EnabledMark = 1,
                PlatformRelease = input.platformRelease
            })
            .Where(it => it.DeleteMark == null && it.Id.Equals(id)).ExecuteCommandAsync();
    }

    #endregion

    #region Private

    /// <summary>
    /// 递归获取发布菜单名称.
    /// </summary>
    private void GetReleaseName(List<string> list, List<ModuleEntity> moduleList, List<SystemEntity> systemList, ModuleEntity module, string? url)
    {
        if (url.IsNullOrEmpty()) url = module.FullName;

        if (module.ParentId.Equals("-1"))
        {
            var sys = systemList.Find(it => it.Id.Equals(module.SystemId));
            if (sys.IsNotEmptyOrNull())
            {
                url = string.Format("{0}/{1}", sys.FullName, url);
                list.Add(url);
            }
        }
        else
        {
            var mod = moduleList.Find(it => it.Id.Equals(module.ParentId));
            if (mod.IsNotEmptyOrNull())
            {
                url = string.Format("{0}/{1}", mod.FullName, url);
                GetReleaseName(list, moduleList, systemList, mod, url);
            }
        }
    }

    #endregion
}
