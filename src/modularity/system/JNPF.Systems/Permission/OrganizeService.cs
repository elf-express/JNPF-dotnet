using JNPF.Common.Configuration;
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
using JNPF.JsonSerialization;
using JNPF.LinqBuilder;
using JNPF.Systems.Entitys.Dto.Organize;
using JNPF.Systems.Entitys.Dto.SysConfig;
using JNPF.Systems.Entitys.Dto.User;
using JNPF.Systems.Entitys.Model.Organize;
using JNPF.Systems.Entitys.Permission;
using JNPF.Systems.Entitys.System;
using JNPF.Systems.Interfaces.Permission;
using JNPF.Systems.Interfaces.System;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NPOI.Util;
using SqlSugar;
using System.Data;
using System.Text.RegularExpressions;

namespace JNPF.Systems;

/// <summary>
/// 机构管理.
/// 组织架构：公司》部门》岗位》用户
/// 版 本：V3.2.0
/// 版 权：引迈信息技术有限公司（https://www.jnpfsoft.com）
/// 作 者：JNPF开发平台组
/// 日 期：2021.06.07.
/// </summary>
[ApiDescriptionSettings(Tag = "Permission", Name = "Organize", Order = 165)]
[Route("api/permission/[controller]")]
public class OrganizeService : IOrganizeService, IDynamicApiController, ITransient
{
    /// <summary>
    /// 基础仓储.
    /// </summary>
    private readonly ISqlSugarRepository<OrganizeEntity> _repository;

    /// <summary>
    /// 用户管理.
    /// </summary>
    private readonly IUserManager _userManager;

    /// <summary>
    /// 系统配置.
    /// </summary>
    private readonly ISysConfigService _sysConfigService;

    /// <summary>
    /// 第三方同步.
    /// </summary>
    private readonly ISynThirdInfoService _synThirdInfoService;

    /// <summary>
    /// 文件服务.
    /// </summary>
    private readonly IFileManager _fileManager;

    /// <summary>
    /// 缓存管理.
    /// </summary>
    private readonly ICacheManager _cacheManager;

    /// <summary>
    /// 多租户事务.
    /// </summary>
    private readonly ITenant _db;

    /// <summary>
    /// 初始化一个<see cref="OrganizeService"/>类型的新实例.
    /// </summary>
    public OrganizeService(
        ISqlSugarRepository<OrganizeEntity> repository,
        ISysConfigService sysConfigService,
        ISynThirdInfoService synThirdInfoService,
        IUserManager userManager,
        ICacheManager cacheManager,
        IFileManager fileService,
        ISqlSugarClient context)
    {
        _repository = repository;
        _cacheManager = cacheManager;
        _fileManager = fileService;
        _sysConfigService = sysConfigService;
        _synThirdInfoService = synThirdInfoService;
        _userManager = userManager;
        _db = context.AsTenant();
    }

    #region GET

    /// <summary>
    /// 获取机构列表.
    /// </summary>
    /// <param name="input">关键字参数.</param>
    /// <returns></returns>
    [HttpGet("")]
    public async Task<dynamic> GetList([FromQuery] CommonInput input)
    {
        StructureOrganizeIdTree(); // 构造组织树 id.

        // 获取分级管理组织
        var dataScope = _userManager.DataScope.Where(x => x.Select).Select(x => x.organizeId).Distinct().ToList();

        List<OrganizeListOutput>? data = await _repository.AsQueryable().Where(t => t.DeleteMark == null)
            .WhereIF(!_userManager.IsAdministrator, a => dataScope.Contains(a.Id))
            .WhereIF(input.type.IsNotEmptyOrNull(), a => a.Category.Equals(input.type))
            .OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc)
            .Select(x => new OrganizeListOutput()
            {
                category = x.Category,
                creatorTime = x.CreatorTime,
                enCode = x.EnCode,
                enabledMark = x.EnabledMark,
                fullName = x.FullName,
                id = x.Id,
                organizeIdTree = x.OrganizeIdTree,
                parentId = x.ParentId,
                sortCode = x.SortCode,
                icon = SqlFunc.IIF(x.Category.Equals("company"), "icon-ym icon-ym-tree-organization3", "icon-ym icon-ym-tree-department1"),
                type = x.Category
            }).ToListAsync();

        if (!string.IsNullOrEmpty(input.keyword))
            data = data.TreeWhere(t => t.fullName.Contains(input.keyword) || t.enCode.Contains(input.keyword), t => t.id, t => t.parentId);
        data.ForEach(item =>
        {
            if (!data.Any(x => x.id.Equals(item.parentId)))
                item.parentId = item.parentId.Equals("-1") ? "-1" : "0";
        });

        // 获取组织树
        var orgTree = GetOrgListTreeName();

        data.ForEach(item =>
        {
            item.fullName = orgTree.FirstOrDefault(x => x.Id.Equals(item.id))?.Description;
            item.organizeIds = item.organizeIdTree.Split(",").ToList();
        });

        // 组织断层处理
        data.OrderByDescending(x => x.organizeIdTree.Length).ToList().ForEach(item =>
        {
            if (!data.Any(x => x.id.Equals(item.parentId)))
            {
                var pItem = data.Find(x => x.id != item.id && item.organizeIdTree.Contains(x.organizeIdTree));
                if (pItem != null)
                {
                    item.parentId = pItem.id;
                    item.fullName = item.fullName.Replace(pItem.fullName + "/", string.Empty);
                }
                else
                {
                    item.parentId = item.parentId.Equals("-1") ? "-1" : "0";
                }
            }
            else
            {
                var pItem = data.Find(x => x.id.Equals(item.parentId));
                item.fullName = item.fullName.Replace(pItem.fullName + "/", string.Empty);
            }
        });

