﻿using JNPF.Common.Configuration;
using JNPF.Common.Const;
using JNPF.Common.Core.Manager;
using JNPF.Common.Core.Manager.Files;
using JNPF.Common.Enums;
using JNPF.Common.Extension;
using JNPF.Common.Filter;
using JNPF.Common.Helper;
using JNPF.Common.Manager;
using JNPF.Common.Models;
using JNPF.Common.Models.NPOI;
using JNPF.Common.Security;
using JNPF.DatabaseAccessor;
using JNPF.DataEncryption;
using JNPF.DependencyInjection;
using JNPF.DynamicApiController;
using JNPF.FriendlyException;
using JNPF.Systems.Entitys.Dto.Role;
using JNPF.Systems.Entitys.Dto.User;
using JNPF.Systems.Entitys.Permission;
using JNPF.Systems.Interfaces.Permission;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NPOI.Util;
using SqlSugar;
using System.Data;
using System.Text.RegularExpressions;

namespace JNPF.Systems;

/// <summary>
/// 业务实现：角色信息.
/// </summary>
[ApiDescriptionSettings(Tag = "Permission", Name = "Role", Order = 167)]
[Route("api/permission/[controller]")]
public class RoleService : IRoleService, IDynamicApiController, ITransient
{
    /// <summary>
    /// 基础仓储.
    /// </summary>
    private readonly ISqlSugarRepository<RoleEntity> _repository;

    /// <summary>
    /// 操作权限服务.
    /// </summary>
    private readonly IAuthorizeService _authorizeService;

    /// <summary>
    /// 文件服务.
    /// </summary>
    private readonly IFileManager _fileManager;

    /// <summary>
    /// 缓存管理器.
    /// </summary>
    private readonly ICacheManager _cacheManager;

    /// <summary>
    /// 用户管理.
    /// </summary>
    private readonly IUserManager _userManager;

    /// <summary>
    /// 机构表服务.
    /// </summary>
    private readonly IOrganizeService _organizeService;

    /// <summary>
    /// 初始化一个<see cref="RoleService"/>类型的新实例.
    /// </summary>
    public RoleService(
        ISqlSugarRepository<RoleEntity> repository,
        IAuthorizeService authorizeService,
        ICacheManager cacheManager,
        IFileManager fileService,
        IOrganizeService organizeService,
        IUserManager userManager)
    {
        _repository = repository;
        _authorizeService = authorizeService;
        _cacheManager = cacheManager;
        _fileManager = fileService;
        _organizeService = organizeService;
        _userManager = userManager;
    }

    #region GET

    /// <summary>
    /// 获取列表.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpGet("")]
    public async Task<dynamic> GetList([FromQuery] RoleListInput input)
    {
        // 获取分级管理组织
        var dataScope = _userManager.DataScope.Where(x => x.Select).Select(x => x.organizeId).Distinct().ToList();

        PageInputBase? pageInput = input.Adapt<PageInputBase>();

        // 处理组织树 名称
        List<OrganizeEntity>? orgTreeNameList = _organizeService.GetOrgListTreeName();

        #region 获取组织层级

        List<string>? childOrgIds = new List<string>();
        if (input.organizeId.IsNotEmptyOrNull() && input.organizeId != "0")
        {
            childOrgIds.Add(input.organizeId);

            // 根据组织Id 获取所有子组织Id集合
            childOrgIds.AddRange(_repository.AsSugarClient().Queryable<OrganizeEntity>().ToChildList(x => x.ParentId, input.organizeId).Select(x => x.Id).ToList());
            childOrgIds = childOrgIds.Distinct().ToList();
        }

        #endregion

        SqlSugarPagedList<RoleListOutput>? data = new SqlSugarPagedList<RoleListOutput>();
        if (childOrgIds.Any())
        {
            data = await _repository.AsSugarClient().Queryable<RoleEntity, OrganizeRelationEntity>((a, b) => new JoinQueryInfos(JoinType.Left, a.Id == b.ObjectId))
                .Where((a, b) => childOrgIds.Contains(b.OrganizeId)).Where((a, b) => a.DeleteMark == null)
                .WhereIF(!pageInput.keyword.IsNullOrEmpty(), (a, b) => a.FullName.Contains(pageInput.keyword) || a.EnCode.Contains(pageInput.keyword))
                .WhereIF(!_userManager.IsAdministrator, (a, b) => dataScope.Contains(b.OrganizeId))
                .WhereIF(input.type.IsNotEmptyOrNull(), (a, b) => a.GlobalMark.Equals(input.type))
                .WhereIF(input.enabledMark.IsNotEmptyOrNull(), (a, b) => a.EnabledMark.Equals(input.enabledMark))
                .GroupBy((a, b) => new { a.Id, a.Type, a.GlobalMark, a.EnCode, a.FullName, a.EnabledMark, a.CreatorTime, a.SortCode })
                .OrderBy((a, b) => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc)
                .Select((a, b) => new RoleListOutput
                {
                    id = a.Id,
                    parentId = a.Type,
                    type = SqlFunc.IIF(a.GlobalMark == 1, "全局", "组织"),
                    enCode = a.EnCode,
                    fullName = a.FullName,
                    enabledMark = a.EnabledMark,
                    creatorTime = a.CreatorTime,
                    sortCode = a.SortCode
                }).ToPagedListAsync(input.currentPage, input.pageSize);
        }
        else
        {
            data = await _repository.AsSugarClient().Queryable<RoleEntity, OrganizeRelationEntity>((a, b) => new JoinQueryInfos(JoinType.Left, a.Id == b.ObjectId))
                .Where((a, b) => a.DeleteMark == null)
                .WhereIF(input.organizeId == "0", (a, b) => a.GlobalMark == 1)
                .WhereIF(!pageInput.keyword.IsNullOrEmpty(), (a, b) => a.FullName.Contains(pageInput.keyword) || a.EnCode.Contains(pageInput.keyword))
                .WhereIF(!_userManager.IsAdministrator && input.organizeId != "0", (a, b) => dataScope.Contains(b.OrganizeId))
                .WhereIF(input.type.IsNotEmptyOrNull(), (a, b) => a.GlobalMark.Equals(input.type))
                .WhereIF(input.enabledMark.IsNotEmptyOrNull(), (a, b) => a.EnabledMark.Equals(input.enabledMark))
                .GroupBy((a, b) => new { a.Id, a.Type, a.GlobalMark, a.EnCode, a.FullName, a.EnabledMark, a.CreatorTime, a.SortCode })
                .OrderBy((a, b) => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc)
                .Select((a, b) => new RoleListOutput
                {
                    id = a.Id,
                    parentId = a.Type,
                    type = SqlFunc.IIF(a.GlobalMark == 1, "全局", "组织"),
                    enCode = a.EnCode,
                    fullName = a.FullName,
                    enabledMark = a.EnabledMark,
                    creatorTime = a.CreatorTime,
                    sortCode = a.SortCode
                }).ToPagedListAsync(input.currentPage, input.pageSize);
        }

        #region 处理 多组织

        List<OrganizeRelationEntity>? orgUserIdAll = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>()
            .Where(x => data.list.Select(u => u.id).Contains(x.ObjectId)).ToListAsync();
        foreach (RoleListOutput? item in data.list)
        {
            // 获取组织集合
            var organizeList = orgUserIdAll.Where(x => x.ObjectId == item.id).Select(x => x.OrganizeId).ToList();
            item.organizeInfo = string.Join(" ; ", orgTreeNameList.Where(x => organizeList.Contains(x.Id)).Select(x => x.Description));
        }

        #endregion

        return PageResult<RoleListOutput>.SqlSugarPageResult(data);
    }

