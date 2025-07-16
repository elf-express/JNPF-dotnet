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
using JNPF.Systems.Entitys.Dto.Position;
using JNPF.Systems.Entitys.Dto.User;
using JNPF.Systems.Entitys.Permission;
using JNPF.Systems.Entitys.System;
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
/// 业务实现：岗位管理.
/// 版 本：V3.2.0
/// 版 权：引迈信息技术有限公司（https://www.jnpfsoft.com）
/// 作 者：JNPF开发平台组
/// 日 期：2021.06.07.
/// </summary>
[ApiDescriptionSettings(Tag = "Permission", Name = "Position", Order = 162)]
[Route("api/Permission/[controller]")]
public class PositionService : IPositionService, IDynamicApiController, ITransient
{
    /// <summary>
    /// 基础仓储.
    /// </summary>
    private readonly ISqlSugarRepository<PositionEntity> _repository;

    /// <summary>
    /// 组织管理.
    /// </summary>
    private readonly IOrganizeService _organizeService;

    /// <summary>
    /// 文件服务.
    /// </summary>
    private readonly IFileManager _fileManager;

    /// <summary>
    /// 缓存管理.
    /// </summary>
    private readonly ICacheManager _cacheManager;

    /// <summary>
    /// 用户管理器.
    /// </summary>
    private readonly IUserManager _userManager;

    /// <summary>
    /// 初始化一个<see cref="PositionService"/>类型的新实例.
    /// </summary>
    public PositionService(
        ISqlSugarRepository<PositionEntity> repository,
        IOrganizeService organizeService,
        ICacheManager cacheManager,
        IFileManager fileService,
        IUserManager userManager)
    {
        _repository = repository;
        _organizeService = organizeService;
        _cacheManager = cacheManager;
        _fileManager = fileService;
        _userManager = userManager;
    }

    #region GET