        return data.Any(x => x.parentId.Equals("-1")) ? new { list = data.OrderBy(x => x.sortCode).ToList().ToTree("-1") } : new { list = data.OrderBy(x => x.sortCode).ToList().ToTree("0") };
    }

    /// <summary>
    /// 获取下拉框.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Selector/{id}")]
    public async Task<dynamic> GetSelector(string id)
    {
        // 获取组织树
        var orgTree = GetOrgListTreeName();

        List<OrganizeEntity>? data = await _repository.AsQueryable().Where(t => t.Category.Equals("company") && t.DeleteMark == null).OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).ToListAsync();
        if (!"0".Equals(id))
        {
            OrganizeEntity? info = data.Find(it => it.Id == id);
            data.Remove(info);
        }

        List<OrganizeListOutput>? treeList = data.Adapt<List<OrganizeListOutput>>();
        treeList.ForEach(item =>
        {
            item.type = item.category;
            if (item != null && item.category.Equals("company")) item.icon = "icon-ym icon-ym-tree-organization3";
            item.organize = orgTree.FirstOrDefault(x => x.Id.Equals(item.id))?.Description;
            item.organizeIds = item.organizeIdTree.Split(",").ToList();
        });
        return new { list = treeList.OrderBy(x => x.sortCode).ToList().ToTree("-1") };
    }

    /// <summary>
    /// 获取下拉框根据权限.
    /// </summary>
    /// <returns></returns>
    [HttpGet("SelectorByAuth/{id}")]
    public async Task<dynamic> GetSelectorByAuth(string id)
    {
        // 获取组织树
        var orgTree = GetOrgListTreeName();

        // 获取分级管理组织
        var dataScope = _userManager.DataScope.Where(x => x.Select).Select(x => x.organizeId).Distinct().ToList();

        List<OrganizeEntity>? data = await _repository.AsQueryable().Where(t => t.Category.Equals("company") && t.DeleteMark == null)
            .WhereIF(!_userManager.IsAdministrator, x => dataScope.Contains(x.Id))
            .OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).ToListAsync();

        //if (!"0".Equals(id))
        //{
        //    OrganizeEntity? info = data.Find(it => it.Id == id);
        //    data.Remove(info);
        //}

        List<OrganizeSelectorOutput>? treeList = data.Adapt<List<OrganizeSelectorOutput>>();
        treeList.ForEach(item =>
        {
            item.type = item.category;
            if (item != null && item.category.Equals("company")) item.icon = "icon-ym icon-ym-tree-organization3";
            item.fullName = orgTree.FirstOrDefault(x => x.Id.Equals(item.id))?.Description;
            item.organize = item.fullName;
            item.organizeIds = item.organizeIdTree.Split(",").ToList();
        });

        if (!await _repository.AsSugarClient().Queryable<OrganizeEntity>().AnyAsync(x => treeList.Select(xx => xx.id).Contains(x.ParentId) && x.Id.Equals(id)))
        {
            if (!id.Equals("0") && id.IsNotEmptyOrNull())
            {
                var entity = orgTree.FirstOrDefault(x => x.Id.Equals(id));
                var pItem = orgTree.FirstOrDefault(x => x.Id.Equals(entity.ParentId));
                if (pItem != null)
                {
                    var addItem = pItem.Adapt<OrganizeSelectorOutput>();

                    if (addItem.type != null && addItem.type.Equals("company")) addItem.icon = "icon-ym icon-ym-tree-organization3";
                    addItem.fullName = orgTree.FirstOrDefault(x => x.Id.Equals(addItem.id))?.Description;
                    addItem.organize = addItem.fullName;
                    addItem.organizeIds = addItem.organizeIdTree.Split(",").ToList();
                    addItem.disabled = true;
                    addItem.sortCode = 0;
                    if (!treeList.Any(x => x.id.Equals(addItem.id))) treeList.Add(addItem);
                }
            }
        }

        // 组织断层处理
        treeList.Where(x => x.parentId != "-1").OrderByDescending(x => x.organizeIdTree.Length).ToList().ForEach(item =>
        {
            if (!treeList.Any(x => x.id.Equals(item.parentId)))
            {
                var pItem = treeList.Find(x => x.id != item.id && item.organizeIdTree.Contains(x.organizeIdTree));
                if (pItem != null)
                {
                    item.parentId = pItem.id;
                    item.fullName = item.fullName.Replace(pItem.fullName + "/", string.Empty);
                }
                else
                {
                    item.parentId = "-1";
                }
            }
            else
            {
                var pItem = treeList.Find(x => x.id.Equals(item.parentId));
                item.fullName = item.fullName.Replace(pItem.fullName + "/", string.Empty);
            }
        });

        return new { list = treeList.OrderBy(x => x.sortCode).ToList().ToTree("-1") };
    }

    /// <summary>
    /// 获取树形.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Tree")]
    public async Task<dynamic> GetTree()
    {
        List<OrganizeEntity>? data = await _repository.AsQueryable().Where(t => t.Category.Equals("company") && t.DeleteMark == null).OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).ToListAsync();
        List<OrganizeTreeOutput>? treeList = data.Adapt<List<OrganizeTreeOutput>>();
        return new { list = treeList.OrderBy(x => x.sortCode).ToList().ToTree("-1") };
    }

    /// <summary>
    /// 获取信息.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [HttpGet("{id}")]
    public async Task<dynamic> GetInfo(string id)
    {
        OrganizeEntity? entity = await _repository.GetSingleAsync(p => p.Id == id);
        var res = entity.Adapt<OrganizeInfoOutput>();
        if (entity.ParentId.Equals("-1")) res.organizeIdTree = new List<string>() { "-1" };
        else res.organizeIdTree = (await _repository.GetSingleAsync(p => p.Id == entity.ParentId)).OrganizeIdTree.Split(",").ToList();
        return res;
    }

    /// <summary>
    /// 获取下拉框异步.
    /// </summary>
    /// <returns></returns>
    [HttpGet("AsyncList/{id}")]
    public async Task<dynamic> GetAsyncList(string id, [FromQuery] PageInputBase input)
    {
        StructureOrganizeIdTree(); // 构造组织树 id.

        // 获取组织树
        var orgTree = GetOrgListTreeName();
        if (input.keyword.IsNullOrEmpty()) input.pageSize = 99999;

        // 获取分级管理组织
        var dataScope = _userManager.DataScope.Where(x => x.Select).Select(x => x.organizeId).Distinct().ToList();

        if (input.keyword.IsNotEmptyOrNull())
        {
            var keywordList = await _repository.AsQueryable().Where(t => t.DeleteMark == null)
            .WhereIF(!_userManager.IsAdministrator, x => dataScope.Contains(x.Id))
            .Where(x => x.FullName.Contains(input.keyword) || x.EnCode.Contains(input.keyword))
            .OrderBy(o => o.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).ToPagedListAsync(input.currentPage, input.pageSize);

            var resList = keywordList.list.Adapt<List<OrganizeSelectorOutput>>();

            foreach (var item in resList)
            {
                item.type = item.category;
                if (item.type != null && item.type.Equals("company")) item.icon = "icon-ym icon-ym-tree-organization3";
                item.fullName = item.fullName;
                item.organize = orgTree.FirstOrDefault(x => x.Id.Equals(item.id))?.Description;
                item.organizeIds = item.organizeIdTree.Split(",").ToList();
                item.isLeaf = null;
                item.children = null;
            }

            return PageResult<OrganizeSelectorOutput>.SqlSugarPageResult(new SqlSugarPagedList<OrganizeSelectorOutput>() { list = resList, pagination = keywordList.pagination });
        }
        else
        {
            var dataList = await _repository.AsQueryable().Where(t => t.DeleteMark == null)
                .WhereIF(!_userManager.IsAdministrator, x => dataScope.Contains(x.Id))
                .WhereIF(!id.Equals("0"), x => x.ParentId.Equals(id))
                .WhereIF(_userManager.IsAdministrator && id.Equals("0"), x => x.ParentId.Equals("-1"))
                .OrderBy(o => o.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).ToListAsync();

            List<OrganizeSelectorOutput>? treeList = dataList.Adapt<List<OrganizeSelectorOutput>>();
            if (id.IsNotEmptyOrNull() && !id.Equals("0")) treeList.Add(orgTree.Find(x => x.Id.Equals(id)).Adapt<OrganizeSelectorOutput>());

            treeList.ForEach(item =>
            {
                item.type = item.category;
                if (item.type != null && item.type.Equals("company")) item.icon = "icon-ym icon-ym-tree-organization3";
                item.fullName = orgTree.FirstOrDefault(x => x.Id.Equals(item.id))?.Description;
                item.organize = item.fullName;
                item.organizeIds = item.organizeIdTree.Split(",").ToList();
            });

            // 组织断层处理
            treeList.Where(x => x.parentId != "-1").OrderByDescending(x => x.organizeIdTree.Length).ToList().ForEach(item =>
            {
                if (!(input.keyword.IsNotEmptyOrNull() && id.Equals("0")))
                {
                    if (!treeList.Any(x => x.id.Equals(item.parentId)))
                    {
                        var pItem = treeList.Find(x => x.id != item.id && item.organizeIdTree.Contains(x.organizeIdTree));
                        if (pItem != null)
                        {
                            item.parentId = pItem.id;
                            item.fullName = item.fullName.Replace(pItem.fullName + "/", string.Empty);
                        }
                        else
                        {
                            item.parentId = "-1";
                        }
                    }
                    else
                    {
                        var pItem = treeList.Find(x => x.id.Equals(item.parentId));
                        item.fullName = item.fullName.Replace(pItem.fullName + "/", string.Empty);
                    }
                }
            });
            if (id.IsNotEmptyOrNull() && !id.Equals("0")) treeList.RemoveAll(x => x.id.Equals(id));

            var res = new List<OrganizeSelectorOutput>();
            if (id.Equals("0"))
                res = treeList.Where(x => x.parentId.Equals("-1")).ToList();
            else
                res = treeList.ToList();
            return new { list = res };
        }
    }

    /// <summary>
    /// 获取下拉框权限异步.
    /// </summary>
    /// <returns></returns>
    [HttpGet("SelectAsyncByAuth/{id}")]
    public async Task<dynamic> SelectAsyncByAuth(string id, [FromQuery] OrganizeCurrInput input)
    {
        StructureOrganizeIdTree(); // 构造组织树 id.

        // 获取组织树
        var orgTree = GetOrgListTreeName();
        if (input.keyword.IsNullOrEmpty()) input.pageSize = 99999;

        // 获取分级管理组织
        var dataScope = _userManager.DataScope.Where(x => x.Select).Select(x => x.organizeId).Distinct().ToList();

        var dataList = await _repository.AsQueryable().Where(t => t.DeleteMark == null && t.Category.Equals("company"))
            .WhereIF(!_userManager.IsAdministrator, x => dataScope.Contains(x.Id))
            .WhereIF(!id.Equals("0"), x => x.OrganizeIdTree.Contains(id))
            .WhereIF(id.Equals("0") && input.currOrgId.IsNotEmptyOrNull() && input.currOrgId != "0", x => !x.OrganizeIdTree.Contains(input.currOrgId))
            .WhereIF(input.keyword.IsNotEmptyOrNull(), x => x.FullName.Contains(input.keyword))
            .OrderBy(o => o.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).ToPagedListAsync(input.currentPage, input.pageSize);

        List<OrganizeSelectorOutput>? treeList = dataList.list.Adapt<List<OrganizeSelectorOutput>>();

        treeList.ForEach(item =>
        {
            item.type = item.category;
            if (item.type != null && item.type.Equals("company")) item.icon = "icon-ym icon-ym-tree-organization3";
            item.fullName = orgTree.FirstOrDefault(x => x.Id.Equals(item.id))?.Description;
            item.organize = item.fullName;
            item.organizeIds = item.organizeIdTree.Split(",").ToList();
        });

        if (!await _repository.AsSugarClient().Queryable<OrganizeEntity>().AnyAsync(x => treeList.Select(xx => xx.id).Contains(x.ParentId) && x.Id.Equals(id)))
        {
            if (!id.Equals("0") && id.IsNotEmptyOrNull())
            {
                var entity = orgTree.FirstOrDefault(x => x.Id.Equals(id));
                var pItem = orgTree.FirstOrDefault(x => x.Id.Equals(entity.ParentId));
                if (pItem != null)
                {
                    var addItem = pItem.Adapt<OrganizeSelectorOutput>();

                    if (addItem.type != null && addItem.type.Equals("company")) addItem.icon = "icon-ym icon-ym-tree-organization3";
                    addItem.fullName = orgTree.FirstOrDefault(x => x.Id.Equals(addItem.id))?.Description;
                    addItem.organize = addItem.fullName;
                    addItem.organizeIds = addItem.organizeIdTree.Split(",").ToList();
                    addItem.disabled = true;
                    addItem.sortCode = 0;
                    if (!treeList.Any(x => x.id.Equals(addItem.id))) treeList.Add(addItem);
                }
            }
        }

        // 组织断层处理
        treeList.Where(x => x.parentId != "-1").OrderByDescending(x => x.organizeIdTree.Length).ToList().ForEach(item =>
        {
            if (!(input.keyword.IsNotEmptyOrNull() && id.Equals("0")))
            {
                if (!treeList.Any(x => x.id.Equals(item.parentId)))
                {
                    var pItem = treeList.Find(x => x.id != item.id && item.organizeIdTree.Contains(x.organizeIdTree));
                    if (pItem != null)
                    {
                        item.parentId = pItem.id;
                        item.fullName = item.fullName.Replace(pItem.fullName + "/", string.Empty);
                    }
                    else
                    {
                        item.parentId = "-1";
                    }
                }
                else
                {
                    var pItem = treeList.Find(x => x.id.Equals(item.parentId));
                    item.fullName = item.fullName.Replace(pItem.fullName + "/", string.Empty);
                }
            }
        });

        if (input.keyword.IsNotEmptyOrNull() && id.Equals("0"))
        {
            return PageResult<OrganizeSelectorOutput>.SqlSugarPageResult(new SqlSugarPagedList<OrganizeSelectorOutput>() { list = treeList, pagination = dataList.pagination });
        }
        else
        {
            var res = new List<OrganizeSelectorOutput>();
            if (id.Equals("0"))
                res = treeList.Where(x => x.parentId.Equals("-1")).ToList();
            else
                res = treeList.Where(x => x.parentId.Equals(id)).ToList();
            return new { list = res };
        }
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
        if (input.ids.IsNotEmptyOrNull())
        {
            var idList = new List<string>();
            foreach (var item in input.ids) idList.Add(item + "--company");
            input.ids = idList;
        }

        return await GetSelectedList(input);
    }

    /// <summary>
    /// 根据组织Id List 获取当前所属组织(部门).
    /// </summary>
    /// <returns></returns>
    [HttpPost("getDefaultCurrentValueDepartmentId")]
    public async Task<dynamic> GetDefaultCurrentValueDepartmentId([FromBody] GetDefaultCurrentValueInput input)
    {
        var depId = _repository.AsSugarClient().Queryable<UserEntity, OrganizeEntity>((a, b) => new JoinQueryInfos(JoinType.Left, b.Id == a.OrganizeId))
            .Where((a, b) => a.Id.Equals(_userManager.UserId) && b.Category.Equals("department")).Select((a, b) => a.OrganizeId).First();

        if (input.DepartIds == null || !input.DepartIds.Any()) return new { departmentId = depId };
        var userRelationList = _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => input.DepartIds.Contains(x.ObjectId))
            .Select(x => x.UserId).ToList();

        if (userRelationList.Contains(_userManager.UserId)) return new { userId = depId };
        else return new { departmentId = string.Empty };
    }

    /// <summary>
    /// 通过部门id获取部门列表.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("OrganizeCondition")]
    public async Task<dynamic> OrganizeCondition([FromBody] OrganizeConditionInput input)
    {
        // 获取所有组织
        List<OrganizeEntity>? allOrgList = GetOrgListTreeName();

        if (input.departIds.Contains("@currentOrg"))
        {
            input.departIds.Add(_userManager.User.OrganizeId);
            input.departIds.Remove("@currentOrg");
        }
        if (input.departIds.Contains("@currentOrgAndSubOrg"))
        {
            input.departIds.AddRange(allOrgList.TreeChildNode(_userManager.User.OrganizeId, t => t.Id, t => t.ParentId).Select(it => it.Id).ToList());
            input.departIds.Remove("@currentOrgAndSubOrg");
        }
        if (input.departIds.Contains("@currentGradeOrg"))
        {
            if (_userManager.IsAdministrator)
            {
                input.departIds.AddRange(allOrgList.Select(it => it.Id).ToList());
            }
            else
            {
                input.departIds.AddRange(_userManager.DataScope.Select(x => x.organizeId).ToList());
            }
            input.departIds.Remove("@currentGradeOrg");
        }

        List<OrganizeListOutput>? data = await _repository.AsQueryable()
            .Where(a => a.DeleteMark == null && input.departIds.Contains(a.Id))
            .WhereIF(input.keyword.IsNotEmptyOrNull(), a => a.FullName.Contains(input.keyword) || a.EnCode.Contains(input.keyword))
            .Select(a => new OrganizeListOutput
            {
                id = a.Id,
                organizeIdTree = a.OrganizeIdTree,
                type = a.Category,
                parentId = a.ParentId,
                lastFullName = a.FullName,
                fullName = a.FullName,
                enabledMark = a.EnabledMark,
                creatorTime = a.CreatorTime,
                icon = a.Category.Equals("company") ? "icon-ym icon-ym-tree-organization3" : "icon-ym icon-ym-tree-department1",
                sortCode = a.SortCode
            }).ToListAsync();

        data.ForEach(item =>
        {
            var description = allOrgList.FirstOrDefault(x => x.Id == item.id)?.Description;
            if (!data.Any(x => x.id.Equals(item.parentId)))
            {
                item.fullName = description;
                item.parentId = "0";
            }
            var ids = new List<string>();
            ids.AddRange(item.organizeIdTree.Split(",").ToList());
            item.organizeIds = ids;
            item.organize = description;
        });

        // 组织断层处理
        data.OrderByDescending(x => x.organizeIdTree.Length).ToList().ForEach(item =>
        {
            if (!data.Any(x => x.id.Equals(item.parentId)))
            {
                var pItem = data.Find(x => x.id != item.id && item.organizeIdTree.Contains(x.organizeIdTree));
                if (pItem != null)
                {
                    item.parentId = pItem.id;
                    item.fullName = item.fullName.Replace(pItem.fullName + "/", string.Empty);
                }
                else
                {
                    item.parentId = "0";
                }
            }
            else
            {
                var pItem = data.Find(x => x.id.Equals(item.parentId));
                item.fullName = item.fullName.Replace(pItem.fullName + "/", string.Empty);
            }
        });

        return new { list = data.ToTree("0") };
    }

    /// <summary>
    /// 新建.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("")]
    public async Task Create([FromBody] OrganizeCrInput input)
    {
        if (!_userManager.DataScope.Any(it => it.organizeId == input.parentId && it.Add) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);
        if (await _repository.IsAnyAsync(o => o.EnCode == input.enCode && o.DeleteMark == null))
            throw Oops.Oh(ErrorCode.D2008);
        if (await _repository.IsAnyAsync(o => o.ParentId == input.parentId && o.FullName == input.fullName && o.Category == "company" && o.DeleteMark == null))
            throw Oops.Oh(ErrorCode.D2009);
        OrganizeEntity? entity = input.Adapt<OrganizeEntity>();
        entity.Id = SnowflakeIdHelper.NextId();
        entity.EnabledMark = input.enabledMark;
        entity.Category = "company";
        entity.PropertyJson = JSON.Serialize(input.propertyJson);

        #region 处理 上级ID列表 存储

        List<string>? idList = new List<string>();
        if (entity.ParentId != "-1")
        {
            var tree = _repository.AsSugarClient().Queryable<OrganizeEntity>().First(x => x.Id.Equals(entity.ParentId));
            List<string>? ids = tree.OrganizeIdTree.Split(",").ToList();
            idList.AddRange(ids);
        }
        idList.Add(entity.Id);
        entity.OrganizeIdTree = string.Join(",", idList);
        #endregion

        OrganizeEntity? isOk = await _repository.AsSugarClient().Insertable(entity).CallEntityMethod(m => m.Create()).ExecuteReturnEntityAsync();
        _ = isOk ?? throw Oops.Oh(ErrorCode.D2012);
        _cacheManager.Del(CommonConst.CACHEKEYORGANIZE);

        #region 默认赋予分级管理权限
        var adminlist = new List<OrganizeAdministratorEntity>();
        if (!_userManager.IsAdministrator)
        {
            adminlist.Add(new OrganizeAdministratorEntity()
            {
                UserId = _userManager.UserId,
                OrganizeId = isOk.Id,
                ThisLayerAdd = 1,
                ThisLayerDelete = 1,
                ThisLayerEdit = 1,
                ThisLayerSelect = 1,
                SubLayerAdd = 0,
                SubLayerDelete = 0,
                SubLayerEdit = 0,
                SubLayerSelect = 0
            });
        }

        var adminUserIds = _repository.AsSugarClient().Queryable<OrganizeAdministratorEntity>().Where(x => !x.UserId.Equals(_userManager.UserId)).Select(x => x.UserId).Distinct().ToList();
        adminUserIds.ForEach(userid =>
        {
            adminlist.Add(new OrganizeAdministratorEntity()
            {
                UserId = userid,
                OrganizeId = isOk.Id,
                ThisLayerAdd = 0,
                ThisLayerDelete = 0,
                ThisLayerEdit = 0,
                ThisLayerSelect = 0,
                SubLayerAdd = 0,
                SubLayerDelete = 0,
                SubLayerEdit = 0,
                SubLayerSelect = 0
            });
        });

        if (adminlist.Any()) await _repository.AsSugarClient().Insertable(adminlist).CallEntityMethod(m => m.Create()).ExecuteReturnEntityAsync();
        #endregion

        #region 第三方同步

        try
        {
            SysConfigOutput? sysConfig = await _sysConfigService.GetInfo();
            List<OrganizeListOutput>? orgList = new List<OrganizeListOutput>();
            orgList.Add(entity.Adapt<OrganizeListOutput>());
            if (sysConfig.dingSynIsSynOrg)
                await _synThirdInfoService.SynDep(2, 1, sysConfig, orgList);
            if (sysConfig.qyhIsSynOrg)
                await _synThirdInfoService.SynDep(1, 1, sysConfig, orgList);
        }
        catch (Exception)
        {
        }

        #endregion
    }

    /// <summary>
    /// 更新.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <param name="input">参数.</param>
    [HttpPut("{id}")]
    public async Task Update(string id, [FromBody] OrganizeUpInput input)
    {
        OrganizeEntity? oldEntity = await _repository.GetSingleAsync(it => it.Id == id);
        if (oldEntity.ParentId != input.parentId && !_userManager.DataScope.Any(it => it.organizeId == oldEntity.ParentId && it.Edit) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        if (oldEntity.ParentId != input.parentId && !_userManager.DataScope.Any(it => it.organizeId == input.parentId && it.Edit) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        if (!_userManager.DataScope.Any(it => it.organizeId == id && it.Edit == true) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        if (input.parentId.Equals(id))
            throw Oops.Oh(ErrorCode.D2001);
        if (input.parentId.Equals("-1") && !oldEntity.ParentId.Equals("-1") && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        // 父id不能为自己的子节点
        List<string>? childIdListById = await GetChildIdListWithSelfById(id);
        if (childIdListById.Contains(input.parentId))
            throw Oops.Oh(ErrorCode.D2001);
        if (await _repository.IsAnyAsync(o => o.EnCode == input.enCode && o.Id != id && o.DeleteMark == null && o.Id != id))
            throw Oops.Oh(ErrorCode.D2008);
        if (await _repository.IsAnyAsync(o => o.ParentId == input.parentId && o.FullName == input.fullName && o.Id != id && o.Category == "company" && o.DeleteMark == null && o.Id != id))
            throw Oops.Oh(ErrorCode.D2009);
        OrganizeEntity? entity = input.Adapt<OrganizeEntity>();
        entity.LastModifyTime = DateTime.Now;
        entity.LastModifyUserId = _userManager.UserId;
        entity.PropertyJson = JSON.Serialize(input.propertyJson);

        try
        {
            // 开启事务
            _db.BeginTran();

            #region 处理 上级ID列表 存储

            if (string.IsNullOrWhiteSpace(oldEntity.OrganizeIdTree) || entity.ParentId != oldEntity.ParentId)
            {
                List<string>? idList = new List<string>();
                if (entity.ParentId != "-1")
                {
                    var tree = _repository.AsSugarClient().Queryable<OrganizeEntity>().First(x => x.Id.Equals(entity.ParentId));
                    List<string>? ids = tree.OrganizeIdTree.Split(",").ToList();
                    idList.AddRange(ids);
                }

                idList.Add(entity.Id);
                entity.OrganizeIdTree = string.Join(",", idList);

                // 如果上级结构 变动 ，需要更改所有包含 该组织的id 的结构
                if (entity.OrganizeIdTree != oldEntity.OrganizeIdTree)
                {
                    List<OrganizeEntity>? oldEntityList = await _repository.AsQueryable().Where(x => x.OrganizeIdTree.Contains(oldEntity.OrganizeIdTree + ",") && x.Id != oldEntity.Id).ToListAsync();
                    oldEntityList.Add(oldEntity);
                    oldEntityList.ForEach(item =>
                    {
                        string? childList = item.OrganizeIdTree.Split(oldEntity.Id).LastOrDefault();
                        item.OrganizeIdTree = entity.OrganizeIdTree + childList;
                    });

                    await _repository.AsSugarClient().Updateable(oldEntityList).UpdateColumns(x => x.OrganizeIdTree).ExecuteCommandAsync(); // 批量修改 父级组织
                }
            }

            #endregion

            await _repository.AsSugarClient().Updateable(entity).IgnoreColumns(ignoreAllNullColumns: true).CallEntityMethod(m => m.LastModify()).ExecuteCommandAsync();
            _cacheManager.Del(CommonConst.CACHEKEYORGANIZE);
            _db.CommitTran();
        }
        catch (Exception)
        {
            _db.RollbackTran();
            throw Oops.Oh(ErrorCode.D2010);
        }

        #region 第三方同步

        try
        {
            SysConfigOutput? sysConfig = await _sysConfigService.GetInfo();
            List<OrganizeListOutput>? orgList = new List<OrganizeListOutput>();
            entity.Category = "company";
            orgList.Add(entity.Adapt<OrganizeListOutput>());
            if (sysConfig.dingSynIsSynOrg) await _synThirdInfoService.SynDep(2, 1, sysConfig, orgList);
            if (sysConfig.qyhIsSynOrg) await _synThirdInfoService.SynDep(1, 1, sysConfig, orgList);
        }
        catch (Exception)
        {
        }

        #endregion
    }

    /// <summary>
    /// 删除.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    public async Task Delete(string id)
    {
        if (!_userManager.DataScope.Any(it => it.organizeId == id && it.Delete == true) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        // 该机构下有机构，则不能删
        if (await _repository.IsAnyAsync(o => o.ParentId.Equals(id) && o.DeleteMark == null))
            throw Oops.Oh(ErrorCode.D2005);

        // 该机构下有岗位，则不能删
        if (await _repository.AsSugarClient().Queryable<PositionEntity>().AnyAsync(p => p.OrganizeId.Equals(id) && p.DeleteMark == null))
            throw Oops.Oh(ErrorCode.D2006);

        // 该机构下有用户，则不能删
        if (await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.ObjectType == "Organize" && x.ObjectId == id).AnyAsync())
            throw Oops.Oh(ErrorCode.D2004);

        // 该机构下有角色，则不能删
        if (await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>().Where(x => x.OrganizeId == id && x.ObjectType == "Role").AnyAsync())
            throw Oops.Oh(ErrorCode.D2020);

        try
        {
            // 开启事务
            _db.BeginTran();

            await _repository.AsSugarClient().Updateable<OrganizeEntity>().SetColumns(it => new OrganizeEntity()
            {
                DeleteMark = 1,
                DeleteTime = SqlFunc.GetDate(),
                DeleteUserId = _userManager.UserId,
            }).Where(it => it.Id == id && it.DeleteMark == null).ExecuteCommandAsync();

            // 删除该组织和角色关联数据
            await _repository.AsSugarClient().Deleteable<OrganizeRelationEntity>().Where(x => x.OrganizeId == id && x.ObjectType == "Role").ExecuteCommandAsync();
            _cacheManager.Del(CommonConst.CACHEKEYORGANIZE);
            _db.CommitTran();
        }
        catch (Exception)
        {
            _db.RollbackTran();
            throw Oops.Oh(ErrorCode.D2012);
        }

        #region 第三方同步
        try
        {
            SysConfigOutput? sysConfig = await _sysConfigService.GetInfo();
            if (sysConfig.dingSynIsSynOrg)
                await _synThirdInfoService.DelSynData(2, 1, sysConfig, id);

            if (sysConfig.qyhIsSynOrg)
                await _synThirdInfoService.DelSynData(1, 1, sysConfig, id);

        }
        catch (Exception)
        {
        }
        #endregion
    }

    /// <summary>
    /// 更新状态.
    /// </summary>
    /// <param name="id">主键</param>
    /// <returns></returns>
    [HttpPut("{id}/Actions/State")]
    public async Task UpdateState(string id)
    {
        if (!_userManager.DataScope.Any(it => it.organizeId == id && it.Edit == true) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        if (!await _repository.IsAnyAsync(u => u.Id == id && u.DeleteMark == null))
            throw Oops.Oh(ErrorCode.D2002);

        int isOk = await _repository.AsSugarClient().Updateable<OrganizeEntity>().SetColumns(it => new OrganizeEntity()
        {
            EnabledMark = SqlFunc.IIF(it.EnabledMark == 1, 0, 1),
            LastModifyUserId = _userManager.UserId,
            LastModifyTime = SqlFunc.GetDate()
        }).Where(it => it.Id == id).ExecuteCommandAsync();
        _cacheManager.Del(CommonConst.CACHEKEYORGANIZE);
        if (!(isOk > 0))
            throw Oops.Oh(ErrorCode.D2011);
    }

    #endregion

    #region PublicMethod

    /// <summary>
    /// 是否机构主管.
    /// </summary>
    /// <param name="userId">用户ID.</param>
    /// <returns></returns>
    [NonAction]
    public async Task<bool> GetIsManagerByUserId(string userId)
    {
        return await _repository.IsAnyAsync(o => o.EnabledMark == 1 && o.DeleteMark == null && o.ManagerId == userId);
    }

    /// <summary>
    /// 获取机构列表(其他服务使用).
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public async Task<List<OrganizeEntity>> GetListAsync()
    {
        return await _repository.AsQueryable().Where(t => t.EnabledMark == 1 && t.DeleteMark == null).OrderBy(o => o.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).ToListAsync();
    }

    /// <summary>
    /// 获取公司列表(其他服务使用).
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public async Task<List<OrganizeEntity>> GetCompanyListAsync()
    {
        return await _repository.AsQueryable().Where(t => t.Category.Equals("company") && t.EnabledMark == 1 && t.DeleteMark == null).OrderBy(o => o.SortCode).ToListAsync();
    }

    /// <summary>
    /// 下属机构.
    /// </summary>
    /// <param name="organizeId">机构ID.</param>
    /// <param name="isAdmin">是否管理员.</param>
    /// <returns></returns>
    [NonAction]
    public async Task<string[]> GetSubsidiary(string organizeId, bool isAdmin)
    {
        List<OrganizeEntity>? data = await _repository.AsQueryable().Where(o => o.DeleteMark == null && o.EnabledMark == 1).OrderBy(o => o.SortCode).ToListAsync();
        if (!isAdmin)
            data = data.TreeChildNode(organizeId, t => t.Id, t => t.ParentId);
        return data.Select(m => m.Id).ToArray();
    }

    /// <summary>
    /// 下属机构.
    /// </summary>
    /// <param name="organizeId">机构ID.</param>
    /// <returns></returns>
    [NonAction]
    public async Task<List<string>> GetSubsidiary(string organizeId)
    {
        List<OrganizeEntity>? data = await _repository.AsQueryable().Where(o => o.DeleteMark == null && o.EnabledMark == 1).OrderBy(o => o.SortCode).ToListAsync();
        data = data.TreeChildNode(organizeId, t => t.Id, t => t.ParentId);
        return data.Select(m => m.Id).ToList();
    }

    /// <summary>
    /// 根据节点Id获取所有子节点Id集合，包含自己.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    [NonAction]
    public async Task<List<string>> GetChildIdListWithSelfById(string id)
    {
        List<string>? childIdList = await _repository.AsQueryable().Where(u => u.ParentId.Contains(id) && u.DeleteMark == null).Select(u => u.Id).ToListAsync();
        childIdList.Add(id);
        return childIdList;
    }

    /// <summary>
    /// 获取机构成员列表 .
    /// </summary>
    /// <param name="organizeId">机构ID.</param>
    /// <returns></returns>
    [NonAction]
    public async Task<List<OrganizeMemberListOutput>> GetOrganizeMemberList(string organizeId)
    {
        List<OrganizeMemberListOutput>? output = new List<OrganizeMemberListOutput>();
        if (organizeId.Equals("0"))
        {
            List<OrganizeEntity>? data = await _repository.AsQueryable()
                .Where(o => o.DeleteMark == null && o.EnabledMark == 1 && o.ParentId.Equals("-1")).OrderBy(o => o.SortCode).ToListAsync();
            data.ForEach(o =>
            {
                output.Add(new OrganizeMemberListOutput
                {
                    id = o.Id,
                    parentId = o.ParentId,
                    fullName = o.FullName,
                    enabledMark = o.EnabledMark,
                    type = o.Category,
                    organize = o.FullName,
                    icon = o.Category.Equals("company") ? "icon-ym icon-ym-tree-organization3" : "icon-ym icon-ym-tree-department1",
                    organizeIdTree = o.OrganizeIdTree,
                    hasChildren = true,
                    isLeaf = false
                });
            });
        }
        else
        {
            var relationList = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.ObjectType.Equals("Organize") && x.ObjectId.Equals(organizeId)).Select(x => x.UserId).ToListAsync();
            List<UserEntity>? userList = await _repository.AsSugarClient().Queryable<UserEntity>().Where(u => relationList.Contains(u.Id) && u.EnabledMark > 0 && u.DeleteMark == null).OrderBy(o => o.SortCode).ToListAsync();
            userList.ForEach(u =>
            {
                output.Add(new OrganizeMemberListOutput()
                {
                    id = u.Id,
                    fullName = u.RealName + "/" + u.Account,
                    enabledMark = u.EnabledMark,
                    type = "user",
                    icon = "icon-ym icon-ym-tree-user2",
                    headIcon = "/api/File/Image/userAvatar/" + u.HeadIcon,
                    hasChildren = false,
                    isLeaf = true,
                    isAdministrator = u.IsAdministrator,
                });
            });
            List<OrganizeEntity>? departmentList = await _repository.AsQueryable()
                .Where(o => o.ParentId.Equals(organizeId) && o.DeleteMark == null && o.EnabledMark == 1).OrderBy(o => o.SortCode).ToListAsync();
            departmentList.ForEach(o =>
            {
                output.Add(new OrganizeMemberListOutput()
                {
                    id = o.Id,
                    parentId = o.ParentId,
                    fullName = o.FullName,
                    enabledMark = o.EnabledMark,
                    type = o.Category,
                    icon = o.Category.Equals("company") ? "icon-ym icon-ym-tree-organization3" : "icon-ym icon-ym-tree-department1",
                    hasChildren = true,
                    organizeIdTree = o.OrganizeIdTree,
                    isLeaf = false
                });
            });
        }

        // 获取组织树
        var orgTree = GetOrgListTreeName();

        // 组织断层处理
        output.Where(x => x.parentId != "-1" && x.organizeIdTree.IsNotEmptyOrNull()).OrderByDescending(x => x.organizeIdTree.Length).ToList().ForEach(item =>
        {
            item.fullName = orgTree.FirstOrDefault(x => x.Id.Equals(item.id))?.Description;
            item.organize = item.fullName;
            if (!output.Any(x => x.id.Equals(item.parentId)))
            {
                var pItem = output.Find(x => x.organizeIdTree.IsNotEmptyOrNull() && x.id != item.id && item.organizeIdTree.Contains(x.organizeIdTree));
                if (pItem != null)
                {
                    item.parentId = pItem.id;
                    item.fullName = item.fullName.Replace(pItem.fullName + "/", string.Empty);
                }
                else
                {
                    item.parentId = "-1";
                }
            }
            else
            {
                var pItem = output.Find(x => x.id.Equals(item.parentId));
                item.fullName = item.fullName.Replace(pItem.fullName + "/", string.Empty);
            }
        });

        if (!organizeId.Equals("0"))
        {
            var pOrgTreeName = orgTree.Find(x => x.Id.Equals(organizeId)).Description;
            output.ForEach(item => item.fullName = item.fullName.Replace(pOrgTreeName + "/", string.Empty));
        }

        return output;
    }

    /// <summary>
    /// 信息.
    /// </summary>
    /// <param name="Id"></param>
    /// <returns></returns>
    [NonAction]
    public async Task<OrganizeEntity> GetInfoById(string Id)
    {
        return await _repository.GetSingleAsync(p => p.Id == Id);
    }

    /// <summary>
    /// 获取组织下所有子组织.
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<List<string>> GetChildOrgId(string id)
    {
        return (await _repository.GetListAsync(x => x.OrganizeIdTree.Contains(id) && x.EnabledMark == 1 && x.DeleteMark == null)).Select(x => x.Id).ToList();
    }

    /// <summary>
    /// 处理组织树 名称.
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public List<OrganizeEntity> GetOrgListTreeName()
    {
        List<OrganizeEntity>? orgTreeNameList = new List<OrganizeEntity>();

        if (_cacheManager.Exists(CommonConst.CACHEKEYORGANIZE))
        {
            orgTreeNameList = _cacheManager.Get(CommonConst.CACHEKEYORGANIZE).ToObject<List<OrganizeEntity>>();
        }
        else
        {
            List<OrganizeEntity>? orgList = _repository.AsSugarClient().Queryable<OrganizeEntity>().Where(x => x.DeleteMark == null && x.EnabledMark == 1)
            .Select(x => new OrganizeEntity()
            {
                Id = x.Id,
                ParentId = x.ParentId,
                FullName = x.FullName,
                Category = x.Category,
                OrganizeIdTree = x.OrganizeIdTree,
                ManagerId = x.ManagerId,
            }).ToList();
            orgList.ForEach(item =>
            {
                if (item.OrganizeIdTree.IsNullOrEmpty()) item.OrganizeIdTree = item.Id;
                OrganizeEntity? newItem = item.Adapt<OrganizeEntity>();
                newItem.Id = item.Id;
                var orgNameList = new List<string>();
                item.OrganizeIdTree.Split(",").ToList().ForEach(it =>
                {
                    var org = orgList.Find(x => x.Id == it);
                    if (org != null) orgNameList.Add(org.FullName);
                });
                newItem.Description = string.Join("/", orgNameList);
                orgTreeNameList.Add(newItem);
            });
            _cacheManager.Set(CommonConst.CACHEKEYORGANIZE, orgTreeNameList, TimeSpan.FromMinutes(1));
        }

        return orgTreeNameList;
    }

    /// <summary>
    /// 构造 OrganizeIdTree.
    /// </summary>
    private void StructureOrganizeIdTree()
    {

        if (_repository.IsAny(x => SqlFunc.IsNullOrEmpty(SqlFunc.ToString(x.OrganizeIdTree))))
        {
            var orgList = _repository.GetList(x => SqlFunc.IsNullOrEmpty(x.OrganizeIdTree));

            foreach (var item in orgList)
            {
                if (item.ParentId == "-1")
                {
                    item.OrganizeIdTree = item.Id;
                }
                else
                {
                    var plist = GetOrganizeIdTree(item.Id);
                    plist.Reverse();
                    item.OrganizeIdTree = string.Join(",", plist);
                }
            }

            _repository.AsUpdateable(orgList).ExecuteCommand();
        }
    }
    private List<string> GetOrganizeIdTree(string id)
    {
        var res = new List<string>() { id };
        var entity = _repository.AsQueryable().First(x => x.Id.Equals(id));
        if (entity != null && !entity.ParentId.Equals("-1")) res.AddRange(GetOrganizeIdTree(entity.ParentId));
        return res;
    }

    /// <summary>
    /// 获取选中组织、岗位、角色、分组、用户基本信息.
    /// </summary>
    /// <param name="input">参数.</param>
    public async Task<dynamic> GetSelectedList(UserSelectedInput input)
    {
        if (input.ids == null) return new { list = new List<UserSelectedOutput>() };

        var objIds = new List<string>();
        input.ids.Where(x => x.IsNotEmptyOrNull()).ToList().ForEach(item => objIds.Add(item.Split("--").First().Split(',').Last()));
        var orgInfoList = GetOrgListTreeName();

        var orgList = new List<OrganizeEntity>();
        var posList = new List<PositionEntity>();
        var roleList = new List<RoleEntity>();
        var groupList = new List<GroupEntity>();
        var userList = new List<UserEntity>();
        foreach (var item in objIds)
        {
            var org = orgInfoList.FirstOrDefault(x => item.Equals(x.Id));
            if (org.IsNotEmptyOrNull()) orgList.Add(org);
            var pos = await _repository.AsSugarClient().Queryable<PositionEntity>().FirstAsync(x => item.Equals(x.Id) && x.DeleteMark == null);
            if (pos.IsNotEmptyOrNull()) posList.Add(pos);
            var role = await _repository.AsSugarClient().Queryable<RoleEntity>().FirstAsync(x => item.Equals(x.Id) && x.DeleteMark == null);
            if (role.IsNotEmptyOrNull()) roleList.Add(role);
            var group = await _repository.AsSugarClient().Queryable<GroupEntity>().FirstAsync(x => item.Equals(x.Id) && x.DeleteMark == null);
            if (group.IsNotEmptyOrNull()) groupList.Add(group);
            var user = await _repository.AsSugarClient().Queryable<UserEntity>().FirstAsync(x => item.Equals(x.Id) && x.DeleteMark == null);
            if (user.IsNotEmptyOrNull()) userList.Add(user);
        }
        var resList = new List<UserSelectedOutput>();

        orgList.ForEach(item =>
        {
            resList.Add(new UserSelectedOutput()
            {
                id = item.Id,
                fullName = item.FullName,
                type = item.Category,
                icon = item.Category.Equals("company") ? "icon-ym icon-ym-tree-organization3" : "icon-ym icon-ym-tree-department1",
                organize = item.Description,
                organizeIds = item.OrganizeIdTree.Split(',').ToList(),
            });
        });

        posList.ForEach(item =>
        {
            resList.Add(new UserSelectedOutput()
            {
                id = item.Id,
                fullName = item.FullName,
                type = "position",
                icon = "icon-ym icon-ym-tree-position1",
                organize = orgInfoList.Find(x => x.Id.Equals(item.OrganizeId)).Description,
                organizeIds = orgInfoList.Find(x => x.Id.Equals(item.OrganizeId)).OrganizeIdTree.Split(',').ToList()
            });
        });

        var roleOrgList = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>().Where(x => roleList.Select(xx => xx.Id).Contains(x.ObjectId)).Select(x => new { x.ObjectId, x.OrganizeId }).ToListAsync();
        roleList.ForEach(item =>
        {
            resList.Add(new UserSelectedOutput()
            {
                id = item.Id,
                fullName = item.FullName,
                type = "role",
                organize = SqlFunc.IIF(item.GlobalMark == 1, "", string.Join(",", orgInfoList.Where(o => roleOrgList.Where(x => x.ObjectId.Equals(item.Id)).Select(x => x.OrganizeId).Contains(o.Id)).Select(x => x.Description))),
                icon = "icon-ym icon-ym-generator-role",
                organizeIds = orgInfoList.Where(o => roleOrgList.Where(x => x.ObjectId.Equals(item.Id)).Select(x => x.OrganizeId).Contains(o.Id)).Select(x => x.OrganizeIdTree).ToList(),
            });
        });

        groupList.ForEach(item =>
        {
            resList.Add(new UserSelectedOutput()
            {
                id = item.Id,
                fullName = item.FullName,
                type = "group",
                icon = "icon-ym icon-ym-generator-group1"
            });
        });

        var userOrgList = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => userList.Select(xx => xx.Id).Contains(x.UserId) && x.ObjectType.Equals("Organize")).Select(x => new { x.ObjectId, x.UserId }).ToListAsync();
        userList.ForEach(item =>
        {
            resList.Add(new UserSelectedOutput()
            {
                id = item.Id,
                fullName = item.RealName + "/" + item.Account,
                type = "user",
                icon = "icon-ym icon-ym-tree-user2",
                headIcon = "/api/File/Image/userAvatar/" + item.HeadIcon,
                organize = string.Join(",", orgInfoList.Where(o => userOrgList.Where(x => x.UserId.Equals(item.Id)).Select(x => x.ObjectId).Contains(o.Id)).Select(x => x.Description)),
                organizeIds = orgInfoList.Where(o => userOrgList.Where(x => x.UserId.Equals(item.Id)).Select(x => x.ObjectId).Contains(o.Id)).Select(x => x.OrganizeIdTree).ToList(),
            });
        });

        if (objIds.Contains("@currentOrg"))
        {
            resList.Add(new UserSelectedOutput()
            {
                id = "@currentOrg",
                fullName = "当前组织",
                type = "system"
            });
        }
        if (objIds.Contains("@currentOrgAndSubOrg"))
        {
            resList.Add(new UserSelectedOutput()
            {
                id = "@currentOrgAndSubOrg",
                fullName = "当前组织及子组织",
                type = "system"
            });
        }
        if (objIds.Contains("@currentGradeOrg"))
        {
            resList.Add(new UserSelectedOutput()
            {
                id = "@currentGradeOrg",
                fullName = "当前分管组织",
                type = "system"
            });
        }

        return new { list = resList.OrderBy(x => objIds.IndexOf(x.id)) };
    }
    #endregion

    #region 导出和导入

    /// <summary>
    /// 导出Excel.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpGet("ExportData")]
    public async Task<dynamic> ExportData([FromQuery] OrganizeExportDataInput input)
    {
        if (input.dataType.Equals("1")) input.dataType = "company";
        else if (input.dataType.Equals("2")) input.dataType = "department";
        else input.dataType = null;

        // 获取分级管理组织
        var dataScope = _userManager.DataScope.Where(x => x.Select).Select(x => x.organizeId).Distinct().ToList();

        var orgList = GetOrgListTreeName();
        var managerList = await _repository.AsSugarClient().Queryable<UserEntity>().Where(x => orgList.Select(x => x.ManagerId).Contains(x.Id) && x.DeleteMark == null && x.EnabledMark.Equals(1)).Select(x => new { id = x.Id, realName = x.RealName, account = x.Account }).ToListAsync();

        DictionaryTypeEntity? typeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "9b542177a477488994ce09fff3c93901" || x.EnCode == "EnterpriseNature") && x.DeleteMark == null).FirstAsync();
        List<DictionaryDataEntity>? _enterpriseNatureList = await _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).ToListAsync(); // 公司性质

        typeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "d59a3cf65f9847dbb08be449e3feae16" || x.EnCode == "IndustryType") && x.DeleteMark == null).FirstAsync();
        List<DictionaryDataEntity>? _industryList = await _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).ToListAsync(); // 所属行业

        var dataList = await _repository.AsQueryable().Where(t => t.DeleteMark == null)
            .WhereIF(!_userManager.IsAdministrator, a => dataScope.Contains(a.Id))
            .WhereIF(input.dataType.IsNotEmptyOrNull(), a => a.Category.Equals(input.dataType))
            .OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).Select(x => new {
                id = x.Id,
                category = x.Category,
                parentId = x.ParentId,
                fullName = x.FullName,
                currOrganize = x.FullName,
                enCode = x.EnCode,
                managerId = x.ManagerId,
                sortCode = x.SortCode,
                description = x.Description,
                propertyJson = x.PropertyJson
            }).ToListAsync();

        var data = new List<OrganizeListImportDataInput>();
        foreach (var item in dataList)
        {
            var addItem = item.propertyJson?.ToObject<OrganizeListImportDataInput>();
            if (addItem == null) addItem = new OrganizeListImportDataInput();
            addItem.category = item.category.Equals("company") ? "公司" : "部门";
            var manager = managerList.FirstOrDefault(x => x.id.Equals(item.managerId));
            if (manager != null) addItem.managerId = string.Format("{0}/{1}", manager.realName, manager.account);
            addItem.fullName = item.fullName;
            if (item.parentId != "-1") addItem.parentId = orgList.Find(x => x.Id.Equals(item.id)).Description.Replace("/" + item.fullName, "");
            else addItem.parentId = string.Empty;
            if (addItem.foundedTime.IsNotEmptyOrNull()) addItem.foundedTime = addItem.foundedTime.TimeStampToDateTime().ToString("yyyy-MM-dd");
            addItem.enCode = item.enCode;
            addItem.sortCode = item.sortCode.ToString();
            addItem.description = item.description;
            addItem.industry = _industryList.Find(x => x.Id.Equals(addItem.industry))?.FullName;
            addItem.enterpriseNature = _enterpriseNatureList.Find(x => x.Id.Equals(addItem.enterpriseNature))?.FullName;
            data.Add(addItem);
        }

        ExcelConfig excelconfig = new ExcelConfig();
        excelconfig.FileName = string.Format("{1}_{0:yyyyMMddhhmmss}.xls", DateTime.Now, "组织信息");
        excelconfig.HeadFont = "微软雅黑";
        excelconfig.HeadPoint = 10;
        excelconfig.IsAllSizeColumn = true;
        excelconfig.IsBold = true;
        excelconfig.IsAllBorder = true;
        excelconfig.ColumnModel = new List<ExcelColumnModel>();
        var columnList = new List<ExportImportHelperModel>();
        if (input.dataType != null && input.dataType.Equals("company")) columnList.AddRange(GetComInfoFieldToTitle(input.selectKey.Split(',').ToList()));
        else if (input.dataType != null && input.dataType.Equals("department")) columnList.AddRange(GetDepInfoFieldToTitle(input.selectKey.Split(',').ToList()));
        else columnList.AddRange(GetOrgInfoFieldToTitle(input.selectKey.Split(',').ToList()));

        foreach (var item in columnList)
        {
            excelconfig.ColumnModel.Add(new ExcelColumnModel() { Column = item.ColumnKey, ExcelColumn = item.ColumnValue, Required = item.Required });
        }

        string? addPath = Path.Combine(FileVariable.TemporaryFilePath, excelconfig.FileName);
        var fs = ExcelExportHelper<OrganizeListImportDataInput>.ExportMemoryStream(data, excelconfig);
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
        List<OrganizeListImportDataInput>? dataList = new List<OrganizeListImportDataInput>() {
            new OrganizeListImportDataInput(){ fullName = "公司名称/公司名称1" , foundedTime = "yyyy-MM-dd" },
            new OrganizeListImportDataInput(){ fullName = "公司名称/公司名称1/部门名称" , managerId = "姓名/账号" }};

        ExcelConfig excelconfig = new ExcelConfig();
        excelconfig.FileName = "组织信息导入模板.xls";
        excelconfig.HeadFont = "微软雅黑";
        excelconfig.HeadPoint = 10;
        excelconfig.IsAllSizeColumn = true;
        excelconfig.IsBold = true;
        excelconfig.IsAllBorder = true;
        excelconfig.IsAnnotation = true;
        excelconfig.ColumnModel = new List<ExcelColumnModel>();
        var infoFields = GetImportInfoFieldToTitle();
        infoFields.RemoveAll(x => x.ColumnKey.Equals("errorsInfo"));
        foreach (var item in infoFields)
        {
            excelconfig.ColumnModel.Add(new ExcelColumnModel() { Column = item.ColumnKey, ExcelColumn = item.ColumnValue, Required = item.Required, SelectList = item.SelectList });
        }

        string? addPath = Path.Combine(FileVariable.TemporaryFilePath, excelconfig.FileName);
        var stream = ExcelExportHelper<OrganizeListImportDataInput>.ToStream(dataList, excelconfig);
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
            var FileEncode = GetImportInfoFieldToTitle();

            string? filePath = FileVariable.TemporaryFilePath;
            string? savePath = Path.Combine(filePath, fileName);

            // 得到数据
            var sr = await _fileManager.GetFileStream(savePath);
            excelData = ExcelImportHelper.ToDataTable(savePath, sr);
            foreach (var it in excelData.Columns)
            {
                var item = it as DataColumn;
                if (item.ColumnName.Equals("errorsInfo")) throw Oops.Oh(ErrorCode.D1807);
                if (!FileEncode.Any(x => x.ColumnKey == item.ToString() && x.ColumnValue.Equals(item.Caption.Replace("*", "")))) throw Oops.Oh(ErrorCode.D1807);
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
    public async Task<dynamic> ExportExceptionData([FromBody] OrganizeImportDataInput list)
    {
        list.list.ForEach(it => it.errorsInfo = string.Empty);
        object[]? res = await DataImport(list.list);

        // 错误数据
        List<OrganizeListImportDataInput>? errorlist = res.Last() as List<OrganizeListImportDataInput>;

        ExcelConfig excelconfig = new ExcelConfig();
        excelconfig.FileName = string.Format("组织信息导入模板错误报告_{0}.xls", DateTime.Now.ToString("yyyyMMddHHmmss"));
        excelconfig.HeadFont = "微软雅黑";
        excelconfig.HeadPoint = 10;
        excelconfig.IsAllSizeColumn = true;
        excelconfig.IsBold = true;
        excelconfig.IsAllBorder = true;
        excelconfig.IsAnnotation = true;
        excelconfig.ColumnModel = new List<ExcelColumnModel>();
        foreach (var item in GetImportInfoFieldToTitle())
        {
            excelconfig.ColumnModel.Add(new ExcelColumnModel() { Column = item.ColumnKey, ExcelColumn = item.ColumnValue, Required = item.Required, FreezePane = item.FreezePane, SelectList = item.SelectList });
        }

        string? addPath = Path.Combine(FileVariable.TemporaryFilePath, excelconfig.FileName);
        ExcelExportHelper<OrganizeListImportDataInput>.Export(errorlist, excelconfig, addPath);

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
    public async Task<dynamic> ImportData([FromBody] OrganizeImportDataInput list)
    {
        list.list.ForEach(x => x.errorsInfo = string.Empty);
        object[]? res = await DataImport(list.list);
        List<OrganizeEntity>? addlist = res.First() as List<OrganizeEntity>;
        List<OrganizeListImportDataInput>? errorlist = res.Last() as List<OrganizeListImportDataInput>;
        return new { snum = addlist.Count, fnum = errorlist.Count, failResult = errorlist, resultType = errorlist.Count < 1 ? 0 : 1 };
    }

    /// <summary>
    /// 导入数据函数.
    /// </summary>
    /// <param name="list">list.</param>
    /// <returns>[成功列表,失败列表].</returns>
    private async Task<object[]> DataImport(List<OrganizeListImportDataInput> list)
    {
        List<OrganizeListImportDataInput> inputList = list;

        if (inputList == null || inputList.Count() < 1)
            throw Oops.Oh(ErrorCode.D5019);
        if (inputList.Count > 1000)
            throw Oops.Oh(ErrorCode.D5029);

        // 是否存在顶级组织
        var isPOrg = _repository.IsAny(x => x.ParentId.Equals("-1") && x.EnabledMark.Equals(1) && x.DeleteMark == null);

        List<OrganizeEntity> addList = new List<OrganizeEntity>();

        var orgList = GetOrgListTreeName();

        DictionaryTypeEntity? typeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "9b542177a477488994ce09fff3c93901" || x.EnCode == "EnterpriseNature") && x.DeleteMark == null).FirstAsync();
        List<DictionaryDataEntity>? _enterpriseNatureList = await _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id).ToListAsync(); // 公司性质

        typeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "d59a3cf65f9847dbb08be449e3feae16" || x.EnCode == "IndustryType") && x.DeleteMark == null).FirstAsync();
        List<DictionaryDataEntity>? _industryList = await _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id).ToListAsync(); // 所属行业

        var regexEnCode = @"^[a-zA-Z0-9.]*$";
        foreach (var item in inputList)
        {
            if (item.category.IsNullOrWhiteSpace())
            {
                item.errorsInfo += "类型不能为空,";
                continue;
            }
            if (item.category.IsNotEmptyOrNull() && !item.category.Equals("公司") && !item.category.Equals("部门"))
            {
                item.errorsInfo += "找不到该类型值,";
                continue;
            }
            if (item.fullName.IsNullOrWhiteSpace()) item.errorsInfo += "名称不能为空,";
            else if (item.fullName.Length > 50) item.errorsInfo += "名称值超出最多输入字符限制,";
            if (item.enCode.IsNullOrWhiteSpace()) item.errorsInfo += "编码不能为空,";
            else if (item.enCode.Length > 50) item.errorsInfo += "编码值超出最多输入字符限制,";
            if (item.enCode.IsNotEmptyOrNull() && (!Regex.IsMatch(item.enCode, regexEnCode) || item.enCode.First().Equals('.') || item.enCode.Last().Equals('.')))
                item.errorsInfo += "编码值只能输入英文、数字和小数点且小数点不能放在首尾,";

            if (isPOrg && item.fullName.IsNotEmptyOrNull() && item.fullName.Equals("顶级节点")) item.errorsInfo += "顶级公司已存在,";
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

            if (item.fullName.IsNotEmptyOrNull() && inputList.Count(x => x.fullName == item.fullName) > 1)
            {
                var errorItems = inputList.Where(x => x.fullName == item.fullName && x.category == item.category).ToList();
                for (var i = 1; i < errorItems.Count; i++) if (!item.errorsInfo.Contains("名称值已存在")) errorItems[i].errorsInfo += "名称值已存在,";
            }
            if (item.enCode.IsNotEmptyOrNull() && inputList.Count(x => x.enCode == item.enCode) > 1)
            {
                var errorItems = inputList.Where(x => x.enCode == item.enCode).ToList();
                for (var i = 1; i < errorItems.Count; i++) if (!item.errorsInfo.Contains("编码值已存在")) errorItems[i].errorsInfo += "编码值已存在,";
            }

            OrganizeEntity entity = new OrganizeEntity();
            entity.Id = SnowflakeIdHelper.NextId();
            if (item.category == null || (!item.category.Equals("公司") && !item.category.Equals("部门"))) entity.Category = string.Empty;
            else entity.Category = item.category.Equals("公司") ? "company" : "department";
            entity.Description = item.description;
            if (item.fullName.IsNotEmptyOrNull() && (entity.Category.Equals("company") || entity.Category.Equals("department")))
            {
                var pName = item.fullName.Split('/').ToList();
                entity.FullName = pName.Last();
                pName.Remove(pName.Last());
                if (!isPOrg && pName.Count == 1)
                {
                    entity.ParentId = "-1";
                    if (orgList.Any(o => o.ParentId == entity.ParentId && o.FullName == entity.FullName && o.Category.Equals(entity.Category) && o.DeleteMark == null))
                    {
                        if (!item.errorsInfo.Contains("名称值已存在"))
                            item.errorsInfo += "名称值已存在,";
                    }
                }
                else if (!item.fullName.Equals("顶级节点"))
                {
                    entity.ParentId = orgList.WhereIF(entity.Category.Equals("company"), x => x.Category.Equals("company"))
                        .Where(x => x.Description.Equals(string.Join("/", pName))).FirstOrDefault()?.Id;
                    if (entity.ParentId.IsNullOrEmpty())
                    {
                        item.errorsInfo += entity.Category.Equals("company") && pName.Any() ? "部门下不能创建公司," : "找不到该所属组织,";
                    }
                    else
                    {
                        if (orgList.Any(o => o.ParentId == entity.ParentId && o.FullName == entity.FullName && o.Category.Equals(entity.Category) && o.DeleteMark == null))
                        {
                            if (!item.errorsInfo.Contains("名称值已存在"))
                                item.errorsInfo += "名称值已存在,";
                        }
                    }
                }
            }

            if (item.enCode.IsNotEmptyOrNull() && orgList.Any(o => o.EnCode == item.enCode && o.DeleteMark == null))
            {
                if (!item.errorsInfo.Contains("编码值已存在"))
                    item.errorsInfo += "编码值已存在,";
            }
            entity.EnCode = item.enCode;
            entity.SortCode = item.sortCode.IsNotEmptyOrNull() ? item.sortCode.ParseToLong() : 0;

            if (entity.Category.Equals("company"))
            {
                var newItem = item.Copy();

                if (item.foundedTime.IsNotEmptyOrNull() && entity.Category.Equals("company"))
                {
                    try
                    {
                        newItem.foundedTime = DateTime.Parse(item.foundedTime).ParseToUnixTime().ToString();
                    }
                    catch
                    {
                        item.errorsInfo += "成立时间值不正确,";
                    }
                }

                // 公司性质
                if (item.enterpriseNature.IsNotEmptyOrNull() && !_enterpriseNatureList.Any(x => x.FullName == item.enterpriseNature))
                {
                    item.errorsInfo += "找不到该公司性质值,";
                }
                else
                {
                    newItem.enterpriseNature = _enterpriseNatureList.Find(x => x.FullName == item.enterpriseNature)?.Id;
                }

                // 所属行业
                if (item.industry.IsNotEmptyOrNull() && !_industryList.Any(x => x.FullName == item.industry))
                {
                    item.errorsInfo += "找不到该所属行业值,";
                }
                else
                {
                    newItem.industry = _industryList.Find(x => x.FullName == item.industry)?.Id;
                }

                entity.PropertyJson = newItem.Adapt<OrganizePropertyModel>().ToJsonString();
            }
            else if (entity.Category.Equals("department") && item.managerId.IsNotEmptyOrNull())
            {
                var account = item.managerId.Split("/").Last();
                var mId = _repository.AsSugarClient().Queryable<UserEntity>().Where(x => x.Account.Equals(account)).Select(x => x.Id).First();
                if (mId.IsNullOrEmpty())
                {
                    item.errorsInfo += "找不到该部门主管,";
                }
                else
                {
                    entity.ManagerId = mId;
                }
            }

            if (item.errorsInfo.IsNullOrEmpty()) addList.Add(entity);
        }

        if (addList.Any())
        {
            try
            {
                // 新增记录
                var newEntity = await _repository.AsInsertable(addList).CallEntityMethod(m => m.Create()).ExecuteReturnEntityAsync();
                _cacheManager.Del(CommonConst.CACHEKEYORGANIZE);
            }
            catch (Exception)
            {
                inputList.ForEach(item => item.errorsInfo = "系统异常");
                inputList = new List<OrganizeListImportDataInput>();
            }
        }

        var errorList = new List<OrganizeListImportDataInput>();
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
    /// 字段对应 列名称 导入.
    /// </summary>
    /// <returns></returns>
    private List<ExportImportHelperModel> GetImportInfoFieldToTitle(List<string> fields = null)
    {
        // 公司性质
        DictionaryTypeEntity? typeEntity = _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "9b542177a477488994ce09fff3c93901" || x.EnCode == "EnterpriseNature") && x.DeleteMark == null).First();
        var _enterpriseNatureList = _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).Select(x => x.FullName).ToList();

        // 所属行业
        typeEntity = _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "d59a3cf65f9847dbb08be449e3feae16" || x.EnCode == "IndustryType") && x.DeleteMark == null).First();
        var _industryList = _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).Select(x => x.FullName).ToList();

        var res = new List<ExportImportHelperModel>();
        res.Add(new ExportImportHelperModel() { ColumnKey = "errorsInfo", ColumnValue = "异常原因", FreezePane = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "category", ColumnValue = "类型", Required = true, SelectList = new List<string>() { "公司", "部门" } });
        res.Add(new ExportImportHelperModel() { ColumnKey = "fullName", ColumnValue = "名称", Required = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "enCode", ColumnValue = "编码", Required = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "shortName", ColumnValue = "公司简称" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "enterpriseNature", ColumnValue = "公司性质", SelectList = _enterpriseNatureList });
        res.Add(new ExportImportHelperModel() { ColumnKey = "industry", ColumnValue = "所属行业", SelectList = _industryList });
        res.Add(new ExportImportHelperModel() { ColumnKey = "foundedTime", ColumnValue = "成立时间" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "telePhone", ColumnValue = "公司电话" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "fax", ColumnValue = "公司传真" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "webSite", ColumnValue = "公司主页" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "address", ColumnValue = "公司地址" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "managerName", ColumnValue = "公司法人" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "managerTelePhone", ColumnValue = "联系电话" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "managerMobilePhone", ColumnValue = "联系手机" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "manageEmail", ColumnValue = "联系邮箱" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "bankName", ColumnValue = "开户银行" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "bankAccount", ColumnValue = "银行账户" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "businessscope", ColumnValue = "经营范围" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "managerId", ColumnValue = "部门主管" });
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

    /// <summary>
    /// 导出字段对应 列名称 公司和部门.
    /// </summary>
    /// <returns></returns>
    private List<ExportImportHelperModel> GetOrgInfoFieldToTitle(List<string> fields = null)
    {
        // 公司性质
        DictionaryTypeEntity? typeEntity = _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "9b542177a477488994ce09fff3c93901" || x.EnCode == "EnterpriseNature") && x.DeleteMark == null).First();
        var _enterpriseNatureList = _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).Select(x => x.FullName).ToList();

        // 所属行业
        typeEntity = _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "d59a3cf65f9847dbb08be449e3feae16" || x.EnCode == "IndustryType") && x.DeleteMark == null).First();
        var _industryList = _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).Select(x => x.FullName).ToList();

        var res = new List<ExportImportHelperModel>();
        res.Add(new ExportImportHelperModel() { ColumnKey = "category", ColumnValue = "类型", Required = true, SelectList = new List<string>() { "公司", "部门" } });
        res.Add(new ExportImportHelperModel() { ColumnKey = "parentId", ColumnValue = "所属组织" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "fullName", ColumnValue = "名称" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "enCode", ColumnValue = "编码" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "shortName", ColumnValue = "公司简称" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "enterpriseNature", ColumnValue = "公司性质", SelectList = _enterpriseNatureList });
        res.Add(new ExportImportHelperModel() { ColumnKey = "industry", ColumnValue = "所属行业", SelectList = _industryList });
        res.Add(new ExportImportHelperModel() { ColumnKey = "foundedTime", ColumnValue = "成立时间" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "telePhone", ColumnValue = "公司电话" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "fax", ColumnValue = "公司传真" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "webSite", ColumnValue = "公司主页" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "address", ColumnValue = "公司地址" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "managerName", ColumnValue = "公司法人" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "managerTelePhone", ColumnValue = "联系电话" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "managerMobilePhone", ColumnValue = "联系手机" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "manageEmail", ColumnValue = "联系邮箱" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "bankName", ColumnValue = "开户银行" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "bankAccount", ColumnValue = "银行账户" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "businessscope", ColumnValue = "经营范围" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "managerId", ColumnValue = "部门主管" });
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

    /// <summary>
    /// 导出字段对应 列名称 公司.
    /// </summary>
    /// <returns></returns>
    private List<ExportImportHelperModel> GetComInfoFieldToTitle(List<string> fields = null)
    {
        // 公司性质
        DictionaryTypeEntity? typeEntity = _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "9b542177a477488994ce09fff3c93901" || x.EnCode == "EnterpriseNature") && x.DeleteMark == null).First();
        var _enterpriseNatureList = _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).Select(x => x.FullName).ToList();

        // 所属行业
        typeEntity = _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "d59a3cf65f9847dbb08be449e3feae16" || x.EnCode == "IndustryType") && x.DeleteMark == null).First();
        var _industryList = _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).Select(x => x.FullName).ToList();

        var res = new List<ExportImportHelperModel>();
        res.Add(new ExportImportHelperModel() { ColumnKey = "category", ColumnValue = "类型", Required = true, SelectList = new List<string>() { "公司", "部门" } });
        res.Add(new ExportImportHelperModel() { ColumnKey = "parentId", ColumnValue = "上级公司" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "fullName", ColumnValue = "公司名称" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "enCode", ColumnValue = "公司编码" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "shortName", ColumnValue = "公司简称" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "enterpriseNature", ColumnValue = "公司性质", SelectList = _enterpriseNatureList });
        res.Add(new ExportImportHelperModel() { ColumnKey = "industry", ColumnValue = "所属行业", SelectList = _industryList });
        res.Add(new ExportImportHelperModel() { ColumnKey = "foundedTime", ColumnValue = "成立时间" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "telePhone", ColumnValue = "公司电话" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "fax", ColumnValue = "公司传真" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "webSite", ColumnValue = "公司主页" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "address", ColumnValue = "公司地址" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "managerName", ColumnValue = "公司法人" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "managerTelePhone", ColumnValue = "联系电话" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "managerMobilePhone", ColumnValue = "联系手机" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "manageEmail", ColumnValue = "联系邮箱" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "bankName", ColumnValue = "开户银行" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "bankAccount", ColumnValue = "银行账户" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "businessscope", ColumnValue = "经营范围" });
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

    /// <summary>
    /// 导出字段对应 列名称 部门.
    /// </summary>
    /// <returns></returns>
    private List<ExportImportHelperModel> GetDepInfoFieldToTitle(List<string> fields = null)
    {
        var res = new List<ExportImportHelperModel>();
        res.Add(new ExportImportHelperModel() { ColumnKey = "category", ColumnValue = "类型", Required = true, SelectList = new List<string>() { "公司", "部门" } });
        res.Add(new ExportImportHelperModel() { ColumnKey = "parentId", ColumnValue = "所属组织" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "fullName", ColumnValue = "部门名称", Required = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "enCode", ColumnValue = "部门编码", Required = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "managerId", ColumnValue = "部门主管" });
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

}