    /// <summary>
    /// 获取下拉框(类型+角色).
    /// </summary>
    /// <returns></returns>
    [HttpGet("Selector")]
    public async Task<dynamic> GetSelector()
    {
        var orgInfoList = _organizeService.GetOrgListTreeName();

        // 获取所有组织 对应 的 角色id集合
        var ridList = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>().Where(x => x.ObjectType == "Role")
            .Select(x => new { x.ObjectId, x.OrganizeId }).Distinct().ToListAsync();

        // 获取 全局角色 和 组织角色
        List<RoleListTreeOutput>? roleList = await _repository.AsQueryable().Where(a => a.DeleteMark == null && a.EnabledMark.Equals(1))
            .Where(a => a.GlobalMark == 1 || ridList.Select(x => x.ObjectId).Contains(a.Id))
            .Select(a => new RoleListTreeOutput
            {
                id = a.Id,
                parentId = a.GlobalMark.ToString(),
                fullName = a.FullName,
                enabledMark = a.EnabledMark,
                creatorTime = a.CreatorTime,
                type = "role",
                //organize = SqlFunc.IIF(a.GlobalMark == 1, "全局角色", "组织角色"),
                icon = "icon-ym icon-ym-generator-role",
                sortCode = 0
            }).MergeTable().OrderBy(a => a.sortCode).OrderBy(a => a.creatorTime, OrderByType.Desc).ToListAsync();

        // 处理 组织角色
        roleList.Where(x => ridList.Select(x => x.ObjectId).Contains(x.id)).ToList().ForEach(item =>
        {
            var oolist = ridList.Where(x => x.ObjectId == item.id).ToList();
            item.organize = string.Join(",", orgInfoList.Where(x => oolist.Select(x => x.OrganizeId).Contains(x.Id)).Select(x => x.Description));
            for (int i = 0; i < oolist.Count; i++)
            {
                if (i == 0)
                {
                    item.parentId = oolist.FirstOrDefault().OrganizeId;
                }
                else
                {
                    // 该角色属于多个组织
                    RoleListTreeOutput? newItem = item.ToObject<RoleListTreeOutput>();
                    newItem.parentId = oolist[i].OrganizeId;
                    roleList.Add(newItem);
                }
            }
        });

        // 设置 全局 根目录
        List<RoleListTreeOutput>? treeList = new List<RoleListTreeOutput>() { new RoleListTreeOutput() { id = "1", type = string.Empty, parentId = "-1", enCode = string.Empty, fullName = "全局", num = roleList.Count(x => x.parentId == "1"), sortCode = 9527 } };

        List<RoleListTreeOutput>? organizeList = orgInfoList.Select(x => new RoleListTreeOutput()
        {
            id = x.Id,
            type = x.Category,
            parentId = x.ParentId,
            organize = x.Description,
            organizeInfo = x.OrganizeIdTree,
            icon = x.Category == "company" ? "icon-ym icon-ym-tree-organization3" : "icon-ym icon-ym-tree-department1",
            fullName = x.FullName,
            sortCode = 1
        }).ToList();
        treeList.AddRange(organizeList);

        for (int i = 0; i < treeList.Count; i++)
        {
            treeList[i].num = roleList.Count(x => x.parentId == treeList[i].id);
        }

        treeList.Where(x => x.num > 0).ToList().ForEach(item =>
        {
            if (item.organizeInfo.IsNotEmptyOrNull())
            {
                treeList.Where(x => !x.id.Equals("1") && x.organizeInfo.IsNotEmptyOrNull()).Where(x => item.organizeInfo.Contains(x.id)).ToList().ForEach(it =>
                {
                    if (it != null && it.num < 1)
                        it.num = item.num;
                });
            }
        });

        var res = treeList.Where(x => x.num > 0).Union(roleList).OrderBy(x => x.sortCode).ToList().ToTree("-1");
        return new { list = OrderbyTree(res) };
    }

    /// <summary>
    /// 获取下拉框，有分级管理查看权限(类型+角色).
    /// </summary>
    /// <returns></returns>
    [HttpGet("SelectorByPermission")]
    public async Task<dynamic> GetSelectorByPermission()
    {
        // 获取分级管理组织
        var dataScope = _userManager.DataScope.Where(x => x.Edit).Select(x => x.organizeId).Distinct().ToList();

        var ridList = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>().Where(x => x.ObjectType == "Role")
            .WhereIF(!_userManager.IsAdministrator, x => dataScope.Contains(x.OrganizeId))
            .Select(x => new { x.ObjectId, x.OrganizeId }).ToListAsync();

        // 获取 全局角色 和 组织角色
        List<RoleListTreeOutput>? roleList = await _repository.AsQueryable().Where(a => a.DeleteMark == null && a.EnabledMark.Equals(1))
            .WhereIF(!_userManager.IsAdministrator, a => a.GlobalMark == 0 && ridList.Select(x => x.ObjectId).Contains(a.Id))
            .WhereIF(_userManager.IsAdministrator, a => a.GlobalMark == 1 || ridList.Select(x => x.ObjectId).Contains(a.Id))
            .Select(a => new RoleListTreeOutput
            {
                id = a.Id,
                parentId = a.GlobalMark.ToString(),
                fullName = a.FullName,
                enabledMark = a.EnabledMark,
                creatorTime = a.CreatorTime,
                type = "role",
                icon = "icon-ym icon-ym-global-role",
                sortCode = 0
            }).MergeTable().OrderBy(a => a.sortCode).OrderBy(a => a.creatorTime, OrderByType.Desc).ToListAsync();

        for (int i = 0; i < roleList.Count; i++) roleList[i].onlyId = "role_" + i;

        // 处理 组织角色
        roleList.Where(x => ridList.Select(x => x.ObjectId).Contains(x.id)).ToList().ForEach(item =>
        {
            var oolist = ridList.Where(x => x.ObjectId == item.id).ToList();

            for (int i = 0; i < oolist.Count; i++)
            {
                if (i == 0)
                {
                    item.parentId = oolist.FirstOrDefault().OrganizeId;
                }
                else
                {
                    // 该角色属于多个组织
                    RoleListTreeOutput? newItem = item.ToObject<RoleListTreeOutput>();
                    newItem.parentId = oolist[i].OrganizeId;
                    roleList.Add(newItem);
                }
            }
        });

        // 设置 全局  根目录
        List<RoleListTreeOutput>? treeList = new List<RoleListTreeOutput>();
        if (_userManager.IsAdministrator) treeList.Add(new RoleListTreeOutput() { id = "1", sortCode = 9999, type = string.Empty, parentId = "-1", enCode = string.Empty, fullName = "全局", num = roleList.Count(x => x.parentId == "1") });

        // 获取所有组织
        List<OrganizeEntity>? allOrgList = await _repository.AsSugarClient().Queryable<OrganizeEntity>().Where(x => x.DeleteMark == null).ToListAsync();

        if (!_userManager.IsAdministrator)
        {
            var orgIdList = new List<string>();
            allOrgList.Where(x => dataScope.Contains(x.Id)).ToList().ForEach(item =>
            {
                orgIdList.AddRange(item.OrganizeIdTree.Split(","));
            });
            allOrgList = allOrgList.Where(x => orgIdList.Contains(x.Id)).ToList();
        }

        List<RoleListTreeOutput>? organizeList = allOrgList.Select(x => new RoleListTreeOutput()
        {
            id = x.Id,
            type = x.Category,
            parentId = x.ParentId,
            organizeInfo = x.OrganizeIdTree,
            sortCode = 11,
            icon = x.Category == "company" ? "icon-ym icon-ym-tree-organization3" : "icon-ym icon-ym-tree-department1",
            fullName = x.FullName
        }).ToList();
        treeList.AddRange(organizeList);

        for (int i = 0; i < treeList.Count; i++)
        {
            treeList[i].onlyId = "organizeList_" + i;
            treeList[i].num = roleList.Count(x => x.parentId == treeList[i].id);
        }

        treeList.Where(x => x.num > 0).ToList().ForEach(item =>
        {
            if (item.organizeInfo.IsNotEmptyOrNull())
            {
                treeList.Where(x => !x.id.Equals("1") && x.organizeInfo.IsNotEmptyOrNull()).Where(x => item.organizeInfo.Contains(x.id)).ToList().ForEach(it =>
                {
                    if (it != null && it.num < 1)
                        it.num = item.num;
                });
            }
        });

        var res = treeList.Where(x => x.num > 0).Union(roleList).OrderBy(x => x.sortCode).ToList().ToTree("-1");
        return new { list = OrderbyTree(res) };
    }