    /// <summary>
    /// 获取列表 根据organizeId.
    /// </summary>
    /// <param name="organizeId">参数.</param>
    /// <returns></returns>
    [HttpGet("getList/{organizeId}")]
    public async Task<dynamic> GetListByOrganizeId(string organizeId)
    {
        List<string>? oid = new List<string>();
        if (!string.IsNullOrWhiteSpace(organizeId))
        {
            // 获取组织下的所有组织 id 集合
            List<OrganizeEntity>? oentity = await _repository.AsSugarClient().Queryable<OrganizeEntity>().ToChildListAsync(x => x.ParentId, organizeId);
            oid = oentity.Select(x => x.Id).ToList();
        }
        var dictionaryTypeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().FirstAsync(x => x.EnCode == "PositionType" && x.DeleteMark == null);
        List<PositionListOutput>? data = await _repository.AsSugarClient().Queryable<PositionEntity, OrganizeEntity, DictionaryDataEntity>(
            (a, b, c) => new JoinQueryInfos(JoinType.Left, b.Id == a.OrganizeId, JoinType.Left, a.Type == c.EnCode && c.DictionaryTypeId == dictionaryTypeEntity.Id))

            // 组织机构
            .WhereIF(!string.IsNullOrWhiteSpace(organizeId), a => oid.Contains(a.OrganizeId))
            .Where(a => a.DeleteMark == null).OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).OrderBy(a => a.LastModifyTime, OrderByType.Desc)
            .Select((a, b, c) => new PositionListOutput
            {
                id = a.Id,
                fullName = a.FullName,
                enCode = a.EnCode,
                type = c.FullName,
                department = b.FullName,
                enabledMark = a.EnabledMark,
                creatorTime = a.CreatorTime,
                description = a.Description,
                sortCode = a.SortCode
            }).ToListAsync();
        return data.OrderBy(x => x.sortCode).ToList();
    }

    /// <summary>
    /// 获取列表.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpGet("")]
    public async Task<dynamic> GetList([FromQuery] PositionListQuery input)
    {
        if (input.organizeId == "0") input.organizeId = _userManager.User.OrganizeId;

        // 获取分级管理组织
        var dataScope = _userManager.DataScope.Where(x => x.Select).Select(x => x.organizeId).Distinct().ToList();
        List<string>? childOrgIds = new List<string>();
        if (input.organizeId.IsNotEmptyOrNull())
        {
            childOrgIds.Add(input.organizeId);

            // 根据组织Id 获取所有子组织Id集合
            childOrgIds.AddRange(_repository.AsSugarClient().Queryable<OrganizeEntity>().ToChildList(x => x.ParentId, input.organizeId).Select(x => x.Id).ToList());
            childOrgIds = childOrgIds.Distinct().ToList();
        }
        var dictionaryTypeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().FirstAsync(x => x.EnCode == "PositionType" && x.DeleteMark == null);
        var posType = await _repository.AsSugarClient().Queryable<DictionaryDataEntity>().FirstAsync(x => x.Id.Equals(input.type) && x.DeleteMark == null);
        var data = await _repository.AsSugarClient().Queryable<PositionEntity>()

            // 组织机构
            .WhereIF(childOrgIds.Any(), a => childOrgIds.Contains(a.OrganizeId))
            .WhereIF(!_userManager.IsAdministrator, a => dataScope.Contains(a.OrganizeId))

            // 关键字（名称、编码）
            .WhereIF(!input.keyword.IsNullOrEmpty(), a => a.FullName.Contains(input.keyword) || a.EnCode.Contains(input.keyword))
            .WhereIF(!input.enabledMark.IsNullOrEmpty(), a => a.EnabledMark.Equals(input.enabledMark))
            .WhereIF(!input.type.IsNullOrEmpty(), a => a.Type.Equals(posType.EnCode))
            .Where(a => a.DeleteMark == null).OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).OrderBy(a => a.LastModifyTime, OrderByType.Desc)
            .Select(a => new PositionListOutput
            {
                id = a.Id,
                fullName = a.FullName,
                enCode = a.EnCode,
                type = SqlFunc.Subqueryable<DictionaryDataEntity>().EnableTableFilter().Where(x => x.DictionaryTypeId.Equals(dictionaryTypeEntity.Id) && x.EnCode.Equals(a.Type) && x.DeleteMark == null ).Select(x => x.FullName),
                department = SqlFunc.Subqueryable<OrganizeEntity>().EnableTableFilter().Where(x => x.Id.Equals(a.OrganizeId) && x.DeleteMark == null).Select(x => x.FullName),
                organizeId = SqlFunc.Subqueryable<OrganizeEntity>().EnableTableFilter().Where(x => x.Id.Equals(a.OrganizeId) && x.DeleteMark == null).Select(x => x.OrganizeIdTree),
                enabledMark = a.EnabledMark,
                creatorTime = a.CreatorTime,
                description = a.Description,
                sortCode = a.SortCode
            }).ToPagedListAsync(input.currentPage, input.pageSize);

        // 处理组织树 名称
        List<OrganizeEntity>? orgList = _organizeService.GetOrgListTreeName();

        #region 处理岗位所属组织树

        foreach (PositionListOutput? item in data.list)
        {
            // 获取用户组织集合
            item.department = orgList.Where(x => x.Id == item.organizeId.Split(",").LastOrDefault()).Select(x => x.Description).FirstOrDefault();
        }

        #endregion

        return PageResult<PositionListOutput>.SqlSugarPageResult(data);
    }

    /// <summary>
    /// 获取列表.
    /// </summary>
    /// <returns></returns>
    [HttpGet("All")]
    public async Task<dynamic> GetList()
    {
        var dictionaryTypeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().FirstAsync(x => x.EnCode == "PositionType" && x.DeleteMark == null);
        List<PositionListOutput>? data = await _repository.AsSugarClient().Queryable<PositionEntity, OrganizeEntity, DictionaryDataEntity>((a, b, c) => new JoinQueryInfos(JoinType.Left, b.Id == a.OrganizeId, JoinType.Left, a.Type == c.EnCode && c.DictionaryTypeId == dictionaryTypeEntity.Id))
            .Where(a => a.DeleteMark == null && a.EnabledMark == 1).OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).OrderBy(a => a.LastModifyTime, OrderByType.Desc)
            .Select((a, b, c) => new PositionListOutput
            {
                id = a.Id,
                fullName = a.FullName,
                enCode = a.EnCode,
                type = c.FullName,
                department = b.FullName,
                enabledMark = a.EnabledMark,
                creatorTime = a.CreatorTime,
                description = a.Description,
                sortCode = a.SortCode
            }).ToListAsync();
        return new { list = data.OrderBy(x => x.sortCode).ToList() };
    }

    /// <summary>
    /// 获取下拉框（公司+部门+岗位）.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Selector")]
    public async Task<dynamic> GetSelector()
    {
        var orgInfoList = _organizeService.GetOrgListTreeName();

        List<OrganizeEntity>? organizeList = await _organizeService.GetListAsync();
        List<PositionEntity>? positionList = await _repository.AsQueryable().Where(t => t.EnabledMark == 1 && t.DeleteMark == null)
            .OrderBy(o => o.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).OrderBy(a => a.LastModifyTime, OrderByType.Desc).ToListAsync();
        List<PositionSelectorOutput>? treeList = new List<PositionSelectorOutput>();
        organizeList.ForEach(item =>
        {
            treeList.Add(
                new PositionSelectorOutput
                {
                    id = item.Id,
                    parentId = item.ParentId,
                    fullName = item.FullName,
                    enabledMark = item.EnabledMark,
                    icon = item.Category.Equals("department") ? "icon-ym icon-ym-tree-department1" : "icon-ym icon-ym-tree-organization3",
                    type = item.Category,
                    organize = orgInfoList.Find(x => x.Id.Equals(item.Id)).Description,
                    organizeIdTree = item.OrganizeIdTree,
                    num = positionList.Count(x => x.OrganizeId.Equals(item.Id)),
                    sortCode = item.SortCode
                });
        });
        positionList.ForEach(item =>
        {
            treeList.Add(
                new PositionSelectorOutput
                {
                    id = item.Id,
                    parentId = item.OrganizeId,
                    fullName = item.FullName,
                    enabledMark = item.EnabledMark,
                    organize = orgInfoList.FirstOrDefault(x => x.Id.Equals(item.OrganizeId))?.Description,
                    icon = "icon-ym icon-ym-tree-position1",
                    type = "position",
                    num = 1,
                    sortCode = -2
                });
        });

        treeList.Where(x => !x.type.Equals("position")).ToList().ForEach(item =>
        {
            if (treeList.Any(x => !x.type.Equals("position") && x.num > 0 && x.organizeIdTree.Contains(item.organizeIdTree))) item.num = 1;
            else item.num = 0;
        });

        return new { list = treeList.Where(x => x.num > 0).OrderBy(x => x.sortCode).ToList().ToTree("-1") };
    }

    /// <summary>
    /// 获取信息.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <returns></returns>
    [HttpGet("{id}")]
    public async Task<dynamic> GetInfo(string id)
    {
        PositionEntity? entity = await _repository.GetSingleAsync(p => p.Id == id);
        var res = entity.Adapt<PositionInfoOutput>();
        res.organizeIdTree = (await _organizeService.GetInfoById(entity.OrganizeId)).OrganizeIdTree.Split(",").ToList();
        return res;
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
        foreach (var item in input.ids) idList.Add(item + "--" + "position");
        input.ids = idList;
        return await _organizeService.GetSelectedList(input);
    }

    /// <summary>
    /// 获取岗位列表 根据组织Id集合.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("getListByOrgIds")]
    public async Task<dynamic> GetListByOrgIds([FromBody] PositionListQuery input)
    {
        var dictionaryTypeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().FirstAsync(x => x.EnCode == "PositionType" && x.DeleteMark == null);
        List<PositionTreeOutput>? data = await _repository.AsSugarClient().Queryable<PositionEntity, OrganizeEntity, DictionaryDataEntity>(
            (a, b, c) => new JoinQueryInfos(JoinType.Left, b.Id == a.OrganizeId, JoinType.Left, a.Type == c.EnCode && c.DictionaryTypeId == dictionaryTypeEntity.Id))
            .Where(a => input.organizeIds.Contains(a.OrganizeId) && a.DeleteMark == null && a.EnabledMark == 1).OrderBy(a => a.SortCode)
            .Select((a, b, c) => new PositionTreeOutput
            {
                id = a.Id,
                type = "position",
                parentId = b.Id,
                fullName = a.FullName,
                enabledMark = a.EnabledMark,
                creatorTime = a.CreatorTime,
                sortCode = -2,
                isLeaf = true
            }).ToListAsync();

        // 处理组织树 名称
        List<OrganizeEntity>? allOrgList = _organizeService.GetOrgListTreeName();
        List<PositionTreeOutput>? organizeList = allOrgList.Where(x => input.organizeIds.Contains(x.Id)).Select(x => new PositionTreeOutput()
        {
            id = x.Id,
            type = x.Category,
            parentId = "0",
            fullName = x.Description,
            num = data.Count(x => x.parentId == x.id)
        }).ToList();

        return new { list = organizeList.Union(data).OrderBy(x => x.sortCode).ToList().ToTree("0") };
    }

    /// <summary>
    /// 通过公司、部门、岗位id获取岗位列表.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("PositionCondition")]
    public async Task<dynamic> PositionCondition([FromBody] PositionConditionInput input)
    {
        // 获取所有组织
        List<OrganizeEntity>? allOrgList = _organizeService.GetOrgListTreeName();

        var objIds = new List<string>();
        input.ids.Where(x => x.IsNotEmptyOrNull()).ToList().ForEach(item => objIds.Add(item.Split("--").First()));
        if (objIds.Contains("@currentOrg"))
        {
            objIds.Add(_userManager.User.OrganizeId);
            objIds.Remove("@currentOrg");
        }
        if (objIds.Contains("@currentOrgAndSubOrg"))
        {
            objIds.AddRange(allOrgList.Copy().TreeChildNode(_userManager.User.OrganizeId, t => t.Id, t => t.ParentId).Select(it => it.Id).ToList());
            objIds.Remove("@currentOrgAndSubOrg");
        }
        if (objIds.Contains("@currentGradeOrg"))
        {
            if (_userManager.IsAdministrator)
            {
                objIds.AddRange(allOrgList.Select(it => it.Id).ToList());
            }
            else
            {
                objIds.AddRange(_userManager.DataScope.Select(x => x.organizeId).ToList());
            }
            objIds.Remove("@currentGradeOrg");
        }

        List<PositionTreeOutput>? data = await _repository.AsSugarClient().Queryable<PositionEntity, OrganizeEntity>((a, b) => new JoinQueryInfos(JoinType.Left, b.Id == a.OrganizeId))
            .Where(a => a.DeleteMark == null && a.EnabledMark == 1)
            .Where(a => objIds.Contains(a.OrganizeId) || objIds.Contains(a.Id))
            .WhereIF(input.keyword.IsNotEmptyOrNull(), a => a.FullName.Contains(input.keyword) || a.EnCode.Contains(input.keyword))
            .Select((a, b) => new PositionTreeOutput
            {
                id = a.Id,
                organizeId = a.OrganizeId,
                parentId = b.Id,
                type = "position",
                fullName = a.FullName,
                enabledMark = a.EnabledMark,
                creatorTime = a.CreatorTime,
                icon = "icon-ym icon-ym-tree-position1",
                sortCode = -2,
                num = 1,
                isLeaf = true
            }).ToListAsync();

        List<PositionTreeOutput>? organizeList = allOrgList.Where(x => data.Select(x => x.organizeId).Distinct().Contains(x.Id)).Select(x => new PositionTreeOutput()
        {
            id = x.Id,
            type = x.Category,
            parentId = x.ParentId.Equals("-1") ? "0" : x.ParentId,
            icon = x.Category.Equals("department") ? "icon-ym icon-ym-tree-department1" : "icon-ym icon-ym-tree-organization3",
            fullName = x.Description,
            organizeId = x.OrganizeIdTree,
            num = data.Count(xx => xx.parentId == x.Id),
            sortCode = 99
        }).ToList();

        organizeList.OrderByDescending(x => x.organizeId.Length).ToList().ForEach(item =>
        {
            // 处理组织的父级id
            if (!organizeList.Any(x => item.parentId.Equals(x.id)) && !item.parentId.Equals("0"))
            {
                var oldParentId = item.parentId;
                var organizeIdList = item.organizeId.Split(",").ToList();
                foreach (var oitem in organizeIdList)
                {
                    if (!oitem.Equals(item.parentId) && organizeList.Any(x => oitem.Equals(x.id)))
                    {
                        item.parentId = oitem;
                    }
                    else
                    {
                        if (oitem.Equals(oldParentId) && !oitem.Equals(organizeIdList.FirstOrDefault()))
                        {
                            break;
                        }
                        else
                        {
                            item.parentId = "0";
                            break;
                        }
                    }
                }
            }

            var pOrgTree = organizeList.Where(x => x.organizeId != item.organizeId && item.organizeId.Contains(x.organizeId)).FirstOrDefault()?.fullName;
            if (organizeList.Any(x => item.parentId.Equals(x.id))) pOrgTree = organizeList.FirstOrDefault(x => item.parentId.Equals(x.id))?.fullName;

            if (pOrgTree.IsNotEmptyOrNull() && item.organizeId.IsNotEmptyOrNull()) item.fullName = item.fullName.Replace(pOrgTree + "/", string.Empty);
        });

        organizeList.Where(x => !x.type.Equals("position") && !x.id.Equals(x.organizeId)).ToList().ForEach(item =>
        {
            if (organizeList.Any(x => !x.type.Equals("position") && x.num > 0 && x.organizeId.Contains(item.organizeId))) item.num = 1;
            else item.num = 0;
            organizeList.Where(x => !x.type.Equals("position") && x.organizeId.Contains(item.organizeId) && x.organizeId != item.organizeId).ToList().ForEach(it =>
            {
                it.parentId = item.id;
            });
        });

        return new { list = organizeList.Where(x => x.num > 0).ToList().Union(data).OrderBy(x => x.sortCode).ToList().ToTree("0") };
    }

    /// <summary>
    /// 新建.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("")]
    public async Task Create([FromBody] PositionCrInput input)
    {
        if (!_userManager.DataScope.Any(it => it.organizeId == input.organizeId && it.Add == true) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);
        if (await _repository.IsAnyAsync(p => p.OrganizeId == input.organizeId && p.FullName == input.fullName && p.DeleteMark == null))
            throw Oops.Oh(ErrorCode.D6005);
        if (await _repository.IsAnyAsync(p => p.EnCode == input.enCode && p.DeleteMark == null))
            throw Oops.Oh(ErrorCode.D6000);
        PositionEntity? entity = input.Adapt<PositionEntity>();
        int isOk = await _repository.AsSugarClient().Insertable(entity).CallEntityMethod(m => m.Creator()).ExecuteCommandAsync();
        if (!(isOk > 0)) throw Oops.Oh(ErrorCode.D6001);
        await DelPosition(string.Format("{0}_{1}", _userManager.TenantId, _userManager.UserId));
    }

    /// <summary>
    /// 删除.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    public async Task Delete(string id)
    {
        PositionEntity? entity = await _repository.GetSingleAsync(p => p.Id == id && p.DeleteMark == null);
        if (!_userManager.DataScope.Any(it => it.organizeId == entity.OrganizeId && it.Delete == true) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        // 岗位下有用户不能删
        if (await _repository.AsSugarClient().Queryable<UserRelationEntity>().AnyAsync(u => u.ObjectType == "Position" && u.ObjectId == id))
            throw Oops.Oh(ErrorCode.D6007);

        int isOk = await _repository.AsSugarClient().Updateable(entity).CallEntityMethod(m => m.Delete()).UpdateColumns(it => new { it.DeleteMark, it.DeleteTime, it.DeleteUserId }).ExecuteCommandAsync();
        if (!(isOk > 0))
            throw Oops.Oh(ErrorCode.D6002);
        await DelPosition(string.Format("{0}_{1}", _userManager.TenantId, _userManager.UserId));
    }

    /// <summary>
    /// 更新.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPut("{id}")]
    public async Task Update(string id, [FromBody] PositionUpInput input)
    {
        PositionEntity? oldEntity = await _repository.GetSingleAsync(it => it.Id == id);
        if (oldEntity.OrganizeId != input.organizeId && !_userManager.DataScope.Any(it => it.organizeId == oldEntity.OrganizeId && it.Edit == true) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);
        if (!_userManager.DataScope.Any(it => it.organizeId == input.organizeId && it.Edit == true) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);
        if (await _repository.IsAnyAsync(p => p.OrganizeId == input.organizeId && p.FullName == input.fullName && p.DeleteMark == null && p.Id != id))
            throw Oops.Oh(ErrorCode.D6005);
        if (await _repository.IsAnyAsync(p => p.EnCode == input.enCode && p.DeleteMark == null && p.Id != id))
            throw Oops.Oh(ErrorCode.D6000);
        if (oldEntity.EnabledMark.Equals(1) && input.enabledMark.Equals(0) && await _repository.AsSugarClient().Queryable<UserRelationEntity>().AnyAsync(x => x.ObjectId == id))
            throw Oops.Oh(ErrorCode.COM1030);

        // 如果变更组织，该岗位下已存在成员，则不允许修改
        if (input.organizeId != oldEntity.OrganizeId)
        {
            if (await _repository.AsSugarClient().Queryable<UserRelationEntity>().AnyAsync(u => u.ObjectType == "Position" && u.ObjectId == id))
                throw Oops.Oh(ErrorCode.D6008);
        }

        PositionEntity? entity = input.Adapt<PositionEntity>();
        int isOk = await _repository.AsSugarClient().Updateable(entity).IgnoreColumns(ignoreAllNullColumns: true).CallEntityMethod(m => m.LastModify()).ExecuteCommandAsync();
        if (!(isOk > 0))
            throw Oops.Oh(ErrorCode.D6003);
        await DelPosition(string.Format("{0}_{1}", _userManager.TenantId, _userManager.UserId));
    }

    /// <summary>
    /// 更新状态.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <returns></returns>
    [HttpPut("{id}/Actions/State")]
    public async Task UpdateState(string id)
    {
        if (!_userManager.DataScope.Any(it => it.organizeId == id && it.Add == true) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);
        if (!await _repository.IsAnyAsync(r => r.Id == id && r.DeleteMark == null))
            throw Oops.Oh(ErrorCode.D6006);
        int isOk = await _repository.AsSugarClient().Updateable<PositionEntity>().UpdateColumns(it => new PositionEntity()
        {
            EnabledMark = SqlFunc.IIF(it.EnabledMark == 1, 0, 1),
            LastModifyUserId = _userManager.UserId,
            LastModifyTime = SqlFunc.GetDate()
        }).Where(it => it.Id == id).ExecuteCommandAsync();
        if (!(isOk > 0))
            throw Oops.Oh(ErrorCode.D6004);
        await DelPosition(string.Format("{0}_{1}", _userManager.TenantId, _userManager.UserId));
    }

    #endregion

    #region 导出和导入

    /// <summary>
    /// 导出Excel.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpGet("ExportData")]
    public async Task<dynamic> ExportData([FromQuery] PositionExportDataInput input)
    {
        if (input.organizeId == "0") input.organizeId = _userManager.User.OrganizeId;

        // 获取分级管理组织
        var dataScope = _userManager.DataScope.Where(x => x.Select).Select(x => x.organizeId).Distinct().ToList();
        List<string>? childOrgIds = new List<string>();
        if (input.organizeId.IsNotEmptyOrNull())
        {
            childOrgIds.Add(input.organizeId);

            // 根据组织Id 获取所有子组织Id集合
            childOrgIds.AddRange(_repository.AsSugarClient().Queryable<OrganizeEntity>().ToChildList(x => x.ParentId, input.organizeId).Select(x => x.Id).ToList());
            childOrgIds = childOrgIds.Distinct().ToList();
        }
        var dictionaryTypeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().FirstAsync(x => x.EnCode == "PositionType" && x.DeleteMark == null);
        var posType = await _repository.AsSugarClient().Queryable<DictionaryDataEntity>().FirstAsync(x => x.Id.Equals(input.type) && x.DeleteMark == null);

        var orgList = _organizeService.GetOrgListTreeName();
        var data = new List<PositionListImportDataInput>();
        var queryable = _repository.AsSugarClient().Queryable<PositionEntity>()
            // 组织机构
            .WhereIF(childOrgIds.Any(), a => childOrgIds.Contains(a.OrganizeId))
            .WhereIF(!_userManager.IsAdministrator, a => dataScope.Contains(a.OrganizeId))

            // 关键字（名称、编码）
            .WhereIF(!input.keyword.IsNullOrEmpty(), a => a.FullName.Contains(input.keyword) || a.EnCode.Contains(input.keyword))
            .WhereIF(!input.enabledMark.IsNullOrEmpty(), a => a.EnabledMark.Equals(input.enabledMark))
            .WhereIF(!input.type.IsNullOrEmpty(), a => a.Type.Equals(posType.EnCode))
            .Where(a => a.DeleteMark == null).OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).OrderBy(a => a.LastModifyTime, OrderByType.Desc)
            .Select(a => new PositionListImportDataInput
            {
                fullName = a.FullName,
                enCode = a.EnCode,
                type = SqlFunc.Subqueryable<DictionaryDataEntity>().EnableTableFilter().Where(x => x.DictionaryTypeId.Equals(dictionaryTypeEntity.Id) && x.EnCode.Equals(a.Type) && x.DeleteMark == null && x.EnabledMark.Equals(1)).Select(x => x.FullName),
                enabledMark = SqlFunc.IIF(a.EnabledMark.Equals(1), "启用", "禁用"),
                organizeId = a.OrganizeId,
                description = a.Description,
                sortCode = SqlFunc.ToString(a.SortCode)
            });
        if (input.dataType.Equals("0")) data = (await queryable.ToPagedListAsync(input.currentPage, input.pageSize)).list.ToList();
        else data = await queryable.ToListAsync();

        data.ForEach(item => item.organizeId = orgList.Find(x => x.Id.Equals(item.organizeId)).Description);

        ExcelConfig excelconfig = new ExcelConfig();
        excelconfig.FileName = string.Format("岗位信息_{0:yyyyMMddhhmmss}.xls", DateTime.Now);
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
        var fs = ExcelExportHelper<PositionListImportDataInput>.ExportMemoryStream(data, excelconfig);
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
        List<PositionListImportDataInput>? dataList = new List<PositionListImportDataInput>() { new PositionListImportDataInput()
            { organizeId = "公司名称/公司名称1/部门名称" , type = "", enabledMark = "" } };

        ExcelConfig excelconfig = new ExcelConfig();
        excelconfig.FileName = "岗位信息导入模板.xls";
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
        var stream = ExcelExportHelper<PositionListImportDataInput>.ToStream(dataList, excelconfig);
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
    public async Task<dynamic> ExportExceptionData([FromBody] PositionImportDataInput list)
    {
        list.list.ForEach(it => it.errorsInfo = string.Empty);
        object[]? res = await DataImport(list.list);

        // 错误数据
        List<PositionListImportDataInput>? errorlist = res.Last() as List<PositionListImportDataInput>;

        ExcelConfig excelconfig = new ExcelConfig();
        excelconfig.FileName = string.Format("岗位信息导入模板错误报告_{0}.xls", DateTime.Now.ToString("yyyyMMddHHmmss"));
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
        ExcelExportHelper<PositionListImportDataInput>.Export(errorlist, excelconfig, addPath);

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
    public async Task<dynamic> ImportData([FromBody] PositionImportDataInput list)
    {
        list.list.ForEach(x => x.errorsInfo = string.Empty);
        object[]? res = await DataImport(list.list);
        List<PositionEntity>? addlist = res.First() as List<PositionEntity>;
        List<PositionListImportDataInput>? errorlist = res.Last() as List<PositionListImportDataInput>;
        return new { snum = addlist.Count, fnum = errorlist.Count, failResult = errorlist, resultType = errorlist.Count < 1 ? 0 : 1 };
    }

    /// <summary>
    /// 导入数据函数.
    /// </summary>
    /// <param name="list">list.</param>
    /// <returns>[成功列表,失败列表].</returns>
    private async Task<object[]> DataImport(List<PositionListImportDataInput> list)
    {
        List<PositionListImportDataInput> inputList = list;

        if (inputList == null || inputList.Count() < 1)
            throw Oops.Oh(ErrorCode.D5019);
        if (inputList.Count > 1000)
            throw Oops.Oh(ErrorCode.D5029);

        var addList = new List<PositionEntity>();

        var pList = _repository.GetList();

        var orgList = _organizeService.GetOrgListTreeName();

        // 岗位类型
        DictionaryTypeEntity? typeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "dae93f2fd7cd4df999d32f8750fa6a1e" || x.EnCode == "PositionType") && x.DeleteMark == null).FirstAsync();
        List<DictionaryDataEntity>? _positionTypeList = await _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null).ToListAsync();

        var regexEnCode = @"^[a-zA-Z0-9.]*$";
        var regexFullNameEnCode = @"^([\u4e00-\u9fa5]|[a-zA-Z0-9])+$";
        foreach (var item in inputList)
        {
            if (item.organizeId.IsNullOrWhiteSpace()) item.errorsInfo += "所属组织不能为空,";
            if (item.fullName.IsNullOrWhiteSpace()) item.errorsInfo += "岗位名称不能为空,";
            else if (item.fullName.Length > 50) item.errorsInfo += "岗位名称值超出最多输入字符限制,";
            if (item.fullName.IsNotEmptyOrNull() && !Regex.IsMatch(item.fullName, regexFullNameEnCode))
                item.errorsInfo += "岗位名称值不能含有特殊符号,";

            if (item.enCode.IsNullOrWhiteSpace()) item.errorsInfo += "岗位编码不能为空,";
            else if (item.enCode.Length > 50) item.errorsInfo += "岗位编码值超出最多输入字符限制,";
            if (item.enCode.IsNotEmptyOrNull() && (!Regex.IsMatch(item.enCode, regexEnCode) || item.enCode.First().Equals('.') || item.enCode.Last().Equals('.')))
                item.errorsInfo += "岗位编码值只能输入英文、数字和小数点且小数点不能放在首尾,";

            if (item.type.IsNullOrWhiteSpace()) item.errorsInfo += "岗位类型不能为空,";
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
            if (item.enabledMark.IsNotEmptyOrNull() && !item.enabledMark.Equals("禁用") && !item.enabledMark.Equals("启用")) item.errorsInfo += "状态值不正确,";
            if (inputList.Count(x => x.fullName == item.fullName) > 1)
            {
                var errorItems = inputList.Where(x => x.fullName.IsNotEmptyOrNull() && x.fullName == item.fullName).ToList();
                for (var i = 1; i < errorItems.Count; i++) if (!item.errorsInfo.Contains("岗位名称值已存在")) errorItems[i].errorsInfo += "岗位名称值已存在,";
            }
            if (inputList.Count(x => x.enCode == item.enCode) > 1)
            {
                var errorItems = inputList.Where(x => x.enCode.IsNotEmptyOrNull() && x.enCode == item.enCode).ToList();
                for (var i = 1; i < errorItems.Count; i++) if (!item.errorsInfo.Contains("岗位编码值已存在")) errorItems[i].errorsInfo += "岗位编码值已存在,";
            }

            PositionEntity entity = new PositionEntity();
            entity.Id = SnowflakeIdHelper.NextId();
            if (item.organizeId.IsNotEmptyOrNull() && !orgList.Any(x => x.Description.Equals(item.organizeId)))
            {
                item.errorsInfo += "找不到该所属组织,";
            }
            entity.OrganizeId = orgList.Find(x => x.Description.Equals(item.organizeId))?.Id;

            if (pList.Any(o =>o.OrganizeId.Equals(entity.OrganizeId) && o.FullName == item.fullName && o.DeleteMark == null))
            {
                if (!item.errorsInfo.Contains("岗位名称值已存在"))
                    item.errorsInfo += "岗位名称值已存在,";
            }
            entity.FullName = item.fullName;

            if (pList.Any(o => o.EnCode == item.enCode && o.DeleteMark == null))
            {
                if (!item.errorsInfo.Contains("岗位编码值已存在"))
                    item.errorsInfo += "岗位编码值已存在,";
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

            // 岗位类型
            if (item.type.IsNotEmptyOrNull() && !_positionTypeList.Any(x => x.FullName == item.type))
            {
                item.errorsInfo += "找不到该岗位类型值,";
            }
            entity.Type = _positionTypeList.Find(x => x.FullName == item.type)?.EnCode;
            entity.SortCode = item.sortCode.IsNotEmptyOrNull() ? item.sortCode.ParseToLong() : 0;
            entity.Description = item.description;

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
                inputList = new List<PositionListImportDataInput>();
            }
        }

        var errorList = new List<PositionListImportDataInput>();
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
        // 岗位类型
        var typeEntity = _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "dae93f2fd7cd4df999d32f8750fa6a1e" || x.EnCode == "PositionType") && x.DeleteMark == null).First();
        var _positionTypeList = _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).Select(x => x.FullName).ToList();

        var res = new List<ExportImportHelperModel>();
        res.Add(new ExportImportHelperModel() { ColumnKey = "errorsInfo", ColumnValue = "异常原因", FreezePane = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "organizeId", ColumnValue = "所属组织", Required = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "fullName", ColumnValue = "岗位名称", Required = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "enCode", ColumnValue = "岗位编码", Required = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "type", ColumnValue = "岗位类型", Required = true, SelectList = _positionTypeList });
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

    #region PublicMethod

    /// <summary>
    /// 获取信息.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <returns></returns>
    [NonAction]
    public async Task<PositionEntity> GetInfoById(string id)
    {
        return await _repository.GetSingleAsync(p => p.Id == id);
    }

    /// <summary>
    /// 获取岗位列表.
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public async Task<List<PositionEntity>> GetListAsync()
    {
        return await _repository.AsQueryable().Where(u => u.DeleteMark == null).ToListAsync();
    }

    /// <summary>
    /// 名称.
    /// </summary>
    /// <param name="ids">岗位ID组</param>
    /// <returns></returns>
    [NonAction]
    public string GetName(string ids)
    {
        if (ids.IsNullOrEmpty())
            return string.Empty;
        List<string>? idList = ids.Split(",").ToList();
        List<string>? nameList = new List<string>();
        List<PositionEntity>? roleList = _repository.AsQueryable().Where(x => x.DeleteMark == null && x.EnabledMark == 1).ToList();
        foreach (string item in idList)
        {
            var info = roleList.Find(x => x.Id == item);
            if (info != null && info.FullName.IsNotEmptyOrNull())
            {
                nameList.Add(info.FullName);
            }
        }

        return string.Join(",", nameList);
    }

    #endregion

    #region PrivateMethod

    /// <summary>
    /// 删除岗位缓存.
    /// </summary>
    /// <param name="userId">适配多租户模式(userId:tenantId_userId).</param>
    /// <returns></returns>
    private async Task<bool> DelPosition(string userId)
    {
        string? cacheKey = string.Format("{0}{1}", CommonConst.CACHEKEYPOSITION, userId);
        await _cacheManager.DelAsync(cacheKey);
        return await Task.FromResult(true);
    }

    #endregion
}