    /// <summary>
    /// 获取信息.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <returns></returns>
    [HttpGet("{id}")]
    public async Task<dynamic> GetInfo(string id)
    {
        RoleEntity? entity = await _repository.GetFirstAsync(r => r.Id == id);
        RoleInfoOutput? output = entity.Adapt<RoleInfoOutput>();
        output.organizeIdsTree = new List<List<string>>();

        List<OrganizeRelationEntity>? oIds = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>().Where(x => x.ObjectId == id).ToListAsync();
        List<OrganizeEntity>? oList = await _repository.AsSugarClient().Queryable<OrganizeEntity>().Where(x => oIds.Select(o => o.OrganizeId).Contains(x.Id)).ToListAsync();

        oList.ForEach(item =>
        {
            if (item.OrganizeIdTree.IsNullOrEmpty()) item.OrganizeIdTree = item.Id;
            List<string>? idList = item.OrganizeIdTree?.Split(",").ToList();
            output.organizeIdsTree.Add(idList);
        });

        return output;
    }

    #endregion

    #region POST

    /// <summary>
    /// 获取下拉框.
    /// </summary>
    /// <returns></returns>
    [HttpPost("SelectedList")]
    public async Task<dynamic> SelectedList([FromBody] UserSelectedInput input)
    {
        var idList = new List<string>();
        foreach (var item in input.ids) idList.Add(item + "--" + "role");
        input.ids = idList;
        return await _organizeService.GetSelectedList(input);
    }

    /// <summary>
    /// 获取角色列表 根据组织Id集合.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("getListByOrgIds")]
    public async Task<dynamic> GetListByOrgIds([FromBody] RoleListInput input)
    {
        // 获取所有组织 对应 的 角色id集合
        var ridList = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>()
            .Where(x => x.ObjectType == "Role" && input.organizeIds.Contains(x.OrganizeId))
            .Select(x => new { x.ObjectId, x.OrganizeId }).ToListAsync();

        // 获取 全局角色 和 组织角色
        List<RoleListTreeOutput>? roleList = await _repository.AsSugarClient().Queryable<RoleEntity>()
            .Where(a => a.DeleteMark == null && a.EnabledMark == 1).Where(a => a.GlobalMark == 1 || ridList.Select(x => x.ObjectId).Contains(a.Id))
            .Select(a => new RoleListTreeOutput
            {
                id = a.Id,
                type = "role",
                parentId = a.GlobalMark.ToString(),
                fullName = a.FullName,
                enabledMark = a.EnabledMark,
                creatorTime = a.CreatorTime,
                sortCode = a.SortCode
            }).MergeTable().OrderBy(a => a.sortCode).OrderBy(a => a.creatorTime, OrderByType.Desc).ToListAsync();

        for (int i = 0; i < roleList.Count; i++) roleList[i].onlyId = "role_" + i;

        // 处理 组织角色
        roleList.Where(x => ridList.Select(x => x.ObjectId).Contains(x.id)).ToList().ForEach(item =>
        {
            var oolist = ridList.Where(x => x.ObjectId == item.id).ToList();

            for (int i = 0; i < oolist.Count; i++)
            {
                if (i == 0)
                {
                    item.parentId = oolist.FirstOrDefault().OrganizeId;
                }
                else
                {
                    // 该角色属于多个组织
                    RoleListTreeOutput? newItem = item.ToObject<RoleListTreeOutput>();
                    newItem.parentId = oolist[i].OrganizeId;
                    roleList.Add(newItem);
                }
            }
        });

        List<RoleListTreeOutput>? treeList = new List<RoleListTreeOutput>();

        // 处理组织树 名称
        List<OrganizeEntity>? allOrgList = _organizeService.GetOrgListTreeName();
        List<RoleListTreeOutput>? organizeList = allOrgList.Where(x => input.organizeIds.Contains(x.Id)).Select(x => new RoleListTreeOutput()
        {
            id = x.Id,
            type = x.Category,
            parentId = "0",
            enCode = string.Empty,
            fullName = x.Description,
            num = roleList.Count(x => x.parentId == x.id)
        }).ToList();
        treeList.AddRange(organizeList);
        treeList.Add(new RoleListTreeOutput() { id = "1", type = string.Empty, parentId = "0", enCode = string.Empty, fullName = "全局", num = roleList.Count(x => x.parentId == "1") });

        for (int i = 0; i < treeList.Count; i++) treeList[i].onlyId = "organizeList_" + i;

        return new { list = treeList.Union(roleList).OrderBy(x => x.sortCode).ToList().ToTree("0") };
    }

    /// <summary>
    /// 新建.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("")]
    [UnitOfWork]
    public async Task Create([FromBody] RoleCrInput input)
    {
        // 全局角色 只能超管才能变更
        if (input.globalMark == 1 && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1612);

        #region 分级权限验证

        List<string?>? orgIdList = input.organizeIdsTree.Select(x => x.LastOrDefault()).ToList();
        if (!_userManager.DataScope.Any(it => orgIdList.Contains(it.organizeId) && it.Add) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);
        #endregion

        if (await _repository.IsAnyAsync(r => r.EnCode == input.enCode && r.DeleteMark == null))
            throw Oops.Oh(ErrorCode.D1600);
        if (await _repository.IsAnyAsync(r => r.FullName == input.fullName && r.GlobalMark == input.globalMark && r.DeleteMark == null))
            throw Oops.Oh(ErrorCode.D1601);

        RoleEntity? entity = input.Adapt<RoleEntity>();

        // 删除除了门户外的相关权限
        await _repository.AsSugarClient().Insertable(entity).CallEntityMethod(m => m.Creator()).ExecuteCommandAsync();

        #region 组织角色关系

        if (input.globalMark == 0)
        {
            List<OrganizeRelationEntity>? oreList = new List<OrganizeRelationEntity>();
            input.organizeIdsTree.ForEach(item =>
            {
                string? id = item.LastOrDefault();
                if (id.IsNotEmptyOrNull())
                {
                    OrganizeRelationEntity? oreEntity = new OrganizeRelationEntity();
                    oreEntity.ObjectType = "Role";
                    oreEntity.CreatorUserId = _userManager.UserId;
                    oreEntity.ObjectId = entity.Id;
                    oreEntity.OrganizeId = id;
                    oreList.Add(oreEntity);
                }
            });

            await _repository.AsSugarClient().Insertable(oreList).CallEntityMethod(m => m.Creator()).ExecuteCommandAsync(); // 插入关系数据

        }

        #endregion

        await DelRole(string.Format("{0}_{1}", _userManager.TenantId, _userManager.UserId));
    }

    /// <summary>
    /// 删除.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    [UnitOfWork]
    public async Task Delete(string id)
    {
        RoleEntity? entity = await _repository.GetFirstAsync(r => r.Id == id && r.DeleteMark == null);
        _ = entity ?? throw Oops.Oh(ErrorCode.D1608);

        // 全局角色 只能超管才能变更
        if (entity.GlobalMark == 1 && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1612);

        #region 分级权限验证

        // 旧数据
        List<string>? orgIdList = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>().Where(x => x.ObjectId == id && x.ObjectType == "Role").Select(x => x.OrganizeId).ToListAsync();
        if (!_userManager.DataScope.Any(it => orgIdList.Contains(it.organizeId) && it.Delete) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        #endregion

        // 角色下有数据权限不能删
        List<string>? items = await _authorizeService.GetAuthorizeItemIds(entity.Id, "resource");
        if (items.Count > 0)
            throw Oops.Oh(ErrorCode.D1603);

        // 角色下有表单不能删
        items = await _authorizeService.GetAuthorizeItemIds(entity.Id, "form");
        if (items.Count > 0)
            throw Oops.Oh(ErrorCode.D1606);

        // 角色下有列不能删除
        items = await _authorizeService.GetAuthorizeItemIds(entity.Id, "column");
        if (items.Count > 0)
            throw Oops.Oh(ErrorCode.D1605);

        // 角色下有按钮不能删除
        items = await _authorizeService.GetAuthorizeItemIds(entity.Id, "button");
        if (items.Count > 0)
            throw Oops.Oh(ErrorCode.D1604);

        // 角色下有菜单不能删
        items = await _authorizeService.GetAuthorizeItemIds(entity.Id, "module");
        if (items.Count > 0)
            throw Oops.Oh(ErrorCode.D1606);

        // 角色下有用户不能删
        if (await _repository.AsSugarClient().Queryable<UserRelationEntity>().AnyAsync(u => u.ObjectType == "Role" && u.ObjectId == id))
            throw Oops.Oh(ErrorCode.D6007);

        await _repository.AsSugarClient().Updateable(entity).CallEntityMethod(m => m.Delete()).UpdateColumns(it => new { it.DeleteMark, it.DeleteTime, it.DeleteUserId }).ExecuteCommandAsync();

        //// 删除角色和组织关联数据
        //await ForcedOffline(id);
        await _repository.AsSugarClient().Deleteable<OrganizeRelationEntity>().Where(x => x.ObjectType == "Role" && x.ObjectId == id).ExecuteCommandAsync();

        await DelRole(string.Format("{0}_{1}", _userManager.TenantId, _userManager.UserId));
    }

    /// <summary>
    /// 更新.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPut("{id}")]
    [UnitOfWork]
    public async Task Update(string id, [FromBody] RoleUpInput input)
    {
        RoleEntity? oldRole = await _repository.AsQueryable().Where(x => x.Id.Equals(input.id)).FirstAsync();

        // 全局角色 只能超管才能变更
        if (oldRole.GlobalMark == 1 && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1612);

        #region 分级权限验证

        // 旧数据
        List<string>? orgIdList = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>().Where(x => x.ObjectId == id && x.ObjectType == "Role").Select(x => x.OrganizeId).ToListAsync();
        if (!_userManager.DataScope.Any(it => orgIdList.Contains(it.organizeId) && it.Edit) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        // 新数据
        orgIdList = input.organizeIdsTree.Select(x => x.LastOrDefault()).ToList();
        if (!_userManager.DataScope.Any(it => orgIdList.Contains(it.organizeId) && it.Edit) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        #endregion

        if (await _repository.IsAnyAsync(r => r.EnCode == input.enCode && r.DeleteMark == null && r.Id != id))
            throw Oops.Oh(ErrorCode.D1600);
        if (await _repository.IsAnyAsync(r => r.FullName == input.fullName && r.GlobalMark == input.globalMark && r.DeleteMark == null && r.Id != id))
            throw Oops.Oh(ErrorCode.D1601);
        if (oldRole.EnabledMark.Equals(1) && input.enabledMark.Equals(0) && await _repository.AsSugarClient().Queryable<UserRelationEntity>().AnyAsync(x => x.ObjectId == id))
            throw Oops.Oh(ErrorCode.COM1030);

        #region 如果变更组织，该角色下已存在成员，则不允许修改

        if (oldRole.GlobalMark == 0)
        {
            // 查找该角色下的所有所属组织id
            List<string>? orgRoleList = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>().Where(x => x.ObjectType == "Role" && x.ObjectId == id).Select(x => x.OrganizeId).ToListAsync();

            // 查找该角色下的所有成员id
            List<string>? roleUserList = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.ObjectType == "Role" && x.ObjectId == id).Select(x => x.UserId).ToListAsync();

            // 获取带有角色成员的组织集合
            var orgUserCountList = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.ObjectType == "Organize" && roleUserList.Contains(x.UserId))
                .GroupBy(it => new { it.ObjectId })
                .Having(x => SqlFunc.AggregateCount(x.UserId) > 0)
                .Select(x => new { x.ObjectId, UCount = SqlFunc.AggregateCount(x.UserId) })
                .ToListAsync();
            List<string>? oldList = orgRoleList.Intersect(orgUserCountList.Select(x => x.ObjectId)).ToList(); // 将两个组织List交集
            List<string?>? newList = input.organizeIdsTree.Select(x => x.LastOrDefault()).ToList();

            if (oldList.Except(newList).Any()) throw Oops.Oh(ErrorCode.D1613);
        }

        // 全局改成组织
        if (oldRole.GlobalMark == 1 && input.globalMark == 0 && _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.ObjectType == "Role" && x.ObjectId == id).Any())
        {
            throw Oops.Oh(ErrorCode.D1615);
        }

        #endregion

        if (oldRole.EnabledMark == 1 && input.enabledMark != 1)
        {
            // 角色下有用户则无法停用
            if (await _repository.AsSugarClient().Queryable<UserRelationEntity>().AnyAsync(u => u.ObjectType == "Role" && u.ObjectId == id))
                throw Oops.Oh(ErrorCode.D6007);
        }

        RoleEntity? entity = input.Adapt<RoleEntity>();

        await _repository.AsSugarClient().Updateable(entity).IgnoreColumns(ignoreAllNullColumns: true).CallEntityMethod(m => m.LastModify()).ExecuteCommandAsync();

        #region 组织角色关系

        //await ForcedOffline(id);
        await _repository.AsSugarClient().Deleteable<OrganizeRelationEntity>().Where(x => x.ObjectType == "Role" && x.ObjectId == entity.Id).ExecuteCommandAsync(); // 删除原数据
        if (input.globalMark == 0)
        {
            List<OrganizeRelationEntity>? oreList = new List<OrganizeRelationEntity>();
            input.organizeIdsTree.ForEach(item =>
            {
                string? id = item.LastOrDefault();
                if (id.IsNotEmptyOrNull())
                {
                    OrganizeRelationEntity? oreEntity = new OrganizeRelationEntity();
                    oreEntity.ObjectType = "Role";
                    oreEntity.CreatorUserId = _userManager.UserId;
                    oreEntity.ObjectId = entity.Id;
                    oreEntity.OrganizeId = id;
                    oreList.Add(oreEntity);
                }
            });

            await _repository.AsSugarClient().Insertable(oreList).CallEntityMethod(m => m.Creator()).ExecuteCommandAsync(); // 插入关系数据
        }

        #endregion

        await DelRole(string.Format("{0}_{1}", _userManager.TenantId, _userManager.UserId));
    }

    /// <summary>
    /// 更新状态.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <returns></returns>
    [HttpPut("{id}/Actions/State")]
    public async Task UpdateState(string id)
    {
        if (!await _repository.IsAnyAsync(r => r.Id == id && r.DeleteMark == null)) throw Oops.Oh(ErrorCode.D1608);

        // 只能超管才能变更
        if (!_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1612);

        int isOk = await _repository.AsSugarClient().Updateable<RoleEntity>().SetColumns(it => new RoleEntity()
        {
            EnabledMark = SqlFunc.IIF(it.EnabledMark == 1, 0, 1),
            LastModifyUserId = _userManager.UserId,
            LastModifyTime = SqlFunc.GetDate()
        }).Where(it => it.Id == id).ExecuteCommandAsync();
        if (!(isOk > 0)) throw Oops.Oh(ErrorCode.D1610);

        await DelRole(string.Format("{0}_{1}", _userManager.TenantId, _userManager.UserId));
    }

    /// <summary>
    /// 通过角色id获取角色列表.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpPost("RoleCondition")]
    public async Task<dynamic> RoleCondition([FromBody] RoleConditionInput input)
    {
        // 获取所有组织
        List<OrganizeEntity>? allOrgList = _organizeService.GetOrgListTreeName();

        if (input.ids.Contains("@currentOrg"))
        {
            input.ids.Add(_userManager.User.OrganizeId);
            input.ids.Remove("@currentOrg");
        }
        if (input.ids.Contains("@currentOrgAndSubOrg"))
        {
            input.ids.AddRange(allOrgList.Copy().TreeChildNode(_userManager.User.OrganizeId, t => t.Id, t => t.ParentId).Select(it => it.Id).ToList());
            input.ids.Remove("@currentOrgAndSubOrg");
        }
        if (input.ids.Contains("@currentGradeOrg"))
        {
            if (_userManager.IsAdministrator)
            {
                input.ids.AddRange(allOrgList.Select(it => it.Id).ToList());
                var globalIds = await _repository.AsQueryable().Where(it => it.DeleteMark == null && it.EnabledMark == 1 && it.GlobalMark == 1).Select(it => it.Id).ToListAsync();
                input.ids.AddRange(globalIds);
            }
            else
            {
                input.ids.AddRange(_userManager.DataScope.Select(x => x.organizeId).ToList());
            }
            input.ids.Remove("@currentGradeOrg");
        }

        // 获取对应组织 对应 的 角色id集合
        var allRidList = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>()
            .Where(x => x.ObjectType == "Role")
            .Select(x => new { x.ObjectId, x.OrganizeId }).Distinct().ToListAsync();

        var ridList = allRidList.Where(x => input.ids.Contains(x.OrganizeId)).ToList();

        // 获取 全局角色 和 组织角色
        List<RoleListTreeOutput>? roleList = await _repository.AsQueryable()
            .Where(a => a.DeleteMark == null && a.EnabledMark == 1)
            .Where(a => ((a.GlobalMark == 1 || input.ids.Contains(a.Id)) && input.ids.Contains(a.Id)) || ridList.Select(x => x.ObjectId).Contains(a.Id))
            .Select(a => new RoleListTreeOutput
            {
                id = a.Id,
                type = "role",
                fullName = a.FullName,
                parentId = a.GlobalMark.ToString(),
                enabledMark = a.EnabledMark,
                creatorTime = a.CreatorTime,
                sortCode = a.SortCode,
                icon = "icon-ym icon-ym-global-role"
            }).MergeTable().OrderBy(a => a.sortCode).OrderBy(a => a.creatorTime, OrderByType.Desc).ToListAsync();

        for (int i = 0; i < roleList.Count; i++) roleList[i].onlyId = "role_" + i;

        // 处理 组织角色
        roleList.Where(x => allRidList.Select(x => x.ObjectId).Contains(x.id)).ToList().ForEach(item =>
        {
            var oolist = allRidList.Where(x => x.ObjectId == item.id).ToList();

            if (ridList.Where(it => it.ObjectId.Contains(item.id)).ToList().Count > 0)
                oolist = oolist.Where(x => ridList.Select(it => it.OrganizeId).Contains(x.OrganizeId)).ToList();

            for (int i = 0; i < oolist.Count; i++)
            {
                if (i == 0)
                {
                    item.parentId = oolist.FirstOrDefault().OrganizeId;
                }
                else
                {
                    // 该角色属于多个组织
                    RoleListTreeOutput? newItem = item.ToObject<RoleListTreeOutput>();
                    newItem.parentId = oolist[i].OrganizeId;
                    roleList.Add(newItem);
                }
            }
        });

        List<RoleListTreeOutput>? treeList = new List<RoleListTreeOutput>();

        // 处理组织树 名称
        List<RoleListTreeOutput>? organizeList = allOrgList.Where(x => roleList.Select(it => it.parentId).Contains(x.Id) || input.ids.Contains(x.Id)).Select(x => new RoleListTreeOutput()
        {
            id = x.Id,
            type = x.Category,
            parentId = x.ParentId.Equals("-1") ? "0" : x.ParentId,
            fullName = x.Description,
            organizeId = x.OrganizeIdTree,
            num = roleList.Count(xx => xx.parentId == x.Id),
            sortCode = 99
        }).ToList();

        organizeList.OrderByDescending(x => x.organizeId.Length).ToList().ForEach(item =>
        {
            if (!organizeList.Any(x => item.parentId.Equals(x.id))) item.parentId = "0";
            var pOrgTree = organizeList.Where(x => x.organizeId != item.organizeId && item.organizeId.Contains(x.organizeId)).FirstOrDefault()?.fullName;
            if (organizeList.Any(x => item.parentId.Equals(x.id))) pOrgTree = organizeList.FirstOrDefault(x => item.parentId.Equals(x.id))?.fullName;

            if (pOrgTree.IsNotEmptyOrNull() && item.organizeId.IsNotEmptyOrNull()) item.fullName = item.fullName.Replace(pOrgTree + "/", string.Empty);

            foreach (var subItem in organizeList.Where(it => it.num > 0).Select(it => it.organizeId).ToList())
            {
                if (subItem.Contains(item.id))
                    item.num = 1;
            }
        });

        treeList.AddRange(organizeList);
        if (roleList.Where(it => it.parentId.Equals("1")).ToList().Count > 0)
        {
            treeList.Add(new RoleListTreeOutput() { id = "1", type = string.Empty, parentId = "0", enCode = string.Empty, fullName = "全局", num = roleList.Count(x => x.parentId == "1"), sortCode = 999 });
        }

        for (int i = 0; i < treeList.Count; i++) treeList[i].onlyId = "organizeList_" + i;

        return new { list = treeList.Where(it => it.num > 0).ToList().Union(roleList).OrderBy(x => x.sortCode).ToList().ToTree("0") };
    }

    #endregion
    
    #region 导出和导入

    /// <summary>
    /// 导出Excel.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpGet("ExportData")]
    public async Task<dynamic> ExportData([FromQuery] RoleExportDataInput input)
    {
        // 获取分级管理组织
        var dataScope = _userManager.DataScope.Where(x => x.Select).Select(x => x.organizeId).Distinct().ToList();

        #region 获取组织层级

        List<string>? childOrgIds = new List<string>();
        if (input.organizeId.IsNotEmptyOrNull() && input.organizeId != "0")
        {
            childOrgIds.Add(input.organizeId);

            // 根据组织Id 获取所有子组织Id集合
            childOrgIds.AddRange(_repository.AsSugarClient().Queryable<OrganizeEntity>().ToChildList(x => x.ParentId, input.organizeId).Select(x => x.Id).ToList());
            childOrgIds = childOrgIds.Distinct().ToList();
        }

        #endregion

        var data = new List<RoleListImportDataInput>();
        if (childOrgIds.Any())
        {
            var queryable = _repository.AsSugarClient().Queryable<RoleEntity, OrganizeRelationEntity>((a, b) => new JoinQueryInfos(JoinType.Left, a.Id == b.ObjectId))
                .Where((a, b) => childOrgIds.Contains(b.OrganizeId)).Where((a, b) => a.DeleteMark == null)
                .WhereIF(!input.keyword.IsNullOrEmpty(), (a, b) => a.FullName.Contains(input.keyword) || a.EnCode.Contains(input.keyword))
                .WhereIF(!_userManager.IsAdministrator, (a, b) => dataScope.Contains(b.OrganizeId))
                .WhereIF(input.type.IsNotEmptyOrNull(), (a, b) => a.GlobalMark.Equals(input.type))
                .WhereIF(input.enabledMark.IsNotEmptyOrNull(), (a, b) => a.EnabledMark.Equals(input.enabledMark))
                .GroupBy((a, b) => new { a.Id, a.Type, a.GlobalMark, a.EnCode, a.FullName, a.EnabledMark, a.CreatorTime, a.SortCode })
                .OrderBy((a, b) => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc)
                .Select((a, b) => new RoleListImportDataInput
                {
                    id = a.Id,
                    globalMark = SqlFunc.IIF(a.GlobalMark == 1, "全局", "组织"),
                    enCode = a.EnCode,
                    fullName = a.FullName,
                    enabledMark = SqlFunc.IIF(a.EnabledMark.Equals(1), "启用", "禁用"),
                    description = a.Description,
                    creatorTime = a.CreatorTime,
                    sortCode = SqlFunc.ToString(a.SortCode)
                });

            if (input.dataType.Equals("0")) data = (await queryable.ToPagedListAsync(input.currentPage, input.pageSize)).list.ToList();
            else data = await queryable.ToListAsync();
        }
        else
        {
            var queryable = _repository.AsSugarClient().Queryable<RoleEntity, OrganizeRelationEntity>((a, b) => new JoinQueryInfos(JoinType.Left, a.Id == b.ObjectId))
                .Where((a, b) => a.DeleteMark == null)
                .WhereIF(input.organizeId == "0", (a, b) => a.GlobalMark == 1)
                .WhereIF(!input.keyword.IsNullOrEmpty(), (a, b) => a.FullName.Contains(input.keyword) || a.EnCode.Contains(input.keyword))
                .WhereIF(!_userManager.IsAdministrator && input.organizeId != "0", (a, b) => dataScope.Contains(b.OrganizeId))
                .WhereIF(input.type.IsNotEmptyOrNull(), (a, b) => a.GlobalMark.Equals(input.type))
                .WhereIF(input.enabledMark.IsNotEmptyOrNull(), (a, b) => a.EnabledMark.Equals(input.enabledMark))
                .GroupBy((a, b) => new { a.Id, a.Type, a.GlobalMark, a.EnCode, a.FullName, a.EnabledMark, a.CreatorTime, a.SortCode, a.Description })
                .OrderBy((a, b) => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc)
                .Select((a, b) => new RoleListImportDataInput
                {
                    id = a.Id,
                    globalMark = SqlFunc.IIF(a.GlobalMark == 1, "全局", "组织"),
                    enCode = a.EnCode,
                    fullName = a.FullName,
                    enabledMark = SqlFunc.IIF(a.EnabledMark.Equals(1), "启用", "禁用"),
                    description = a.Description,
                    creatorTime = a.CreatorTime,
                    sortCode = SqlFunc.ToString(a.SortCode)
                });

            if (input.dataType.Equals("0")) data = (await queryable.ToPagedListAsync(input.currentPage, input.pageSize)).list.ToList();
            else data = await queryable.ToListAsync();
        }

        // 处理多组织
        var orgList = _organizeService.GetOrgListTreeName();
        List<OrganizeRelationEntity>? orgUserIdAll = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>()
            .Where(x => data.Select(u => u.id).Contains(x.ObjectId)).ToListAsync();

        data.ForEach(item =>
        {
            var orgIds = orgUserIdAll.Where(x => x.ObjectId.Equals(item.id)).Select(x => x.OrganizeId).ToList();
            item.organizeId = string.Join(",", orgList.Where(x => orgIds.Contains(x.Id)).Select(x => x.Description).ToList());
        });

        ExcelConfig excelconfig = new ExcelConfig();
        excelconfig.FileName = string.Format("角色信息_{0:yyyyMMddhhmmss}.xls", DateTime.Now);
        excelconfig.HeadFont = "微软雅黑";
        excelconfig.HeadPoint = 10;
        excelconfig.IsAllSizeColumn = true;
        excelconfig.IsBold = true;
        excelconfig.IsAllBorder = true;
        excelconfig.ColumnModel = new List<ExcelColumnModel>();
        var columnList = GetInfoFieldToTitle(input.selectKey.Split(',').ToList());
        foreach (var item in columnList)
        {
            excelconfig.ColumnModel.Add(new ExcelColumnModel() { Column = item.ColumnKey, ExcelColumn = item.ColumnValue, Required = item.Required });
        }

        string? addPath = Path.Combine(FileVariable.TemporaryFilePath, excelconfig.FileName);
        var fs = ExcelExportHelper<RoleListImportDataInput>.ExportMemoryStream(data, excelconfig);
        var flag = await _fileManager.UploadFileByType(fs, FileVariable.TemporaryFilePath, excelconfig.FileName);
        if (flag)
        {
            fs.Flush();
            fs.Close();
        }

        _cacheManager.Set(excelconfig.FileName, string.Empty);
        return new { name = excelconfig.FileName, url = "/api/file/Download?encryption=" + DESEncryption.Encrypt(_userManager.UserId + "|" + excelconfig.FileName + "|" + addPath, "JNPF") };
    }

    /// <summary>
    /// 模板下载.
    /// </summary>
    /// <returns></returns>
    [HttpGet("TemplateDownload")]
    public async Task<dynamic> TemplateDownload()
    {
        // 初始化 一条空数据
        List<RoleListImportDataInput>? dataList = new List<RoleListImportDataInput>() { new RoleListImportDataInput()
            { organizeId = "公司名称/公司名称1/部门名称" , globalMark = "", enabledMark = "" } };

        ExcelConfig excelconfig = new ExcelConfig();
        excelconfig.FileName = "角色信息导入模板.xls";
        excelconfig.HeadFont = "微软雅黑";
        excelconfig.HeadPoint = 10;
        excelconfig.IsAllSizeColumn = true;
        excelconfig.IsBold = true;
        excelconfig.IsAllBorder = true;
        excelconfig.IsAnnotation = true;
        excelconfig.ColumnModel = new List<ExcelColumnModel>();
        var infoFields = GetInfoFieldToTitle();
        infoFields.RemoveAll(x => x.ColumnKey.Equals("errorsInfo"));
        foreach (var item in infoFields)
        {
            excelconfig.ColumnModel.Add(new ExcelColumnModel() { Column = item.ColumnKey, ExcelColumn = item.ColumnValue, Required = item.Required, SelectList = item.SelectList });
        }

        string? addPath = Path.Combine(FileVariable.TemporaryFilePath, excelconfig.FileName);
        var stream = ExcelExportHelper<RoleListImportDataInput>.ToStream(dataList, excelconfig);
        await _fileManager.UploadFileByType(stream, FileVariable.TemporaryFilePath, excelconfig.FileName);
        _cacheManager.Set(excelconfig.FileName, string.Empty);
        return new { name = excelconfig.FileName, url = "/api/file/Download?encryption=" + DESEncryption.Encrypt(_userManager.UserId + "|" + excelconfig.FileName + "|" + addPath, "JNPF") };
    }

    /// <summary>
    /// 上传文件.
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    [HttpPost("Uploader")]
    public async Task<dynamic> Uploader(IFormFile file)
    {
        var _filePath = _fileManager.GetPathByType(string.Empty);
        var _fileName = DateTime.Now.ToString("yyyyMMdd") + "_" + SnowflakeIdHelper.NextId() + Path.GetExtension(file.FileName);
        var stream = file.OpenReadStream();
        await _fileManager.UploadFileByType(stream, _filePath, _fileName);
        return new { name = _fileName, url = string.Format("/api/File/Image/{0}/{1}", string.Empty, _fileName) };
    }

    /// <summary>
    /// 导入预览.
    /// </summary>
    /// <returns></returns>
    [HttpGet("ImportPreview")]
    public async Task<dynamic> ImportPreview(string fileName)
    {
        DataTable? excelData = new DataTable();
        try
        {
            var FileEncode = GetInfoFieldToTitle();

            string? filePath = FileVariable.TemporaryFilePath;
            string? savePath = Path.Combine(filePath, fileName);

            // 得到数据
            var sr = await _fileManager.GetFileStream(savePath);
            excelData = ExcelImportHelper.ToDataTable(savePath, sr);
            foreach (var it in excelData.Columns)
            {
                var item = it as DataColumn;
                if (item.ColumnName.Equals("errorsInfo")) throw Oops.Oh(ErrorCode.D1807);
                if(!FileEncode.Any(x => x.ColumnKey == item.ToString() && x.ColumnValue.Equals(item.Caption.Replace("*", "")))) throw Oops.Oh(ErrorCode.D1807);
                excelData.Columns[item.ToString()].ColumnName = FileEncode.Where(x => x.ColumnKey == item.ToString()).FirstOrDefault().ColumnKey;
            }

            if (excelData.Rows.Count > 0) excelData.Rows.RemoveAt(0);
        }
        catch (Exception)
        {
            throw Oops.Oh(ErrorCode.D1807);
        }

        if (excelData.Rows.Count < 1)
            throw Oops.Oh(ErrorCode.D5019);
        if (excelData.Rows.Count > 1000)
            throw Oops.Oh(ErrorCode.D5029);

        // 返回结果
        return new { dataRow = excelData };
    }

    /// <summary>
    /// 导出错误报告.
    /// </summary>
    /// <param name="list"></param>
    /// <returns></returns>
    [HttpPost("ExportExceptionData")]
    [UnitOfWork]
    public async Task<dynamic> ExportExceptionData([FromBody] RoleImportDataInput list)
    {
        list.list.ForEach(it => it.errorsInfo = string.Empty);
        object[]? res = await DataImport(list.list);

        // 错误数据
        List<RoleListImportDataInput>? errorlist = res.Last() as List<RoleListImportDataInput>;

        ExcelConfig excelconfig = new ExcelConfig();
        excelconfig.FileName = string.Format("角色信息导入模板错误报告_{0}.xls", DateTime.Now.ToString("yyyyMMddHHmmss"));
        excelconfig.HeadFont = "微软雅黑";
        excelconfig.HeadPoint = 10;
        excelconfig.IsAllSizeColumn = true;
        excelconfig.IsBold = true;
        excelconfig.IsAllBorder = true;
        excelconfig.IsAnnotation = true;
        excelconfig.ColumnModel = new List<ExcelColumnModel>();
        foreach (var item in GetInfoFieldToTitle())
        {
            excelconfig.ColumnModel.Add(new ExcelColumnModel() { Column = item.ColumnKey, ExcelColumn = item.ColumnValue, Required = item.Required, FreezePane = item.FreezePane, SelectList = item.SelectList });
        }

        string? addPath = Path.Combine(FileVariable.TemporaryFilePath, excelconfig.FileName);
        ExcelExportHelper<RoleListImportDataInput>.Export(errorlist, excelconfig, addPath);

        _cacheManager.Set(excelconfig.FileName, string.Empty);
        return new { name = excelconfig.FileName, url = "/api/file/Download?encryption=" + DESEncryption.Encrypt(_userManager.UserId + "|" + excelconfig.FileName + "|" + addPath, "JNPF") };
    }

    /// <summary>
    /// 导入数据.
    /// </summary>
    /// <param name="list"></param>
    /// <returns></returns>
    [HttpPost("ImportData")]
    [UnitOfWork]
    public async Task<dynamic> ImportData([FromBody] RoleImportDataInput list)
    {
        list.list.ForEach(x => x.errorsInfo = string.Empty);
        object[]? res = await DataImport(list.list);
        List<RoleEntity>? addlist = res.First() as List<RoleEntity>;
        List<RoleListImportDataInput>? errorlist = res.Last() as List<RoleListImportDataInput>;
        return new { snum = addlist.Count, fnum = errorlist.Count, failResult = errorlist, resultType = errorlist.Count < 1 ? 0 : 1 };
    }

    /// <summary>
    /// 导入数据函数.
    /// </summary>
    /// <param name="list">list.</param>
    /// <returns>[成功列表,失败列表].</returns>
    private async Task<object[]> DataImport(List<RoleListImportDataInput> list)
    {
        List<RoleListImportDataInput> inputList = list;

        if (inputList == null || inputList.Count() < 1)
            throw Oops.Oh(ErrorCode.D5019);
        if (inputList.Count > 1000)
            throw Oops.Oh(ErrorCode.D5029);

        var addList = new List<RoleEntity>();

        var pList = _repository.GetList();

        var orgList = _organizeService.GetOrgListTreeName();

        var regexEnCode = @"^[a-zA-Z0-9.]*$";
        var regexFullNameEnCode = @"^([\u4e00-\u9fa5]|[a-zA-Z0-9])+$";

        foreach (var item in inputList)
        {
            if (item.globalMark.IsNotEmptyOrNull() && item.globalMark.Equals("组织") && item.organizeId.IsNullOrWhiteSpace()) item.errorsInfo += "所属组织不能为空,";
            if (item.fullName.IsNullOrWhiteSpace()) item.errorsInfo += "角色名称不能为空,";
            else if (item.fullName.Length > 50) item.errorsInfo += "角色名称值超出最多输入字符限制,";
            if (item.fullName.IsNotEmptyOrNull() && !Regex.IsMatch(item.fullName, regexFullNameEnCode))
                item.errorsInfo += "角色名称值不能含有特殊符号,";

            if (item.enCode.IsNullOrWhiteSpace()) item.errorsInfo += "角色编码不能为空,";
            else if (item.enCode.Length > 50) item.errorsInfo += "角色编码值超出最多输入字符限制,";
            if (item.enCode.IsNotEmptyOrNull() && (!Regex.IsMatch(item.enCode, regexEnCode) || item.enCode.First().Equals('.') || item.enCode.Last().Equals('.')))
                item.errorsInfo += "角色编码值只能输入英文、数字和小数点且小数点不能放在首尾,";

            if (item.globalMark.IsNullOrWhiteSpace()) item.errorsInfo += "角色类型不能为空,";
            if (item.enabledMark.IsNullOrWhiteSpace()) item.errorsInfo += "状态不能为空,";
            if (item.sortCode.IsNotEmptyOrNull())
            {
                try
                {
                    var sortCode = long.Parse(item.sortCode);
                    if (sortCode < 0) item.errorsInfo += "排序值不能小于0,";
                    else if (sortCode > 999999) item.errorsInfo += "排序值不能大于999999,";
                }
                catch
                {
                    item.errorsInfo += "排序值不正确,";
                }
            }
            if (item.globalMark.IsNotEmptyOrNull() && !item.globalMark.Equals("全局") && !item.globalMark.Equals("组织")) item.errorsInfo += "找不到该角色类型值,";
            if (item.enabledMark.IsNotEmptyOrNull() && !item.enabledMark.Equals("禁用") && !item.enabledMark.Equals("启用")) item.errorsInfo += "状态值不正确,";
            if (inputList.Count(x => x.fullName == item.fullName) > 1)
            {
                var errorItems = inputList.Where(x => x.fullName.IsNotEmptyOrNull() && x.fullName == item.fullName).ToList();
                for (var i = 1; i < errorItems.Count; i++) if (!item.errorsInfo.Contains("角色名称值已存在")) errorItems[i].errorsInfo += "角色名称值已存在,";
            }
            if (inputList.Count(x => x.enCode == item.enCode) > 1)
            {
                var errorItems = inputList.Where(x => x.enCode.IsNotEmptyOrNull() && x.enCode == item.enCode).ToList();
                for (var i = 1; i < errorItems.Count; i++) if (!item.errorsInfo.Contains("角色编码值已存在")) errorItems[i].errorsInfo += "角色编码值已存在,";
            }

            var orgIds = new List<string>(); // 多组织 , 号隔开

            RoleEntity entity = new RoleEntity();
            entity.Id = SnowflakeIdHelper.NextId();
            entity.Description = item.description;
            if (item.globalMark.IsNotEmptyOrNull() && item.globalMark.Equals("组织") && item.organizeId.IsNotEmptyOrNull())
            {
                // 寻找组织
                var errorOrgList = new List<string>();
                string[]? userOidList = item.organizeId?.Split(",");
                if (userOidList != null && userOidList.Any())
                {
                    foreach (string? oinfo in userOidList)
                    {
                        if (orgList.Any(x => x.Description.Equals(oinfo))) orgIds.Add(orgList.Find(x => x.Description.Equals(oinfo)).Id);
                        else errorOrgList.Add(oinfo);
                    }
                }
                else
                {
                    // 如果未找到组织，列入错误列表
                    item.errorsInfo += "找不到该所属组织,";
                }

                // 存在未找到组织，列入错误列表
                if (errorOrgList.Any())
                {
                    if (errorOrgList.Count == 1) item.errorsInfo += "找不到该所属组织,";
                    else item.errorsInfo += string.Format("找不到该所属组织({0}),", string.Join("、", errorOrgList));
                }
            }

            if (orgIds.Any())
            {
                var relationList = new List<OrganizeRelationEntity>();
                orgIds.ForEach(it => relationList.Add(new OrganizeRelationEntity() { ObjectId = entity.Id, OrganizeId = it, ObjectType = "Role" }));
                _repository.AsSugarClient().Insertable(relationList).CallEntityMethod(m => m.Creator()).ExecuteCommand();
            }

            if (pList.Any(o => o.FullName == item.fullName && o.DeleteMark == null))
            {
                if (!item.errorsInfo.Contains("角色名称值已存在"))
                    item.errorsInfo += "角色名称值已存在,";
            }
            entity.FullName = item.fullName;

            if (pList.Any(o => o.EnCode == item.enCode && o.DeleteMark == null))
            {
                if (!item.errorsInfo.Contains("角色编码值已存在"))
                    item.errorsInfo += "角色编码值已存在,";
            }
            entity.EnCode = item.enCode;

            switch (item.enabledMark)
            {
                case "禁用":
                    entity.EnabledMark = 0;
                    break;
                case "启用":
                    entity.EnabledMark = 1;
                    break;
            }

            if (item.globalMark.IsNotEmptyOrNull())
            {
                switch (item.globalMark)
                {
                    case "全局":
                        entity.GlobalMark = 1;
                        break;
                    case "组织":
                        entity.GlobalMark = 0;
                        break;
                }
            }

            entity.SortCode = item.sortCode.IsNotEmptyOrNull() ? item.sortCode.ParseToLong() : 0;

            if (item.errorsInfo.IsNullOrEmpty()) addList.Add(entity);
        }

        if (addList.Any())
        {
            try
            {
                // 新增记录
                var newEntity = await _repository.AsInsertable(addList).CallEntityMethod(m => m.Create()).ExecuteReturnEntityAsync();
            }
            catch (Exception)
            {
                inputList.ForEach(item => item.errorsInfo = "系统异常");
                inputList = new List<RoleListImportDataInput>();
            }
        }

        var errorList = new List<RoleListImportDataInput>();
        foreach (var item in inputList)
        {
            if (item.errorsInfo.IsNotEmptyOrNull() && item.errorsInfo.Contains(","))
            {
                item.errorsInfo = item.errorsInfo.TrimEnd(',');
                errorList.Add(item);
            }
        }
        return new object[] { addList, errorList };
    }

    /// <summary>
    /// 字段对应 列名称.
    /// </summary>
    /// <returns></returns>
    private List<ExportImportHelperModel> GetInfoFieldToTitle(List<string> fields = null)
    {
        var res = new List<ExportImportHelperModel>();
        res.Add(new ExportImportHelperModel() { ColumnKey = "errorsInfo", ColumnValue = "异常原因", FreezePane = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "fullName", ColumnValue = "角色名称", Required = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "enCode", ColumnValue = "角色编码", Required = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "globalMark", ColumnValue = "角色类型", Required = true, SelectList = new List<string>() { "全局", "组织" } });
        res.Add(new ExportImportHelperModel() { ColumnKey = "organizeId", ColumnValue = "所属组织" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "enabledMark", ColumnValue = "状态", Required = true, SelectList = new List<string>() { "启用", "禁用" } });
        res.Add(new ExportImportHelperModel() { ColumnKey = "sortCode", ColumnValue = "排序" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "description", ColumnValue = "说明" });

        if (fields == null || !fields.Any()) return res;

        var result = new List<ExportImportHelperModel>();

        foreach (var item in res)
        {
            if (fields.Contains(item.ColumnKey)) result.Add(item);
        }

        return result;
    }

    #endregion

    #region PrivateMethod

    /// <summary>
    /// 删除角色缓存.
    /// </summary>
    /// <param name="userId">适配多租户模式(userId:tenantId_userId).</param>
    /// <returns></returns>
    private async Task<bool> DelRole(string userId)
    {
        string? cacheKey = string.Format("{0}{1}", CommonConst.CACHEKEYROLE, userId);
        await _cacheManager.DelAsync(cacheKey);
        return await Task.FromResult(true);
    }

    /// <summary>
    /// 递归排序 树形 List.
    /// </summary>
    /// <param name="list">.</param>
    /// <returns></returns>
    private List<RoleListTreeOutput> OrderbyTree(List<RoleListTreeOutput> list)
    {
        foreach (var item in list)
        {
            item.onlyId = SnowflakeIdHelper.NextId();
            var cList = item.children.ToObject<List<RoleListTreeOutput>>();
            if (cList != null)
            {
                cList = cList.OrderBy(x => x.sortCode).ToList();
                if (cList.Any()) OrderbyTree(cList);
                item.children = cList.ToObject<List<object>>();
            }
        }

        return list;
    }

    /// <summary>
    /// 强制角色下的所有用户下线.
    /// </summary>
    /// <param name="roleId">角色Id.</param>
    /// <returns></returns>
    public async Task ForcedOffline(string roleId)
    {
        // 查找该角色下的所有成员id
        var roleUserIds = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.ObjectType == "Role" && x.ObjectId == roleId).Select(x => x.UserId).ToListAsync();
        Scoped.Create((_, scope) =>
        {
            roleUserIds.ForEach(id =>
            {
                var services = scope.ServiceProvider;
                var _onlineuser = App.GetService<OnlineUserService>(services);
                _onlineuser.ForcedOffline(id);
            });
        });
    }
    #endregion
}