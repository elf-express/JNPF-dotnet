using JNPF.Common.Configuration;
using JNPF.Common.Const;
using JNPF.Common.Core.Handlers;
using JNPF.Common.Core.Manager;
using JNPF.Common.Core.Manager.Files;
using JNPF.Common.Core.Manager.Tenant;
using JNPF.Common.Enums;
using JNPF.Common.Extension;
using JNPF.Common.Filter;
using JNPF.Common.Helper;
using JNPF.Common.Manager;
using JNPF.Common.Models;
using JNPF.Common.Models.NPOI;
using JNPF.Common.Models.User;
using JNPF.Common.Options;
using JNPF.Common.Security;
using JNPF.DatabaseAccessor;
using JNPF.DataEncryption;
using JNPF.DependencyInjection;
using JNPF.DynamicApiController;
using JNPF.FriendlyException;
using JNPF.RemoteRequest.Extensions;
using JNPF.Systems.Entitys.Dto.Organize;
using JNPF.Systems.Entitys.Dto.Permission.User;
using JNPF.Systems.Entitys.Dto.Role;
using JNPF.Systems.Entitys.Dto.SysConfig;
using JNPF.Systems.Entitys.Dto.User;
using JNPF.Systems.Entitys.Dto.UserRelation;
using JNPF.Systems.Entitys.Enum;
using JNPF.Systems.Entitys.Permission;
using JNPF.Systems.Entitys.System;
using JNPF.Systems.Interfaces.Permission;
using JNPF.Systems.Interfaces.System;
using JNPF.WorkFlow.Interfaces.Repository;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Ocsp;
using SqlSugar;
using System.Data;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace JNPF.Systems;

/// <summary>
///  业务实现：用户信息.
/// </summary>
[ApiDescriptionSettings(Tag = "Permission", Name = "Users", Order = 163)]
[Route("api/permission/[controller]")]
public class UsersService : IUsersService, IDynamicApiController, ITransient
{
    /// <summary>
    /// 配置文档.
    /// </summary>
    private readonly OauthOptions _oauthOptions = App.GetConfig<OauthOptions>("OAuth", true);

    /// <summary>
    /// 服务基础仓储.
    /// </summary>
    private readonly ISqlSugarRepository<UserEntity> _repository;  // 用户表仓储

    /// <summary>
    /// 机构表服务.
    /// </summary>
    private readonly IOrganizeService _organizeService;

    /// <summary>
    /// 机构表服务.
    /// </summary>
    private readonly IDepartmentService _departmentService;

    /// <summary>
    /// 流程相关.
    /// </summary>
    private readonly IWorkFlowRepository _flowTaskRepository;

    /// <summary>
    /// 用户关系表服务.
    /// </summary>
    private readonly IUserRelationService _userRelationService;

    /// <summary>
    /// 系统配置服务.
    /// </summary>
    private readonly ISysConfigService _sysConfigService;

    /// <summary>
    /// 第三方同步服务.
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
    /// 用户管理.
    /// </summary>
    private readonly IUserManager _userManager;

    /// <summary>
    /// 多租户配置选项.
    /// </summary>
    private readonly TenantOptions _tenant;

    /// <summary>
    /// 租户管理.
    /// </summary>
    private readonly ITenantManager _tenantManager;

    /// <summary>
    /// IM中心处理程序.
    /// </summary>
    private IMHandler _imHandler;

    /// <summary>
    /// 初始化一个<see cref="UsersService"/>类型的新实例.
    /// </summary>
    public UsersService(
        ISqlSugarRepository<UserEntity> userRepository,
        IOrganizeService organizeService,
        IDepartmentService departmentService,
        IUserRelationService userRelationService,
        IWorkFlowRepository flowTaskRepository,
        ISysConfigService sysConfigService,
        ISynThirdInfoService synThirdInfoService,
        ICacheManager cacheManager,
        IFileManager fileService,
        ISqlSugarClient sqlSugarClient,
        IOptions<TenantOptions> tenantOptions,
        IUserManager userManager,
        ITenantManager tenantManager,
        IMHandler imHandler)
    {
        _repository = userRepository;
        _organizeService = organizeService;
        _departmentService = departmentService;
        _userRelationService = userRelationService;
        _flowTaskRepository = flowTaskRepository;
        _sysConfigService = sysConfigService;
        _userManager = userManager;
        _cacheManager = cacheManager;
        _synThirdInfoService = synThirdInfoService;
        _fileManager = fileService;
        _tenant = tenantOptions.Value;
        _tenantManager = tenantManager;
        _imHandler = imHandler;
    }

    #region GET

    /// <summary>
    /// 获取列表.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpGet("")]
    public async Task<PageResult<UserListOutput>> GetList([FromQuery] UserListQuery input)
    {
        // 获取分级管理组织
        var dataScope = _userManager.DataScope.Where(x => x.Select).Select(x => x.organizeId).Distinct().ToList();

        PageInputBase? pageInput = input.Adapt<PageInputBase>();

        // 处理组织树 名称
        List<OrganizeEntity>? orgTreeNameList = _organizeService.GetOrgListTreeName();

        #region 获取组织层级

        List<string>? childOrgIds = new List<string>();
        if (input.organizeId.IsNotEmptyOrNull())
        {
            childOrgIds.Add(input.organizeId);

            // 根据组织Id 获取所有子组织Id集合
            childOrgIds.AddRange(orgTreeNameList.Where(x => x.OrganizeIdTree.Contains(input.organizeId)).Select(x => x.Id).ToList());
            childOrgIds = childOrgIds.Distinct().ToList();
        }

        #endregion

        // 获取配置文件 账户锁定类型
        SysConfigEntity? config = await _repository.AsSugarClient().Queryable<SysConfigEntity>().Where(x => x.Key.Equals("lockType") && x.Category.Equals("SysConfig")).FirstAsync();
        ErrorStrategy configLockType = (ErrorStrategy)Enum.Parse(typeof(ErrorStrategy), config?.Value);

        SqlSugarPagedList<UserListOutput>? data = new SqlSugarPagedList<UserListOutput>();

        // 性别字典类型
        var dictionaryTypeEntity2 = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().FirstAsync(x => x.EnCode == "sex" && x.DeleteMark == null && x.EnabledMark == 1);

        data = await _repository.AsQueryable()
            .Where(a => a.DeleteMark == null && !a.Account.Equals("admin"))
            .WhereIF(input.enabledMark != null, a => a.EnabledMark.Equals(input.enabledMark))
            .WhereIF(input.gender != null, a => a.Gender.Equals(input.gender))
            .WhereIF(childOrgIds.Any(), a => SqlFunc.Subqueryable<UserRelationEntity>().EnableTableFilter().Where(x => childOrgIds.Contains(x.ObjectId) && x.UserId.Equals(a.Id)).Any())
            .WhereIF(!input.keyword.IsNullOrEmpty(), a => a.Account.Contains(input.keyword) || a.RealName.Contains(input.keyword) || a.MobilePhone.Contains(input.keyword))
            .WhereIF(!_userManager.IsAdministrator, a => SqlFunc.Subqueryable<UserRelationEntity>().EnableTableFilter().Where(x => dataScope.Contains(x.ObjectId) && x.UserId.Equals(a.Id)).Any())
            .OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).OrderByIF(!input.keyword.IsNullOrEmpty(), a => a.LastModifyTime, OrderByType.Desc)
            .Select(a => new UserListOutput
            {
                id = a.Id,
                account = a.Account,
                realName = a.RealName,
                headIcon = SqlFunc.Subqueryable<UserEntity>().EnableTableFilter().Where(e => e.Id == a.Id).Select(e => SqlFunc.MergeString("/api/File/Image/userAvatar/", e.HeadIcon)),
                creatorTime = a.CreatorTime,
                gender = SqlFunc.Subqueryable<DictionaryDataEntity>().EnableTableFilter().Where(d => d.DictionaryTypeId == dictionaryTypeEntity2.Id && d.EnCode == a.Gender && d.DeleteMark == null && d.EnabledMark == 1).Select(d => d.FullName),
                mobilePhone = a.MobilePhone,
                sortCode = a.SortCode,
                isAdministrator = a.IsAdministrator,
                enabledMark = SqlFunc.IIF(configLockType == ErrorStrategy.Delay && a.EnabledMark == 2 && a.UnLockTime < DateTime.Now, 1, a.EnabledMark),
                handoverMark = SqlFunc.IIF(SqlFunc.IsNullOrEmpty(a.HandoverUserId), 0, 1)
            }).ToPagedListAsync(input.currentPage, input.pageSize);

        #region 处理 用户 多组织

        List<UserRelationEntity>? orgUserIdAll = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
            .Where(x => data.list.Select(u => u.id).Contains(x.UserId)).ToListAsync();
        foreach (UserListOutput? item in data.list)
        {
            // 获取用户组织集合
            item.organizeList = orgUserIdAll.Where(x => x.UserId == item.id).Select(x => x.ObjectId).ToList();
            item.organize = string.Join(" ; ", orgTreeNameList.Where(x => item.organizeList.Contains(x.Id)).Select(x => x.Description));
        }

        #endregion

        return new PageResult<UserListOutput>() { list = data.list.ToList(), pagination = data.pagination.Adapt<PageResult>() };
    }
    /// <summary>
    /// 获取全部用户.
    /// </summary>
    /// <returns></returns>
    [HttpGet("All")]
    public async Task<dynamic> GetUserAllList()
    {
        return await _repository.AsSugarClient().Queryable<UserEntity, OrganizeEntity>((a, b) => new JoinQueryInfos(JoinType.Left, b.Id == a.OrganizeId))
            .Where(p => p.EnabledMark == 1 && p.DeleteMark == null).OrderBy(p => p.SortCode)
            .Select((a, b) => new UserListAllOutput
            {
                id = a.Id,
                account = a.Account,
                realName = a.RealName,
                headIcon = SqlFunc.MergeString("/api/File/Image/userAvatar/", a.HeadIcon),
                gender = a.Gender,
                department = b.FullName,
                sortCode = a.SortCode,
                quickQuery = a.QuickQuery,
            }).ToListAsync();
    }

    /// <summary>
    /// 获取用户数据分页 根据角色Id.
    /// </summary>
    /// <returns></returns>
    [HttpGet("getUsersByRoleId")]
    public async Task<dynamic> GetUsersByRoleId([FromQuery] RoleListInput input)
    {
        RoleEntity? roleInfo = await _repository.AsSugarClient().Queryable<RoleEntity>().Where(x => x.Id == input.roleId).FirstAsync();

        // 查询全部用户 (全局角色)
        if (roleInfo.GlobalMark == 1)
        {
            SqlSugarPagedList<UserListAllOutput>? list = await _repository.AsQueryable()
                .WhereIF(!input.keyword.IsNullOrEmpty(), a => a.Account.Contains(input.keyword) || a.RealName.Contains(input.keyword))
                .Where(p => p.EnabledMark == 1 && p.DeleteMark == null).OrderBy(p => p.SortCode)
                .Select(a => new UserListAllOutput
                {
                    id = a.Id,
                    account = a.Account,
                    realName = a.RealName,
                    gender = a.Gender,
                    sortCode = a.SortCode,
                    quickQuery = a.QuickQuery
                }).ToPagedListAsync(input.currentPage, input.pageSize);

            return PageResult<UserListAllOutput>.SqlSugarPageResult(list);
        }

        // 查询角色 所属 所有组织 用户
        else
        {
            // 查询角色 所有所属组织
            List<string>? orgList = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>().Where(x => x.ObjectType == "Role" && x.ObjectId == roleInfo.Id).Select(x => x.OrganizeId).ToListAsync();

            List<string>? userIdList = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.ObjectType == "Organize" && orgList.Contains(x.ObjectId)).Select(x => x.UserId).Distinct().ToListAsync();

            SqlSugarPagedList<UserListAllOutput>? list = await _repository.AsQueryable()
                .Where(a => userIdList.Contains(a.Id))
                .Where(p => p.EnabledMark == 1 && p.DeleteMark == null).OrderBy(p => p.SortCode)
                .WhereIF(!input.keyword.IsNullOrEmpty(), a => a.Account.Contains(input.keyword) || a.RealName.Contains(input.keyword))
                .Select(a => new UserListAllOutput
                {
                    id = a.Id,
                    account = a.Account,
                    realName = a.RealName,
                    gender = a.Gender,
                    sortCode = a.SortCode,
                    quickQuery = a.QuickQuery,
                }).ToPagedListAsync(input.currentPage, input.pageSize);

            return PageResult<UserListAllOutput>.SqlSugarPageResult(list);
        }
    }

    /// <summary>
    /// 获取用户数据 根据角色所属组织.
    /// </summary>
    /// <returns></returns>
    [HttpGet("GetUsersByRoleOrgId")]
    public async Task<dynamic> GetUsersByRoleOrgId([FromQuery] RoleListInput input)
    {
        RoleEntity? roleInfo = await _repository.AsSugarClient().Queryable<RoleEntity>().Where(x => x.Id == input.roleId).FirstAsync();
        input.organizeId = input.organizeId == null ? "0" : input.organizeId;

        // 获取角色所属组织集合
        List<string>? orgList = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>().Where(x => x.ObjectType == "Role" && x.ObjectId == roleInfo.Id).Select(x => x.OrganizeId).ToListAsync();

        var orgTreeNameList = _organizeService.GetOrgListTreeName();

        List<OrganizeMemberListOutput>? output = new List<OrganizeMemberListOutput>();
        if (input.organizeId.Equals("0"))
        {
            if (input.keyword.IsNotEmptyOrNull())
            {
                // 获取角色所属组织 成员id
                var res = await _repository.AsSugarClient().Queryable<UserEntity, UserRelationEntity>((a, b) => new JoinQueryInfos(JoinType.Left, b.UserId == a.Id))
                .Where((a, b) => b.ObjectType == "Organize" && orgList.Contains(b.ObjectId)).Where((a, b) => a.EnabledMark == 1 && a.DeleteMark == null)
                .Where((a, b) => a.RealName.Contains(input.keyword) || a.Account.Contains(input.keyword))
                .GroupBy((a, b) => new { a.Id, a.RealName, a.Account, a.EnabledMark })
                .Select((a, b) => new {
                    id = a.Id,
                    fullName = SqlFunc.MergeString(a.RealName, "/", a.Account),
                    enabledMark = a.EnabledMark,
                    type = "user",
                    icon = "icon-ym icon-ym-tree-user2",
                    hasChildren = false,
                    isLeaf = true
                }).ToListAsync();
                output.AddRange(res.Adapt<List<OrganizeMemberListOutput>>());
            }
            else
            {
                List<OrganizeEntity>? allOrg = _organizeService.GetOrgListTreeName();

                List<OrganizeEntity>? data = await _repository.AsSugarClient().Queryable<OrganizeEntity>()
                    .Where(o => orgList.Contains(o.Id) && o.DeleteMark == null && o.EnabledMark == 1)
                    .OrderBy(o => o.SortCode).ToListAsync();

                foreach (OrganizeEntity? o in data)
                {
                    if (o.OrganizeIdTree.IsNullOrEmpty()) o.OrganizeIdTree = o.Id;
                    if (!data.Where(x => x.Id != o.Id && o.OrganizeIdTree.Contains(x.OrganizeIdTree)).Any())
                    {
                        output.Add(new OrganizeMemberListOutput
                        {
                            id = o.Id,
                            fullName = allOrg.FirstOrDefault(x => x.Id.Equals(o.Id))?.Description,
                            enabledMark = o.EnabledMark,
                            type = o.Category,
                            icon = "icon-ym icon-ym-tree-organization3",
                            hasChildren = true,
                            isLeaf = false
                        });
                    }
                }
            }
        }
        else
        {
            List<OrganizeEntity>? allOrg = await _repository.AsSugarClient().Queryable<OrganizeEntity>().Where(o => o.DeleteMark == null && o.EnabledMark == 1).OrderBy(o => o.ParentId).ToListAsync();

            var res = await _repository.AsSugarClient().Queryable<UserEntity, UserRelationEntity>((a, b) => new JoinQueryInfos(JoinType.Left, b.UserId == a.Id))
                .Where((a, b) => b.ObjectType == "Organize" && b.ObjectId == input.organizeId).Where((a, b) => a.EnabledMark == 1 && a.DeleteMark == null)
                .GroupBy((a, b) => new { a.Id, a.RealName, a.Account, a.EnabledMark })
                .Select((a, b) => new {
                    id = a.Id,
                    fullName = SqlFunc.MergeString(a.RealName, "/", a.Account),
                    enabledMark = a.EnabledMark,
                    type = "user",
                    icon = "icon-ym icon-ym-tree-user2",
                    hasChildren = false,
                    isLeaf = true
                }).ToListAsync();
            output.AddRange(res.Adapt<List<OrganizeMemberListOutput>>());
            var departmentList = await _repository.AsSugarClient().Queryable<OrganizeEntity>().Where(o => o.OrganizeIdTree.Contains(input.organizeId) && orgList.Contains(o.Id)).ToListAsync();

            departmentList.OrderBy(x => x.OrganizeIdTree.Length).ToList().ForEach(o =>
            {
                o.FullName = orgTreeNameList.FirstOrDefault(x => x.Id.Equals(o.Id)).Description;

                if (o.Id != input.organizeId && !output.Any(x => o.FullName.Contains(x.fullName)))
                {
                    var pName = string.Empty;
                    if (!departmentList.Any(x => x.Id == o.ParentId)) pName = orgTreeNameList.FirstOrDefault(x => x.Id.Equals(input.organizeId)).Description;
                    else pName = orgTreeNameList.FirstOrDefault(x => x.Id.Equals(o.ParentId)).Description;
                    output.Add(new OrganizeMemberListOutput()
                    {
                        id = o.Id,
                        fullName = o.FullName.Replace(pName + "/", string.Empty),
                        enabledMark = o.EnabledMark,
                        type = o.Category,
                        icon = o.Category.Equals("company") ? "icon-ym icon-ym-tree-organization3" : "icon-ym icon-ym-tree-department1",
                        hasChildren = true,
                        isLeaf = false
                    });
                }
            });
        }

        // 获取 所属组织的所有成员
        List<UserRelationEntity>? userList = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
            .Where(x => x.ObjectType == "Organize" && output.Select(x => x.id).Contains(x.UserId)).ToListAsync();

        // 用户头像
        var usersHead = await _repository.AsSugarClient().Queryable<UserEntity>().Where(x => output.Select(xx => xx.id).Contains(x.Id)).Select(x => new { id = x.Id, headIcon = x.HeadIcon }).ToListAsync();

        // 处理组织树
        output.ForEach(item =>
        {
            if (item.type.Equals("user"))
            {
                var head = usersHead.Find(x => x.id.Equals(item.id)).headIcon;
                item.headIcon = head.IsNullOrEmpty() ? "/api/file/Image/userAvatar/001.png" : "/api/file/Image/userAvatar/" + head;
            }
            var oids = userList.Where(x => x.UserId.Equals(item.id)).Select(x => x.ObjectId).ToList();
            var oTree = orgTreeNameList.Where(x => oids.Contains(x.Id)).Select(x => x.Description).ToList();
            item.organize = string.Join(",", oTree);
        });
        return output;
    }

    /// <summary>
    /// 获取IM用户列表.
    /// </summary>
    /// <returns></returns>
    [HttpGet("ImUser")]
    public async Task<dynamic> GetImUserList([FromQuery] PageInputBase input)
    {
        SqlSugarPagedList<IMUserListOutput>? list = await _repository.AsSugarClient().Queryable<UserEntity, OrganizeEntity>((a, b) => new JoinQueryInfos(JoinType.Left, b.Id == a.OrganizeId))
            .WhereIF(!input.keyword.IsNullOrEmpty(), a => a.Account.Contains(input.keyword) || a.RealName.Contains(input.keyword))
            .Where(a => a.Id != _userManager.UserId && a.EnabledMark == 1 && a.DeleteMark == null).OrderBy(a => a.SortCode)
            .Select((a, b) => new IMUserListOutput
            {
                id = a.Id,
                account = a.Account,
                realName = a.RealName,
                headIcon = SqlFunc.MergeString("/api/File/Image/userAvatar/", a.HeadIcon),
                department = b.FullName,
            }).ToPagedListAsync(input.currentPage, input.pageSize);
        return PageResult<IMUserListOutput>.SqlSugarPageResult(list);
    }

    /// <summary>
    /// 获取下拉框（公司+部门+用户）.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Selector")]
    public async Task<dynamic> GetSelector()
    {
        List<OrganizeEntity>? organizeList = await _organizeService.GetListAsync();
        List<UserEntity>? userList = await _repository.AsQueryable().Where(t => t.EnabledMark == 1 && t.DeleteMark == null).OrderBy(u => u.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).ToListAsync();
        List<UserSelectorOutput>? organizeTreeList = organizeList.Adapt<List<UserSelectorOutput>>();
        List<UserSelectorOutput>? treeList = userList.Adapt<List<UserSelectorOutput>>();
        treeList = treeList.Concat(organizeTreeList).ToList();
        return new { list = treeList.OrderBy(x => x.sortCode).ToList().ToTree("-1") };
    }

    /// <summary>
    /// 获取信息.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <returns></returns>
    [HttpGet("{id}")]
    public async Task<dynamic> GetInfo(string id)
    {
        UserEntity? entity = await _repository.GetFirstAsync(u => u.Id == id);
        SysConfigEntity? config = await _repository.AsSugarClient().Queryable<SysConfigEntity>().Where(x => x.Key.Equals("lockType") && x.Category.Equals("SysConfig")).FirstAsync();
        string? configLockType = config?.Value;
        entity.EnabledMark = configLockType.IsNotEmptyOrNull() && configLockType == "2" && entity.EnabledMark == 2 && entity.UnLockTime < DateTime.Now ? 1 : entity.EnabledMark;
        UserInfoOutput? output = entity.Adapt<UserInfoOutput>();
        if (output.headIcon == "/api/File/Image/userAvatar/") output.headIcon = string.Empty;
        if (entity != null)
        {
            List<UserRelationEntity>? allRelationList = await _userRelationService.GetListByUserId(id);
            var relationIds = allRelationList.Where(x => x.ObjectType == "Organize" || x.ObjectType == "Position").Select(x => new { x.ObjectId, x.ObjectType }).ToList();
            List<OrganizeEntity>? oList = await _repository.AsSugarClient().Queryable<OrganizeEntity>().Where(x => relationIds.Where(x => x.ObjectType == "Organize").Select(x => x.ObjectId).Contains(x.Id)).ToListAsync();
            output.organizeIdTree = new List<List<string>>();
            oList.ForEach(item =>
            {
                if (item.OrganizeIdTree.IsNotEmptyOrNull()) output.organizeIdTree.Add(item.OrganizeIdTree.Split(",").ToList());
            });
            output.organizeId = string.Join(",", relationIds.Where(x => x.ObjectType == "Organize").Select(x => x.ObjectId));
            output.positionId = string.Join(",", relationIds.Where(x => x.ObjectType == "Position").Select(x => x.ObjectId));
        }

        return output;
    }

    /// <summary>
    /// 获取当前用户所属机构下属成员.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpGet("getOrganization")]
    public async Task<dynamic> GetOrganizeMember([FromQuery] UserListQuery input)
    {
        if (input.organizeId.IsNotEmptyOrNull() && input.organizeId != "0") input.organizeId = input.organizeId.Split(",").LastOrDefault();
        else input.organizeId = _userManager.User.OrganizeId;

        // 获取所属组织的所有成员
        List<UserRelationEntity>? userList = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
            .Where(x => x.ObjectType == "Organize").ToListAsync();

        var res = await _repository.AsQueryable()
                .WhereIF(!input.keyword.IsNullOrEmpty(), u => u.Account.Contains(input.keyword) || u.RealName.Contains(input.keyword))
                .Where(u => u.EnabledMark == 1 && u.DeleteMark == null && userList.Where(x => x.ObjectId == input.organizeId).Select(x => x.UserId).Contains(u.Id)).OrderBy(o => o.SortCode)
                .Select(u => new OrganizeMemberListOutput
                {
                    id = u.Id,
                    fullName = SqlFunc.MergeString(u.RealName, "/", u.Account),
                    enabledMark = u.EnabledMark,
                    icon = "icon-ym icon-ym-tree-user2",
                    headIcon = SqlFunc.MergeString("/api/File/Image/userAvatar/", u.HeadIcon),
                    isLeaf = true,
                    hasChildren = false,
                    type = "user",
                }).ToListAsync();

        if (res.Any())
        {
            var orgList = _organizeService.GetOrgListTreeName();

            // 处理组织树
            res.ForEach(item =>
            {
                var oids = userList.Where(x => x.UserId.Equals(item.id)).Select(x => x.ObjectId).ToList();
                var oTree = orgList.Where(x => oids.Contains(x.Id)).Select(x => x.Description).ToList();
                item.organize = string.Join(",", oTree);
            });
        }

        return res;
    }

    /// <summary>
    /// 获取工作交接内容.
    /// </summary>
    /// <param name="fromId">移交人Id.</param>
    /// <returns></returns>
    [HttpGet("getWorkByUser")]
    public async Task<dynamic> GetWorkByUser([FromQuery] string fromId)
    {
        var res = new UserWorkHandoverModel();
        res.permission = await _repository.AsSugarClient().Queryable<PermissionGroupEntity>().Where(x => x.PermissionMember.Contains(fromId) && x.DeleteMark == null)
            .Select(x => new PermissionGroupListSelector() { id = x.Id, enCode = x.EnCode, fullName = x.FullName, icon = "icon-ym icon-ym-authGroup" }).ToListAsync();

        res.flowTask = _flowTaskRepository.GetWorkHandover(fromId, 1);
        res.flow = _flowTaskRepository.GetWorkHandover(fromId, 2);

        return res;
    }

    /// <summary>
    /// 当前用户相关人员.
    /// </summary>
    /// <param name="type">2:同一部门、3:同一岗位、6:同一公司.</param>
    /// <returns></returns>
    [HttpGet("ReceiveUserList")]
    public async Task<dynamic> ReceiveUserList([FromQuery] string type, [FromQuery] UserListQuery input)
    {
        var list = new SqlSugarPagedList<UserListOutput>();
        var userIdList = new List<string>();
        var depId = string.Empty;
        var orgIds = new List<string>();
        var orgEntity = _repository.AsSugarClient().Queryable<OrganizeEntity>().First(x => x.Id == _userManager.User.OrganizeId && x.EnabledMark == 1 && x.DeleteMark == null);
        if (orgEntity.IsNotEmptyOrNull())
        {
            if (orgEntity.Category == "department")
            {
                depId = orgEntity.Id;
                var companyId = _departmentService.GetCompanyId(orgEntity.Id);
                orgIds = _departmentService.GetCompanyAllDep(companyId, true).Select(x => x.Id).Distinct().ToList();
                orgIds.Add(companyId);
            }
            else
            {
                orgIds = _departmentService.GetCompanyAllDep(orgEntity.Id).Select(x => x.Id).ToList();
            }
        }
        list = _repository.AsSugarClient().Queryable<UserRelationEntity, UserEntity>((a, b) => new JoinQueryInfos(JoinType.Left, b.Id == a.UserId))
        .Where((a, b) => b.DeleteMark == null && b.EnabledMark==1)
        .WhereIF(input.type == "2", (a, b) => a.ObjectId == depId && a.ObjectType == "Organize")
        .WhereIF(input.type == "3", (a, b) => a.ObjectId == _userManager.User.PositionId && a.ObjectType == "Position")
        .WhereIF(input.type == "6" && orgIds.Any(), (a, b) => orgIds.Contains(a.ObjectId) && a.ObjectType == "Organize")
        .WhereIF(input.keyword.IsNotEmptyOrNull(), (a, b) => b.Account.Contains(input.keyword) || b.RealName.Contains(input.keyword))
        .GroupBy((a, b) => new { Id = b.Id, Account = b.Account, RealName = b.RealName, Gender = b.Gender, MobilePhone = b.MobilePhone })
        .Select((a, b) => new UserListOutput()
        {
            id = b.Id,
            organizeId = SqlFunc.Subqueryable<UserEntity>().EnableTableFilter().Where(e => e.Id == b.Id).Select(u => u.OrganizeId),
            account = b.Account,
            fullName = SqlFunc.MergeString(b.RealName, "/", b.Account),
            headIcon = SqlFunc.Subqueryable<UserEntity>().EnableTableFilter().Where(e => e.Id == b.Id).Select(u => SqlFunc.MergeString("/api/File/Image/userAvatar/", u.HeadIcon)),
            gender = b.Gender,
            mobilePhone = b.MobilePhone
        }).ToPagedList(input.currentPage, input.pageSize);

        if (list.list.Any())
        {
            var orgList = _organizeService.GetOrgListTreeName();

            // 获取所属组织的所有成员
            List<UserRelationEntity>? userList = _repository.AsSugarClient().Queryable<UserRelationEntity>()
                .Where(x => x.ObjectType == "Organize" && list.list.Select(x => x.id).Contains(x.UserId)).ToList();

            // 处理组织树
            list.list.ToList().ForEach(item =>
            {
                var oids = userList.Where(x => x.UserId.Equals(item.id)).Select(x => x.ObjectId).ToList();
                var oTree = orgList.Where(x => oids.Contains(x.Id)).Select(x => x.Description).ToList();
                item.organize = string.Join(",", oTree);
            });
        }

        return PageResult<UserListOutput>.SqlSugarPageResult(list);
    }

    #endregion

    #region POST

    /// <summary>
    /// 根据用户Id List 获取当前用户Id.
    /// </summary>
    /// <returns></returns>
    [HttpPost("getDefaultCurrentValueUserId")]
    public async Task<dynamic> GetDefaultCurrentValueUserId([FromBody] GetDefaultCurrentValueInput input)
    {
        if ((input.UserIds == null || !input.UserIds.Any()) && (input.DepartIds == null || !input.DepartIds.Any()) && (input.PositionIds == null || !input.PositionIds.Any())
            && (input.RoleIds == null || !input.RoleIds.Any()) && (input.GroupIds == null || !input.GroupIds.Any())) return new { userId = _userManager.UserId };

        var userRelationList = _repository.AsSugarClient().Queryable<UserRelationEntity>().Select(x => new UserRelationEntity() { UserId = x.UserId, ObjectId = x.ObjectId }).ToList();
        var userIdList = userRelationList.Where(x => input.UserIds.Contains(x.UserId) || input.DepartIds.Contains(x.ObjectId)
            || input.PositionIds.Contains(x.ObjectId) || input.RoleIds.Contains(x.ObjectId) || input.GroupIds.Contains(x.ObjectId)).Select(x => x.UserId).ToList();

        if (userIdList.Contains(_userManager.UserId)) return new { userId = _userManager.UserId };
        else return new { userId = string.Empty };
    }

    /// <summary>
    /// 获取.
    /// </summary>
    /// <returns></returns>
    [HttpPost("GetUserList")]
    public async Task<dynamic> GetUserList([FromBody] UserRelationInput input)
    {
        var data = await _repository.AsQueryable().Where(it => it.EnabledMark > 0 && it.DeleteMark == null)
            .Where(it => input.ids.Contains(it.Id))
            .Select(it => new OrganizeMemberListOutput()
            {
                id = it.Id,
                fullName = SqlFunc.MergeString(it.RealName, "/", it.Account),
                headIcon = SqlFunc.MergeString("/api/File/Image/userAvatar/", it.HeadIcon),
                enabledMark = it.EnabledMark,
                isAdministrator = it.IsAdministrator,
            }).ToListAsync();

        data = data.OrderBy(x => input.ids.IndexOf(x.id)).ToList();
        if (data.Any())
        {
            var orgList = _organizeService.GetOrgListTreeName();

            // 获取 所属组织的所有成员
            List<UserRelationEntity>? userList = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
                .Where(x => x.ObjectType == "Organize" && data.Select(x => x.id).Contains(x.UserId)).ToListAsync();

            // 处理组织树
            data.ForEach(item =>
            {
                var oids = userList.Where(x => x.UserId.Equals(item.id)).Select(x => x.ObjectId).ToList();
                var oTree = orgList.Where(x => oids.Contains(x.Id)).Select(x => x.Description).ToList();
                item.organize = string.Join(",", oTree);
            });
        }

        return new { list = data };
    }

    /// <summary>
    /// 获取机构成员列表.
    /// </summary>
    /// <param name="organizeId">机构ID.</param>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("ImUser/Selector/{organizeId}")]
    public async Task<dynamic> GetOrganizeMemberList(string organizeId, [FromBody] PageInputBase input)
    {
        List<OrganizeMemberListOutput>? output = new List<OrganizeMemberListOutput>();
        var orgList = _organizeService.GetOrgListTreeName();
        if (!input.keyword.IsNullOrEmpty())
        {
            var outList = await _repository.AsQueryable()
                .WhereIF(!input.keyword.IsNullOrEmpty(), u => u.Account.Contains(input.keyword) || u.RealName.Contains(input.keyword))
                .Where(u => u.EnabledMark > 0 && u.DeleteMark == null).OrderBy(o => o.SortCode)
                .Select(u => new OrganizeMemberListOutput
                {
                    id = u.Id,
                    fullName = SqlFunc.MergeString(u.RealName, "/", u.Account),
                    enabledMark = SqlFunc.IIF(u.EnabledMark == 2 && u.UnLockTime < DateTime.Now, 1, u.EnabledMark),
                    icon = "icon-ym icon-ym-tree-user2",
                    headIcon = SqlFunc.MergeString("/api/File/Image/userAvatar/", u.HeadIcon),
                    isLeaf = true,
                    hasChildren = false,
                    type = "user",
                    isAdministrator = u.IsAdministrator,
                }).ToPagedListAsync(input.currentPage, input.pageSize);

            if (outList.list.Any())
            {
                // 获取 所属组织的所有成员
                List<UserRelationEntity>? userList = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
                    .Where(x => x.ObjectType == "Organize" && outList.list.Select(x => x.id).Contains(x.UserId)).ToListAsync();

                // 处理组织树
                outList.list.Where(x => x.type.Equals("user")).ToList().ForEach(item =>
                {
                    var oids = userList.Where(x => x.UserId.Equals(item.id)).Select(x => x.ObjectId).ToList();
                    var oTree = orgList.Where(x => oids.Contains(x.Id)).Select(x => x.Description).ToList();
                    item.organize = string.Join(",", oTree);
                });
            }

            return PageResult<OrganizeMemberListOutput>.SqlSugarPageResult(outList);
        }
        else
        {
            var pOrganize = orgList.FirstOrDefault(x => x.Id.Equals(organizeId));

            output = await _organizeService.GetOrganizeMemberList(organizeId);
            if (pOrganize != null) output.ForEach(item => item.fullName = item.fullName.Replace(pOrganize.FullName + "/", string.Empty));
        }

        if (output.Any())
        {
            // 获取 所属组织的所有成员
            List<UserRelationEntity>? userList = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
                .Where(x => x.ObjectType == "Organize" && output.Select(x => x.id).Contains(x.UserId)).ToListAsync();

            // 处理组织树
            output.Where(x => x.type.Equals("user")).ToList().ForEach(item =>
            {
                var oids = userList.Where(x => x.UserId.Equals(item.id)).Select(x => x.ObjectId).ToList();
                var oTree = orgList.Where(x => oids.Contains(x.Id)).Select(x => x.Description).ToList();
                item.organize = string.Join(",", oTree);
            });
        }

        return new { list = output };
    }

    /// <summary>
    /// 获取下拉框 根据权限.
    /// </summary>
    /// <returns></returns>
    [HttpPost("GetListByAuthorize/{organizeId}")]
    public async Task<dynamic> GetListByAuthorize(string organizeId, [FromBody] KeywordInput input)
    {
        List<OrganizeMemberListOutput>? output = new List<OrganizeMemberListOutput>();
        if (!input.keyword.IsNullOrEmpty())
        {
            var dataScope = _userManager.DataScope.Where(x => x.Select).Select(x => x.organizeId).ToList();
            var userIds = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => dataScope.Contains(x.ObjectId)).Select(x => x.UserId).ToListAsync();

            output = await _repository.AsQueryable()
                .Where(u => u.Account.Contains(input.keyword) || u.RealName.Contains(input.keyword))
                .Where(u => u.EnabledMark > 0 && u.DeleteMark == null && userIds.Contains(u.Id)).OrderBy(o => o.SortCode)
                .Select(u => new OrganizeMemberListOutput
                {
                    id = u.Id,
                    fullName = SqlFunc.MergeString(u.RealName, "/", u.Account),
                    enabledMark = SqlFunc.IIF(u.EnabledMark == 2 && u.UnLockTime < DateTime.Now, 1, u.EnabledMark),
                    icon = "icon-ym icon-ym-tree-user2",
                    headIcon = SqlFunc.MergeString("/api/File/Image/userAvatar/", u.HeadIcon),
                    isLeaf = true,
                    hasChildren = false,
                    type = "user",
                }).Take(50).ToListAsync();

        }
        else
        {
            output = await GetOrganizeMemberList(organizeId);
        }

        if (output.Any())
        {
            var orgList = _organizeService.GetOrgListTreeName();
            // 获取所属组织的所有成员
            List<UserRelationEntity>? userList = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
                .Where(x => x.ObjectType == "Organize" && output.Select(x => x.id).Contains(x.UserId)).ToListAsync();

            // 处理组织树
            output.Where(x => x.type.Equals("user")).ToList().ForEach(item =>
            {
                var oids = userList.Where(x => x.UserId.Equals(item.id)).Select(x => x.ObjectId).ToList();
                var oTree = orgList.Where(x => oids.Contains(x.Id)).Select(x => x.Description).ToList();
                item.organize = string.Join(",", oTree);
            });
        }

        return new { list = output.DistinctBy(x => x.id).ToList() };
    }

    /// <summary>
    /// 获取当前用户下属成员.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("getSubordinates")]
    public async Task<dynamic> GetSubordinate([FromBody] KeywordInput input)
    {
        var res = await _repository.AsQueryable()
                   .WhereIF(!input.keyword.IsNullOrEmpty(), u => u.Account.Contains(input.keyword) || u.RealName.Contains(input.keyword))
                   .Where(u => u.EnabledMark == 1 && u.DeleteMark == null && u.ManagerId == _userManager.UserId).OrderBy(o => o.SortCode)
                   .Select(u => new OrganizeMemberListOutput
                   {
                       id = u.Id,
                       fullName = SqlFunc.MergeString(u.RealName, "/", u.Account),
                       enabledMark = u.EnabledMark,
                       icon = "icon-ym icon-ym-tree-user2",
                       headIcon = SqlFunc.MergeString("/api/File/Image/userAvatar/", u.HeadIcon),
                       isLeaf = true,
                       hasChildren = false,
                       type = "user",
                   }).ToListAsync();

        // ��ȡ������֯�����г�Ա
        List<UserRelationEntity>? userList = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
            .Where(x => res.Select(xx => xx.id).Contains(x.UserId)).ToListAsync();

        if (res.Any())
        {
            var orgList = _organizeService.GetOrgListTreeName();

            // ������֯��
            res.ForEach(item =>
            {
                var oids = userList.Where(x => x.UserId.Equals(item.id)).Select(x => x.ObjectId).ToList();
                var oTree = orgList.Where(x => oids.Contains(x.Id)).Select(x => x.Description).ToList();
                item.organize = string.Join(",", oTree);
            });
        }

        return res;
    }

    /// <summary>
    /// 获取当前用户所属机构下属成员.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpGet("GetUsersByPositionId")]
    public async Task<dynamic> GetUsersByPositionId([FromQuery] UserListQuery input)
    {
        List<OrganizeMemberListOutput>? outData = new List<OrganizeMemberListOutput>();
        UserEntity? user = _userManager.User;

        // 获取岗位所属组织信息
        OrganizeMemberListOutput? orgInfo = await _repository.AsSugarClient().Queryable<PositionEntity, OrganizeEntity>((a, b) =>
                new JoinQueryInfos(JoinType.Left, b.Id == a.OrganizeId && b.EnabledMark == 1 && b.DeleteMark == null))
            .Where((a, b) => a.Id == input.positionId).Select((a, b) => new OrganizeMemberListOutput
            {
                id = b.Id,
                fullName = b.FullName,
                enabledMark = b.EnabledMark,
                type = b.Category,
                parentId = "0",
                organize = b.Id,
                icon = b.Category.Equals("company") ? "icon-ym icon-ym-tree-organization3" : "icon-ym icon-ym-tree-department1",
                hasChildren = true,
                isLeaf = false
            }).FirstAsync();

        var orgList = _organizeService.GetOrgListTreeName();

        // 处理组织树
        if (orgInfo.organize.IsNotEmptyOrNull())
        {
            orgInfo.fullName = orgList.FirstOrDefault(x => x.Id.Equals(orgInfo.organize))?.Description;
        }

        outData.Add(orgInfo);

        // 获取岗位所属组织的所有成员
        List<OrganizeMemberListOutput>? userData = await _repository.AsSugarClient().Queryable<UserRelationEntity, UserEntity>((a, b) => new JoinQueryInfos(JoinType.Left, b.Id == a.UserId))
            .Where((a, b) => a.ObjectType == "Organize" && a.ObjectId == orgInfo.id && b.EnabledMark == 1 && b.DeleteMark == null)
            .WhereIF(!input.keyword.IsNullOrEmpty(), (a, b) => b.Account.Contains(input.keyword) || b.RealName.Contains(input.keyword))
            .Select((a, b) => new OrganizeMemberListOutput
            {
                id = b.Id,
                parentId = orgInfo.id,
                fullName = SqlFunc.MergeString(b.RealName, "/", b.Account),
                enabledMark = b.EnabledMark,
                icon = "icon-ym icon-ym-tree-user2",
                headIcon = SqlFunc.MergeString("/api/File/Image/userAvatar/", b.HeadIcon),
                isLeaf = true,
                hasChildren = false,
                type = "user"
            }).ToListAsync();

        // 获取 所属组织的所有成员
        List<UserRelationEntity>? userList = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
            .Where(x => x.ObjectType == "Organize" && userData.Select(x => x.id).Contains(x.UserId)).ToListAsync();

        // 处理组织树
        userData.ForEach(item =>
        {
            var oids = userList.Where(x => x.UserId.Equals(item.id)).Select(x => x.ObjectId).ToList();
            var oTree = orgList.Where(x => oids.Contains(x.Id)).Select(x => x.Description).ToList();
            item.organize = string.Join(",", oTree);
        });
        outData.AddRange(userData);

        return outData.ToTree("0");
    }

    /// <summary>
    /// 通过部门、岗位、用户、角色、分组id获取用户列表.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("UserCondition")]
    public async Task<dynamic> UserCondition([FromBody] UserConditionInput input)
    {
        SqlSugarPagedList<UserListOutput>? data = new SqlSugarPagedList<UserListOutput>();

        if (input.departIds == null) input.departIds = new List<string>();
        if (input.positionIds != null) input.departIds.AddRange(input.positionIds);
        if (input.roleIds != null) input.departIds.AddRange(input.roleIds);
        if (input.groupIds != null) input.departIds.AddRange(input.groupIds);
        if (data.list == null) data.list = new List<UserListOutput>();
        if (!input.departIds.Any()) return PageResult<UserListOutput>.SqlSugarPageResult(data);
        var ids = await _repository.AsSugarClient().Queryable<UserRelationEntity, UserEntity>((a, b) => new JoinQueryInfos(JoinType.Left, b.Id == a.UserId))
            .Where((a, b) => b.DeleteMark == null)
            .WhereIF(input.departIds.Any() || input.userIds.Any(), (a, b) => input.departIds.Contains(a.ObjectId) || input.userIds.Contains(b.Id))
            .WhereIF(input.pagination.keyword.IsNotEmptyOrNull(), (a, b) => b.Account.Contains(input.pagination.keyword) || b.RealName.Contains(input.pagination.keyword))
            .Select((a, b) => b.Id).Distinct().ToListAsync();
        data = await _repository.AsQueryable().Where(x => ids.Contains(x.Id)).Select(x => new UserListOutput()
        {
            id = x.Id,
            organizeId = x.OrganizeId,
            account = x.Account,
            fullName = SqlFunc.MergeString(x.RealName, "/", x.Account),
            headIcon = SqlFunc.MergeString("/api/File/Image/userAvatar/", x.HeadIcon),
            gender = x.Gender,
            mobilePhone = x.MobilePhone
        }).ToPagedListAsync(input.pagination.currentPage, input.pagination.pageSize);
        if (data.list.Any())
        {
            var orgList = _organizeService.GetOrgListTreeName();

            // 获取所属组织的所有成员
            List<UserRelationEntity>? userList = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
                .Where(x => x.ObjectType == "Organize" && data.list.Select(x => x.id).Contains(x.UserId)).ToListAsync();

            // 处理组织树
            data.list.ToList().ForEach(item =>
            {
                var oids = userList.Where(x => x.UserId.Equals(item.id)).Select(x => x.ObjectId).ToList();
                var oTree = orgList.Where(x => oids.Contains(x.Id)).Select(x => x.Description).ToList();
                item.organize = string.Join(",", oTree);
            });

        }

        return PageResult<UserListOutput>.SqlSugarPageResult(data);
    }

    /// <summary>
    /// 获取选中组织、岗位、角色、分组、用户基本信息.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("GetSelectedList")]
    public async Task<dynamic> GetSelectedList([FromBody] UserSelectedInput input)
    {
        return await _organizeService.GetSelectedList(input);
    }

    /// <summary>
    /// 获取用户基本信息.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("GetSelectedUserList")]
    public async Task<dynamic> GetSelectedUserList([FromBody] UserSelectedInput input)
    {
        var userId = new List<string>();
        input.ids.ForEach(item => userId.Add(item.Split("--").First()));
        var orgInfoList = _organizeService.GetOrgListTreeName();

        if (userId.Contains("@currentOrg"))
        {
            userId.Add(_userManager.User.OrganizeId);
            userId.Remove("@currentOrg");
        }
        if (userId.Contains("@currentOrgAndSubOrg"))
        {
            userId.AddRange(orgInfoList.TreeChildNode(_userManager.User.OrganizeId, t => t.Id, t => t.ParentId).Select(it => it.Id).ToList());
            userId.Remove("@currentOrgAndSubOrg");
        }
        if (userId.Contains("@currentGradeOrg"))
        {
            if (_userManager.IsAdministrator)
            {
                userId.AddRange(orgInfoList.Select(it => it.Id).ToList());
            }
            else
            {
                userId.AddRange(_userManager.DataScope.Select(x => x.organizeId).ToList());
            }
            userId.Remove("@currentGradeOrg");
        }

        var userIdList = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => userId.Contains(x.ObjectId) || userId.Contains(x.UserId)).Select(x => x.UserId).Distinct().ToListAsync();

        // 子组织
        //var childOrgIdList = new List<string>();
        //relIdList.Where(x => x.ObjectType.Equals("Organize")).Select(x => x.ObjectId).ToList().ForEach(item => childOrgIdList.AddRange(orgInfoList.Where(x => x.OrganizeIdTree.Contains(item)).Select(x => x.Id)));
        //userIdList.AddRange(await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => childOrgIdList.Contains(x.ObjectId) && x.ObjectType.Equals("Organize")).Select(x => x.UserId).ToListAsync());
        //userIdList.AddRange(userId);
        var userOrgList = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => userIdList.Contains(x.UserId) && x.ObjectType.Equals("Organize")).Select(x => new { x.ObjectId, x.UserId }).ToListAsync();
        var userInfoList = await _repository.AsSugarClient().Queryable<UserEntity>().Where(x => userIdList.Contains(x.Id) && x.DeleteMark == null && x.EnabledMark > 0)
            .WhereIF(input.pagination.keyword.IsNotEmptyOrNull(), x => x.RealName.Contains(input.pagination.keyword) || x.Account.Contains(input.pagination.keyword))
            .Select(x => new UserSelectedOutput
            {
                fullName = SqlFunc.MergeString(x.RealName, "/", x.Account),
                icon = "icon-ym icon-ym-tree-user2",
                headIcon = SqlFunc.MergeString("/api/File/Image/userAvatar/", x.HeadIcon),
                id = x.Id,
                type = "user",
                gender = x.Gender,
                mobilePhone = x.MobilePhone
            }).ToPagedListAsync(input.pagination.currentPage, input.pagination.pageSize);

        userInfoList.list.ToList().ForEach(item =>
        {
            item.organize = string.Join(",", orgInfoList.Where(o => userOrgList.Where(x => x.UserId.Equals(item.id)).Select(x => x.ObjectId).Contains(o.Id)).Select(x => x.Description));
            item.organizeIds = orgInfoList.Where(o => userOrgList.Where(x => x.UserId.Equals(item.id)).Select(x => x.ObjectId).Contains(o.Id)).Select(x => x.OrganizeIdTree).ToList();
        });

        return PageResult<UserSelectedOutput>.SqlSugarPageResult(userInfoList);
    }

    /// <summary>
    /// 新建.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("")]
    public async Task Create([FromBody] UserCrInput input)
    {
        if (_tenant.MultiTenancy)
        {
            var tenatInfo = await _tenantManager.GetTenant(_userManager.TenantId);

            // 是否超过租户账号额度
            if (tenatInfo.accountNum != 0 && tenatInfo.accountNum <= await _repository.AsQueryable().CountAsync(x => x.DeleteMark == null))
                throw Oops.Oh(ErrorCode.D1041);
        }

        var orgids = input.organizeId.Split(',');
        if (!_userManager.DataScope.Any(it => orgids.Contains(it.organizeId) && it.Add) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);
        if (await _repository.IsAnyAsync(u => u.Account == input.account && u.DeleteMark == null)) throw Oops.Oh(ErrorCode.D1003);
        UserEntity? entity = input.Adapt<UserEntity>();

        #region 用户表单

        entity.IsAdministrator = 0;
        entity.EntryDate = input.entryDate.IsNullOrEmpty() ? null : input.entryDate;
        entity.Birthday = input.birthday.IsNullOrEmpty() ? null : input.birthday;
        entity.QuickQuery = PinyinHelper.PinyinString(input.realName);
        entity.Secretkey = Guid.NewGuid().ToString();

        var defaultPassWord = await _repository.AsSugarClient().Queryable<SysConfigEntity>()
            .Where(it => it.Key.Equals("newUserDefaultPassword"))
            .Select(it => it.Value)
            .FirstAsync();
        entity.Password = MD5Encryption.Encrypt(MD5Encryption.Encrypt(defaultPassWord) + entity.Secretkey);
        string? headIcon = input.headIcon?.Split('/').ToList().Last();
        if (string.IsNullOrEmpty(headIcon))
            headIcon = "001.png";
        entity.HeadIcon = headIcon;

        // 多组织
        string[]? orgList = entity.OrganizeId.Split(",");
        entity.OrganizeId = orgList.FirstOrDefault();
        string[]? positionIds = entity.PositionId?.Split(",");
        List<string>? pIdList = await _repository.AsSugarClient().Queryable<PositionEntity>().Where(x => x.OrganizeId == entity.OrganizeId && positionIds.Contains(x.Id)).Select(x => x.Id).ToListAsync();
        entity.PositionId = pIdList.FirstOrDefault(); // 多 岗位 默认取当前组织第一个

        #endregion

        try
        {
            // 新增用户记录
            await _repository.AsInsertable(entity).CallEntityMethod(m => m.Creator()).ExecuteCommandAsync();

            // 将临时文件迁移至正式文件
            FileHelper.MoveFile(Path.Combine(FileVariable.TemporaryFilePath, headIcon), Path.Combine(FileVariable.UserAvatarFilePath, headIcon));

            List<UserRelationEntity>? userRelationList = new List<UserRelationEntity>();
            userRelationList.AddRange(_userRelationService.CreateUserRelation(entity.Id, input.roleId, "Role"));
            userRelationList.AddRange(_userRelationService.CreateUserRelation(entity.Id, input.positionId, "Position"));
            userRelationList.AddRange(_userRelationService.CreateUserRelation(entity.Id, input.organizeId, "Organize"));
            userRelationList.AddRange(_userRelationService.CreateUserRelation(entity.Id, input.groupId, "Group"));

            if (userRelationList.Count > 0) await _userRelationService.Create(userRelationList); // 批量新增用户关系

            #region 第三方同步

            try
            {
                SysConfigOutput? sysConfig = await _sysConfigService.GetInfo();
                List<UserEntity>? userList = new List<UserEntity>();
                userList.Add(entity);
                if (sysConfig.dingSynIsSynUser)
                    await _synThirdInfoService.SynUser(2, 3, sysConfig, userList);
                if (sysConfig.qyhIsSynUser)
                    await _synThirdInfoService.SynUser(1, 3, sysConfig, userList);
            }
            catch (Exception)
            {
            }

            #endregion

            // 单点登录同步
            entity.Password = defaultPassWord;
            await syncUserInfo(entity, "create", _userManager.TenantId);
        }
        catch (Exception)
        {
            throw Oops.Bah(ErrorCode.D5001);
        }

    }

    /// <summary>
    /// 删除.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    public async Task Delete(string id)
    {
        UserEntity? entity = await _repository.GetFirstAsync(u => u.Id == id && u.DeleteMark == null);

        // 所属组织 分级权限验证
        List<string>? orgIdList = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.UserId == id && x.ObjectType == "Organize").Select(x => x.ObjectId).ToListAsync();
        if (!_userManager.DataScope.Any(it => orgIdList.Contains(it.organizeId) && it.Delete) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        if (await _organizeService.GetIsManagerByUserId(id))
            throw Oops.Oh(ErrorCode.D2003);
        _ = entity ?? throw Oops.Oh(ErrorCode.D5002);
        if (entity.IsAdministrator == (int)AccountType.Administrator)
            throw Oops.Oh(ErrorCode.D1014);
        if (entity.Id == _userManager.UserId)
            throw Oops.Oh(ErrorCode.D1001);
        entity.DeleteTime = DateTime.Now;
        entity.DeleteMark = 1;
        entity.DeleteUserId = _userManager.UserId;

        // 用户软删除
        await _repository.AsUpdateable(entity).UpdateColumns(it => new { it.DeleteTime, it.DeleteMark, it.DeleteUserId }).ExecuteCommandAsync();

        // 直接删除用户关系表相关相关数据
        await _userRelationService.Delete(id);

        await ForcedOffline(entity, true);

        #region 第三方同步

        try
        {
            SysConfigOutput? sysConfig = await _sysConfigService.GetInfo();
            if (sysConfig.dingSynIsSynUser)
                await _synThirdInfoService.DelSynData(2, 3, sysConfig, id);
            if (sysConfig.qyhIsSynUser)
                await _synThirdInfoService.DelSynData(1, 3, sysConfig, id);
        }
        catch (Exception)
        {
        }

        #endregion

        // 单点登录同步
        await syncUserInfo(entity, "delete", _userManager.TenantId);
    }

    /// <summary>
    /// 更新.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPut("{id}")]
    public async Task Update(string id, [FromBody] UserUpInput input)
    {
        UserEntity? oldUserEntity = await _repository.GetFirstAsync(it => it.Id == id);
        input.roleId = input.roleId == null ? string.Empty : input.roleId;

        // 超级管理员 只有 admin 账号才有变更权限
        if (_userManager.UserId != oldUserEntity.Id && oldUserEntity.IsAdministrator == 1 && _userManager.Account != "admin")
            throw Oops.Oh(ErrorCode.D1033);

        // 超级管理员不能禁用
        if (oldUserEntity.IsAdministrator.Equals(1) && input.enabledMark.Equals(0))
            throw Oops.Oh(ErrorCode.D1015);

        // 旧数据
        List<string>? orgIdList = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.UserId == id && x.ObjectType == "Organize").Select(x => x.ObjectId).ToListAsync();
        if (!_userManager.DataScope.Any(it => orgIdList.Contains(it.organizeId) && it.Edit) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        // 新数据
        var orgids = input.organizeId.Split(',');
        if (!_userManager.DataScope.Any(it => orgids.Contains(it.organizeId) && it.Edit) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        // 排除自己并且判断与其他是否相同
        if (await _repository.IsAnyAsync(u => u.Account == input.account && u.DeleteMark == null && u.Id != id)) throw Oops.Oh(ErrorCode.D1003);
        if (id == input.managerId) throw Oops.Oh(ErrorCode.D1021);

        // 直属主管的上级不能为自己的下属
        if (await GetIsMyStaff(id, input.managerId, 10)) throw Oops.Oh(ErrorCode.D1026);
        UserEntity? entity = input.Adapt<UserEntity>();
        entity.QuickQuery = PinyinHelper.PinyinString(input.realName);
        string? headIcon = input.headIcon.Split('/').ToList().Last();
        entity.HeadIcon = headIcon;
        entity.LastModifyTime = DateTime.Now;
        entity.LastModifyUserId = _userManager.UserId;
        entity.SystemId = oldUserEntity.SystemId;
        entity.AppSystemId = oldUserEntity.AppSystemId;
        if (entity.EnabledMark == 2) entity.UnLockTime = null;

        // 多 组织
        if (orgids.Contains(oldUserEntity.OrganizeId)) entity.OrganizeId = oldUserEntity.OrganizeId;
        else entity.OrganizeId = orgids.FirstOrDefault();

        // 获取默认组织下的岗位
        string[]? positionIds = entity.PositionId?.Split(",");
        List<string>? pIdList = await _repository.AsSugarClient().Queryable<PositionEntity>().Where(x => x.OrganizeId == entity.OrganizeId && positionIds.Contains(x.Id)).Select(x => x.Id).ToListAsync();

        if (entity.PositionId.IsNotEmptyOrNull() && pIdList.Contains(oldUserEntity.PositionId)) entity.PositionId = oldUserEntity.PositionId;
        else entity.PositionId = pIdList.FirstOrDefault(); // 多 岗位 默认取第一个

        try
        {
            // 更新用户记录
            int newEntity = await _repository.AsUpdateable(entity).UpdateColumns(it => new {
                it.Account,
                it.RealName,
                it.QuickQuery,
                it.Gender,
                it.Email,
                it.OrganizeId,
                it.ManagerId,
                it.PositionId,
                it.RoleId,
                it.SortCode,
                it.EnabledMark,
                it.Description,
                it.HeadIcon,
                it.Nation,
                it.NativePlace,
                it.EntryDate,
                it.CertificatesType,
                it.CertificatesNumber,
                it.Education,
                it.UrgentContacts,
                it.UrgentTelePhone,
                it.PostalAddress,
                it.MobilePhone,
                it.Birthday,
                it.TelePhone,
                it.Landline,
                it.UnLockTime,
                it.GroupId,
                it.Ranks,
                it.LastModifyTime,
                it.SystemId,
                it.LastModifyUserId
            }).ExecuteCommandAsync();

            // 将临时文件迁移至正式文件
            FileHelper.MoveFile(Path.Combine(FileVariable.TemporaryFilePath, headIcon), Path.Combine(FileVariable.UserAvatarFilePath, headIcon));

            // 捞取用户分组
            var userGroupIds = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.UserId.Equals(id) && x.ObjectType.Equals("Group")).Select(x => x.ObjectId).ToListAsync();
            if (userGroupIds != null && userGroupIds.Any()) input.groupId = string.Join(",", userGroupIds);

            // 直接删除用户关系表相关相关数据
            await _userRelationService.Delete(id);

            List<UserRelationEntity>? userRelationList = new List<UserRelationEntity>();
            userRelationList.AddRange(_userRelationService.CreateUserRelation(id, entity.RoleId, "Role"));
            userRelationList.AddRange(_userRelationService.CreateUserRelation(id, input.positionId, "Position"));
            userRelationList.AddRange(_userRelationService.CreateUserRelation(id, input.organizeId, "Organize"));
            userRelationList.AddRange(_userRelationService.CreateUserRelation(id, input.groupId, "Group"));
            if (userRelationList.Count > 0) await _userRelationService.Create(userRelationList); // 批量新增用户关系

            await ForcedOffline(oldUserEntity, false);
        }
        catch (Exception)
        {
            FileHelper.MoveFile(Path.Combine(FileVariable.UserAvatarFilePath, headIcon), Path.Combine(FileVariable.TemporaryFilePath, headIcon));
            throw Oops.Oh(ErrorCode.D5004);
        }

        #region 第三方同步

        try
        {
            SysConfigOutput? sysConfig = await _sysConfigService.GetInfo();
            List<UserEntity>? userList = new List<UserEntity>();
            userList.Add(entity);
            if (sysConfig.dingSynIsSynUser)
                await _synThirdInfoService.SynUser(2, 3, sysConfig, userList);
            if (sysConfig.qyhIsSynUser)
                await _synThirdInfoService.SynUser(1, 3, sysConfig, userList);
        }
        catch (Exception)
        {
        }

        #endregion

        // 单点登录同步
        await syncUserInfo(entity, "update", _userManager.TenantId);
    }

    /// <summary>
    /// 更新状态.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <returns></returns>
    [HttpPut("{id}/Actions/State")]
    public async Task UpdateState(string id)
    {
        UserEntity? entity = await _repository.GetFirstAsync(it => it.Id == id);
        if (!_userManager.DataScope.Any(it => it.organizeId == entity.OrganizeId && it.Edit == true) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);
        if (!await _repository.IsAnyAsync(u => u.Id == id && u.DeleteMark == null))
            throw Oops.Oh(ErrorCode.D1002);
        int isOk = await _repository.AsUpdateable().SetColumns(it => new UserEntity()
        {
            EnabledMark = SqlFunc.IIF(it.EnabledMark == 1, 0, 1),
            LastModifyUserId = _userManager.UserId,
            LastModifyTime = SqlFunc.GetDate()
        }).Where(it => it.Id == id).ExecuteCommandAsync();

        if (!(isOk > 0)) throw Oops.Oh(ErrorCode.D5005);
    }

    /// <summary>
    /// 重置密码.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpPost("{id}/Actions/ResetPassword")]
    public async Task ResetPassword(string id, [FromBody] UserResetPasswordInput input)
    {
        UserEntity? entity = await _repository.GetFirstAsync(u => u.Id == id && u.DeleteMark == null);

        // 所属组织 分级权限验证
        List<string>? orgIdList = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.UserId == id && x.ObjectType == "Organize").Select(x => x.ObjectId).ToListAsync();
        if (!_userManager.DataScope.Any(it => orgIdList.Contains(it.organizeId) && it.Edit) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);

        if (!input.userPassword.Equals(input.validatePassword))
            throw Oops.Oh(ErrorCode.D5006);
        _ = entity ?? throw Oops.Oh(ErrorCode.D1002);

        string? password = MD5Encryption.Encrypt(input.userPassword + entity.Secretkey);

        int isOk = await _repository.AsUpdateable().SetColumns(it => new UserEntity()
        {
            Password = password,
            ChangePasswordDate = SqlFunc.GetDate(),
            LastModifyUserId = _userManager.UserId,
            LastModifyTime = SqlFunc.GetDate()
        }).Where(it => it.Id == id).ExecuteCommandAsync();

        if (!(isOk > 0)) throw Oops.Oh(ErrorCode.D5005);

        // 重置该用户密码，该用户会立即退出登录
        var onlineCacheKey = string.Format("{0}:{1}", CommonConst.CACHEKEYONLINEUSER, _userManager.TenantId);
        var list = await _cacheManager.GetAsync<List<UserOnlineModel>>(onlineCacheKey);
        var user = list.Find(it => it.tenantId == _userManager.TenantId && it.userId == id);
        if (user != null)
        {
            await _imHandler.SendMessageAsync(user.connectionId, new { method = "logout", msg = "密码已变更，请重新登录！" }.ToJsonString());

            // 删除在线用户ID
            list.RemoveAll((x) => x.connectionId == user.connectionId);
            await _cacheManager.SetAsync(onlineCacheKey, list);

            // 删除用户登录信息缓存
            var cacheKey = string.Format("{0}:{1}:{2}", _userManager.TenantId, CommonConst.CACHEKEYUSER, user.userId);
            await _cacheManager.DelAsync(cacheKey);
        }

        // 单点登录同步
        entity.Password = input.userPassword;
        await syncUserInfo(entity, "modifyPassword", _userManager.TenantId);
    }

    /// <summary>
    /// 解除锁定.
    /// </summary>
    /// <param name="id">主键.</param>
    /// <returns></returns>
    [HttpPut("{id}/Actions/Unlock")]
    public async Task Unlock(string id)
    {
        UserEntity? entity = await _repository.GetFirstAsync(u => u.Id == id && u.DeleteMark == null);
        if (!_userManager.DataScope.Any(it => it.organizeId == entity.OrganizeId && it.Edit) && !_userManager.IsAdministrator)
            throw Oops.Oh(ErrorCode.D1013);
        int isOk = await _repository.AsUpdateable().SetColumns(it => new UserEntity()
        {
            LockMark = 0, // 解锁
            LogErrorCount = 0, // 解锁
            EnabledMark = 1, // 解锁
            UnLockTime = DateTime.Now, // 取消解锁时间
            LastModifyUserId = _userManager.UserId,
            LastModifyTime = SqlFunc.GetDate()
        }).Where(it => it.Id == id).ExecuteCommandAsync();

        if (!(isOk > 0)) throw Oops.Oh(ErrorCode.D5005);
    }

    /// <summary>
    /// 导出Excel.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpGet("ExportData")]
    public async Task<dynamic> ExportData([FromQuery] UserExportDataInput input)
    {
        // 获取分级管理组织
        var dataScope = _userManager.DataScope.Where(x => x.Select).Select(x => x.organizeId).Distinct().ToList();

        // 处理组织树 名称
        List<OrganizeEntity>? orgTreeNameList = _organizeService.GetOrgListTreeName();

        #region 获取组织层级

        List<string>? childOrgIds = new List<string>();
        if (input.organizeId.IsNotEmptyOrNull())
        {
            childOrgIds.Add(input.organizeId);

            // 根据组织Id 获取所有子组织Id集合
            childOrgIds.AddRange(orgTreeNameList.Where(x => x.OrganizeIdTree.Contains(input.organizeId)).Select(x => x.Id).ToList());
            childOrgIds = childOrgIds.Distinct().ToList();
        }

        #endregion

        // 用户信息列表
        List<UserListImportDataInput>? userList = new List<UserListImportDataInput>();
        var dictionaryTypeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().FirstAsync(x => x.EnCode == "certificateType" && x.DeleteMark == null && x.EnabledMark == 1);
        var dictionaryTypeEntity1 = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().FirstAsync(x => x.EnCode == "Education" && x.DeleteMark == null && x.EnabledMark == 1);
        var dictionaryTypeEntity2 = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().FirstAsync(x => x.EnCode == "sex" && x.DeleteMark == null && x.EnabledMark == 1);
        var dictionaryTypeEntity3 = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().FirstAsync(x => x.EnCode == "Nation" && x.DeleteMark == null && x.EnabledMark == 1);
        var dictionaryTypeEntity4 = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().FirstAsync(x => x.EnCode == "Rank" && x.DeleteMark == null && x.EnabledMark == 1);
        var query = _repository.AsQueryable()
            .Where(a => a.DeleteMark == null && !a.Account.Equals("admin"))
            .WhereIF(input.enabledMark != null, a => a.EnabledMark.Equals(input.enabledMark))
            .WhereIF(input.gender != null, a => a.Gender.Equals(input.gender))
            .WhereIF(childOrgIds.Any(), a => SqlFunc.Subqueryable<UserRelationEntity>().EnableTableFilter().Where(x => childOrgIds.Contains(x.ObjectId) && x.UserId.Equals(a.Id)).Any())
            .WhereIF(!input.keyword.IsNullOrEmpty(), a => a.Account.Contains(input.keyword) || a.RealName.Contains(input.keyword))
            .WhereIF(!_userManager.IsAdministrator, a => SqlFunc.Subqueryable<UserRelationEntity>().EnableTableFilter().Where(x => dataScope.Contains(x.ObjectId) && x.UserId.Equals(a.Id)).Any())
            .OrderBy(a => a.SortCode).OrderBy(a => a.CreatorTime, OrderByType.Desc).OrderByIF(!input.keyword.IsNullOrEmpty(), a => a.LastModifyTime, OrderByType.Desc)
            .Select(a => new UserListImportDataInput()
            {
                id = a.Id,
                account = a.Account,
                realName = a.RealName,
                birthday = a.Birthday.Value.ToString("yyyy-MM-dd"),
                certificatesNumber = a.CertificatesNumber,
                managerId = SqlFunc.Subqueryable<UserEntity>().EnableTableFilter().Where(u => u.Id == a.ManagerId && u.DeleteMark == null && u.EnabledMark == 1).Select(u => SqlFunc.MergeString(u.RealName, "/", u.Account)),
                organizeId = a.OrganizeId, // 组织结构
                positionId = a.PositionId, // 岗位
                roleId = a.RoleId, // 多角色
                certificatesType = SqlFunc.Subqueryable<DictionaryDataEntity>().EnableTableFilter().Where(d => d.DictionaryTypeId == dictionaryTypeEntity.Id && d.Id == a.CertificatesType && d.DeleteMark == null && d.EnabledMark == 1).Select(d => d.FullName),
                education = SqlFunc.Subqueryable<DictionaryDataEntity>().EnableTableFilter().Where(d => d.DictionaryTypeId == dictionaryTypeEntity1.Id && d.Id == a.Education && d.DeleteMark == null && d.EnabledMark == 1).Select(d => d.FullName),
                gender = SqlFunc.Subqueryable<DictionaryDataEntity>().EnableTableFilter().Where(d => d.DictionaryTypeId == dictionaryTypeEntity2.Id && d.EnCode == a.Gender && d.DeleteMark == null && d.EnabledMark == 1).Select(d => d.FullName),
                nation = SqlFunc.Subqueryable<DictionaryDataEntity>().EnableTableFilter().Where(d => d.DictionaryTypeId == dictionaryTypeEntity3.Id && d.Id == a.Nation && d.DeleteMark == null && d.EnabledMark == 1).Select(d => d.FullName),
                description = a.Description,
                entryDate = a.EntryDate.Value.ToString("yyyy-MM-dd"),
                email = a.Email,
                enabledMark = SqlFunc.IF(a.EnabledMark.Equals(0)).Return("禁用").ElseIF(a.EnabledMark.Equals(1)).Return("启用").End("锁定"),
                mobilePhone = a.MobilePhone,
                nativePlace = a.NativePlace,
                postalAddress = a.PostalAddress,
                telePhone = a.TelePhone,
                urgentContacts = a.UrgentContacts,
                urgentTelePhone = a.UrgentTelePhone,
                landline = a.Landline,
                ranks = SqlFunc.Subqueryable<DictionaryDataEntity>().EnableTableFilter().Where(d => d.DictionaryTypeId == dictionaryTypeEntity4.Id && d.Id == a.Ranks && d.DeleteMark == null && d.EnabledMark == 1).Select(d => d.FullName),
                sortCode = a.SortCode.ToString()
            });
        if (input.dataType.Equals("0"))
        {
            userList = (await query.ToPagedListAsync(input.currentPage, input.pageSize)).list.ToList();
        }
        else
        {
            userList = await query.ToListAsync();
        }

        userList.ForEach(item =>
        {
            if (item.birthday.IsNotEmptyOrNull()) item.birthday = Convert.ToDateTime(item.birthday).ToString("yyyy-MM-dd");
            else item.birthday = string.Empty;
            if (item.entryDate.IsNotEmptyOrNull()) item.entryDate = Convert.ToDateTime(item.entryDate).ToString("yyyy-MM-dd");
            else item.entryDate = string.Empty;
        });

        List<PositionEntity>? plist = await _repository.AsSugarClient().Queryable<PositionEntity>().Where(it => it.EnabledMark == 1 && it.DeleteMark == null).ToListAsync(); // 获取所有岗位
        List<RoleEntity>? rlist = await _repository.AsSugarClient().Queryable<RoleEntity>().Where(it => it.EnabledMark == 1 && it.DeleteMark == null).ToListAsync(); // 获取所有角色

        // 获取用户组织关联数据
        var userRelation = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.ObjectType == "Organize" || x.ObjectType == "Position").Where(x => userList.Select(xx => xx.id).Contains(x.UserId))
            .Select(x => new { x.ObjectId, x.ObjectType, x.UserId }).ToListAsync();

        // 转换 组织结构 和 岗位(多岗位)
        foreach (UserListImportDataInput? item in userList)
        {
            // 获取用户组织关联数据
            List<string>? orgRelList = userRelation.Where(x => x.ObjectType == "Organize" && x.UserId == item.id).Select(x => x.ObjectId).ToList();

            if (orgRelList.Any())
            {
                List<OrganizeEntity>? oentityList = orgTreeNameList.Where(x => orgRelList.Contains(x.Id)).ToList();
                if (oentityList.Any())
                {
                    List<string>? userOrgList = new List<string>();
                    oentityList.ForEach(oentity => userOrgList.Add(oentity.Description));
                    item.organizeId = string.Join(",", userOrgList);
                }
            }
            else
            {
                item.organizeId = string.Empty;
            }

            // 获取用户岗位关联
            List<string>? posRelList = userRelation.Where(x => x.ObjectType == "Position" && x.UserId == item.id).Select(x => x.ObjectId).ToList();
            if (posRelList.Any())
                item.positionId = string.Join(",", plist.Where(x => posRelList.Contains(x.Id)).Select(x => x.FullName + "/" + x.EnCode).ToList());
            else
                item.positionId = string.Empty;

            // 角色
            if (item.roleId.IsNotEmptyOrNull())
            {
                List<string>? ridList = item.roleId.Split(',').ToList();
                item.roleId = string.Join(",", rlist.Where(x => ridList.Contains(x.Id)).Select(x => x.FullName).ToList());
            }
        }

        ExcelConfig excelconfig = new ExcelConfig();
        excelconfig.FileName = string.Format("用户信息_{0:yyyyMMddhhmmss}.xls", DateTime.Now);
        excelconfig.HeadFont = "微软雅黑";
        excelconfig.HeadPoint = 10;
        excelconfig.IsAllSizeColumn = true;
        excelconfig.IsBold = true;
        excelconfig.IsAllBorder = true;
        excelconfig.ColumnModel = new List<ExcelColumnModel>();
        foreach (var item in GetUserInfoFieldToTitle(input.selectKey.Split(',').ToList()))
        {
            excelconfig.ColumnModel.Add(new ExcelColumnModel() { Column = item.ColumnKey, ExcelColumn = item.ColumnValue, Required = item.Required });
        }

        string? addPath = Path.Combine(FileVariable.TemporaryFilePath, excelconfig.FileName);
        var fs = ExcelExportHelper<UserListImportDataInput>.ExportMemoryStream(userList, excelconfig);
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
        List<UserListImportDataInput>? dataList = new List<UserListImportDataInput>() { new UserListImportDataInput() {
            organizeId = "公司名称/公司名称1/部门名称,公司名称/公司名称1/部门名称1",
            positionId = "岗位名称/岗位编码,岗位名称1/岗位编码1", roleId = "角色名称/角色编码,角色名称1/角色编码1",
            managerId = "姓名/账户", birthday = "yyyy-MM-dd", entryDate = "yyyy-MM-dd" } };

        ExcelConfig excelconfig = new ExcelConfig();
        excelconfig.FileName = "用户信息导入模板.xls";
        excelconfig.HeadFont = "微软雅黑";
        excelconfig.HeadPoint = 10;
        excelconfig.IsAllSizeColumn = true;
        excelconfig.IsBold = true;
        excelconfig.IsAllBorder = true;
        excelconfig.IsAnnotation = true;
        excelconfig.ColumnModel = new List<ExcelColumnModel>();
        var userInfoFields = GetUserInfoFieldToTitle();
        userInfoFields.RemoveAll(x => x.ColumnKey.Equals("errorsInfo"));
        foreach (var item in userInfoFields)
        {
            excelconfig.ColumnModel.Add(new ExcelColumnModel() { Column = item.ColumnKey, ExcelColumn = item.ColumnValue, Required = item.Required, SelectList = item.SelectList });
        }

        string? addPath = Path.Combine(FileVariable.TemporaryFilePath, excelconfig.FileName);
        var stream = ExcelExportHelper<UserListImportDataInput>.ToStream(dataList, excelconfig);
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
            var FileEncode = GetUserInfoFieldToTitle();

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
    public async Task<dynamic> ExportExceptionData([FromBody] UserImportDataInput list)
    {
        list.list.ForEach(it => it.errorsInfo = string.Empty);
        object[]? res = await ImportUserData(list.list);

        // 错误数据
        List<UserListImportDataInput>? errorlist = res.Last() as List<UserListImportDataInput>;

        ExcelConfig excelconfig = new ExcelConfig();
        excelconfig.FileName = string.Format("用户信息导入模板错误报告_{0}.xls", DateTime.Now.ToString("yyyyMMddHHmmss"));
        excelconfig.HeadFont = "微软雅黑";
        excelconfig.HeadPoint = 10;
        excelconfig.IsAllSizeColumn = true;
        excelconfig.IsBold = true;
        excelconfig.IsAllBorder = true;
        excelconfig.IsAnnotation = true;
        excelconfig.ColumnModel = new List<ExcelColumnModel>();
        foreach (var item in GetUserInfoFieldToTitle())
        {
            excelconfig.ColumnModel.Add(new ExcelColumnModel() { Column = item.ColumnKey, ExcelColumn = item.ColumnValue, FreezePane = item.FreezePane, Required = item.Required, SelectList = item.SelectList });
        }

        string? addPath = Path.Combine(FileVariable.TemporaryFilePath, excelconfig.FileName);
        ExcelExportHelper<UserListImportDataInput>.Export(errorlist, excelconfig, addPath);

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
    public async Task<dynamic> ImportData([FromBody] UserImportDataInput list)
    {
        list.list.ForEach(x => x.errorsInfo = string.Empty);
        object[]? res = await ImportUserData(list.list);
        List<UserEntity>? addlist = res.First() as List<UserEntity>;
        List<UserListImportDataInput>? errorlist = res.Last() as List<UserListImportDataInput>;
        return new UserImportResultOutput() { snum = addlist.Count, fnum = errorlist.Count, failResult = errorlist, resultType = errorlist.Count < 1 ? 0 : 1 };
    }

    /// <summary>
    /// 保存工作交接.
    /// </summary>
    /// <param name="input">主键.</param>
    /// <returns></returns>
    [HttpPost("workHandover")]
    public async Task SaveWorkHandover([FromBody] UserWorkHandoverInput input)
    {
        try
        {
            if (input.toId.Equals(input.fromId)) throw Oops.Oh(ErrorCode.D5027);
            if (await _repository.IsAnyAsync(x => x.Id.Equals(input.toId) && x.Account.Equals("admin"))) throw Oops.Oh(ErrorCode.D5028);

            // 记录 被交接人Id
            int isOk = await _repository.AsUpdateable().SetColumns(it => new UserEntity()
            {
                HandoverUserId = input.fromId,
                LastModifyUserId = _userManager.UserId,
                LastModifyTime = SqlFunc.GetDate()
            }).Where(it => it.Id == input.toId).ExecuteCommandAsync();

            // 交接权限组
            if (input.permissionList != null && input.permissionList.Any())
            {
                var pList = await _repository.AsSugarClient().Queryable<PermissionGroupEntity>().Where(x => input.permissionList.Contains(x.Id)).ToListAsync();
                pList.ForEach(item =>
                {
                    var itemPList = item.PermissionMember?.Split(',').ToList();
                    itemPList.Add(input.toId + "--user");
                    itemPList.Remove(input.fromId + "--user");
                    item.PermissionMember = string.Join(",", itemPList);
                });

                await _repository.AsSugarClient().Updateable(pList).UpdateColumns(it => new { it.PermissionMember }).ExecuteCommandAsync();
            }

            // 待办
            if (input.flowTaskList != null && input.flowTaskList.Any()) _flowTaskRepository.SaveWorkHandover(input.toId, input.flowTaskList, 1, input.fromId);

            // 负责流程
            if (input.flowList != null && input.flowList.Any()) _flowTaskRepository.SaveWorkHandover(input.toId, input.flowList, 2, input.fromId);

        }
        catch (Exception e)
        {
            throw e;
        }
    }

    #endregion

    #region PublicMethod

    /// <summary>
    /// 获取用户信息 根据用户ID.
    /// </summary>
    /// <param name="userId">用户ID</param>
    /// <returns></returns>
    [NonAction]
    public UserEntity GetInfoByUserId(string userId)
    {
        return _repository.GetFirst(u => u.Id == userId && u.DeleteMark == null);
    }

    /// <summary>
    /// 获取用户信息 根据用户ID.
    /// </summary>
    /// <param name="userId">用户ID.</param>
    /// <returns></returns>
    [NonAction]
    public async Task<UserEntity> GetInfoByUserIdAsync(string userId)
    {
        return await _repository.GetFirstAsync(u => u.Id == userId && u.DeleteMark == null);
    }

    /// <summary>
    /// 获取用户列表.
    /// </summary>
    /// <returns></returns>
    [NonAction]
    public async Task<List<UserEntity>> GetList()
    {
        return await _repository.AsQueryable().Where(u => u.DeleteMark == null).ToListAsync();
    }

    /// <summary>
    /// 获取用户信息 根据用户账户.
    /// </summary>
    /// <param name="account">用户账户.</param>
    /// <returns></returns>
    [NonAction]
    public async Task<UserEntity> GetInfoByAccount(string account)
    {
        return await _repository.GetFirstAsync(u => u.Account == account && u.DeleteMark == null);
    }

    /// <summary>
    /// 获取用户信息 根据登录信息.
    /// </summary>
    /// <param name="account">用户账户.</param>
    /// <param name="password">用户密码.</param>
    /// <returns></returns>
    [NonAction]
    public async Task<UserEntity> GetInfoByLogin(string account, string password)
    {
        return await _repository.GetFirstAsync(u => u.Account == account && u.Password == password && u.DeleteMark == null);
    }

    /// <summary>
    /// 根据用户姓名获取用户ID.
    /// </summary>
    /// <param name="realName">用户姓名.</param>
    /// <returns></returns>
    [NonAction]
    public async Task<string> GetUserIdByRealName(string realName)
    {
        return (await _repository.GetFirstAsync(u => u.RealName == realName && u.DeleteMark == null)).Id;
    }

    /// <summary>
    /// 获取用户名.
    /// </summary>
    /// <param name="userId">用户id.</param>
    /// <param name="isAccount">是否显示账号.</param>
    /// <returns></returns>
    [NonAction]
    public async Task<string> GetUserName(string userId, bool isAccount = true)
    {
        UserEntity? entity = await _repository.GetFirstAsync(x => x.Id == userId && x.DeleteMark == null);
        if (entity.IsNullOrEmpty()) return string.Empty;
        return isAccount ? entity.RealName + "/" + entity.Account : entity.RealName;
    }

    /// <summary>
    /// 获取当前用户岗位信息.
    /// </summary>
    /// <param name="PositionIds"></param>
    /// <returns></returns>
    [NonAction]
    public async Task<List<PositionInfoModel>> GetPosition(string organizeId)
    {
        return await _repository.AsSugarClient().Queryable<PositionEntity, UserRelationEntity>((a, b) => new JoinQueryInfos(JoinType.Left, a.Id.Equals(b.ObjectId) && b.ObjectType.Equals("Position"))).Where((a, b) => a.OrganizeId.Equals(organizeId) && b.UserId.Equals(_userManager.UserId)).Select(a => new PositionInfoModel { id = a.Id, name = a.FullName }).ToListAsync();
    }

    /// <summary>
    /// 表达式获取用户.
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    [NonAction]
    public async Task<UserEntity> GetUserByExp(Expression<Func<UserEntity, bool>> expression)
    {
        return await _repository.GetFirstAsync(expression);
    }

    /// <summary>
    /// 表达式获取用户列表.
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    [NonAction]
    public async Task<List<UserEntity>> GetUserListByExp(Expression<Func<UserEntity, bool>> expression)
    {
        return await _repository.AsQueryable().Where(expression).ToListAsync();
    }

    /// <summary>
    /// 表达式获取指定字段的用户列表.
    /// </summary>
    /// <param name="expression">where 条件表达式.</param>
    /// <param name="select">select 选择字段表达式.</param>
    /// <returns></returns>
    [NonAction]
    public async Task<List<UserEntity>> GetUserListByExp(Expression<Func<UserEntity, bool>> expression, Expression<Func<UserEntity, UserEntity>> select)
    {
        return await _repository.AsQueryable().Where(expression).Select(select).ToListAsync();
    }

    #endregion

    #region PrivateMethod

    /// <summary>
    /// 获取集合中的组织 树,根据上级ID.
    /// </summary>
    /// <param name="list">组织 集合.</param>
    /// <param name="parentId">上级ID.</param>
    /// <param name="addList">返回.</param>
    /// <returns></returns>
    private List<string> GetOrganizeParentName(List<OrganizeEntity> list, string parentId, List<string> addList)
    {
        OrganizeEntity? entity = list.Find(x => x.Id == parentId);

        if (entity.ParentId != "-1") GetOrganizeParentName(list, entity.ParentId, addList);
        else addList.Add(entity.FullName);

        return addList;
    }

    /// <summary>
    /// 是否我的下属.
    /// </summary>
    /// <param name="userId">当前用户.</param>
    /// <param name="managerId">主管ID.</param>
    /// <param name="tier">层级.</param>
    /// <returns></returns>
    private async Task<bool> GetIsMyStaff(string userId, string managerId, int tier)
    {
        bool isMyStaff = false;
        if (tier <= 0) return true;
        string? superiorUserId = (await _repository.GetFirstAsync(it => it.Id.Equals(managerId) && it.DeleteMark == null))?.ManagerId;
        if (superiorUserId == null)
        {
            isMyStaff = false;
        }
        else if (userId == superiorUserId)
        {
            isMyStaff = true;
        }
        else
        {
            tier--;
            isMyStaff = await GetIsMyStaff(userId, superiorUserId, tier);
        }

        return isMyStaff;
    }

    /// <summary>
    /// 用户信息 字段对应 列名称.
    /// </summary>
    /// <returns></returns>
    private List<ExportImportHelperModel> GetUserInfoFieldToTitle(List<string> fields = null)
    {
        var typeEntity = _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "963255a34ea64a2584c5d1ba269c1fe6" || x.EnCode == "sex") && x.DeleteMark == null).First();
        var _genderList = _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).Select(x => x.FullName).ToList(); // 性别

        typeEntity = _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "b6cd65a763fa45eb9fe98e5057693e40" || x.EnCode == "Nation") && x.DeleteMark == null).First();
        var _nationList = _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).Select(x => x.FullName).ToList(); // 民族

        typeEntity = _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "7866376d5f694d4d851c7164bd00ebfc" || x.EnCode == "certificateType") && x.DeleteMark == null).First();
        var certificateTypeList = _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).Select(x => x.FullName).ToList(); // 证件类型

        typeEntity = _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "6a6d6fb541b742fbae7e8888528baa16" || x.EnCode == "Education") && x.DeleteMark == null).First();
        var educationList = _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).Select(x => x.FullName).ToList(); // 文化程度

        typeEntity = _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "485719133245509" || x.EnCode == "Rank") && x.DeleteMark == null).First();
        var ranksList = _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null && d.EnabledMark.Equals(1)).Select(x => x.FullName).ToList(); // 职级

        var res = new List<ExportImportHelperModel>();
        res.Add(new ExportImportHelperModel() { ColumnKey = "errorsInfo", ColumnValue = "异常原因", FreezePane = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "account", ColumnValue = "账户", Required = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "realName", ColumnValue = "姓名", Required = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "gender", ColumnValue = "性别", Required = true, SelectList = _genderList });
        res.Add(new ExportImportHelperModel() { ColumnKey = "email", ColumnValue = "电子邮箱" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "organizeId", ColumnValue = "所属组织", Required = true });
        res.Add(new ExportImportHelperModel() { ColumnKey = "managerId", ColumnValue = "直属主管" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "positionId", ColumnValue = "岗位" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "ranks", ColumnValue = "职级", SelectList = ranksList });
        res.Add(new ExportImportHelperModel() { ColumnKey = "roleId", ColumnValue = "角色" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "nation", ColumnValue = "民族", SelectList = _nationList });
        res.Add(new ExportImportHelperModel() { ColumnKey = "nativePlace", ColumnValue = "籍贯" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "entryDate", ColumnValue = "入职时间" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "certificatesType", ColumnValue = "证件类型", SelectList = certificateTypeList });
        res.Add(new ExportImportHelperModel() { ColumnKey = "certificatesNumber", ColumnValue = "证件号码" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "education", ColumnValue = "文化程度", SelectList = educationList });
        res.Add(new ExportImportHelperModel() { ColumnKey = "birthday", ColumnValue = "出生年月" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "telePhone", ColumnValue = "办公电话" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "landline", ColumnValue = "办公座机" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "mobilePhone", ColumnValue = "手机号码" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "urgentContacts", ColumnValue = "紧急联系" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "urgentTelePhone", ColumnValue = "紧急电话" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "postalAddress", ColumnValue = "通讯地址" });
        res.Add(new ExportImportHelperModel() { ColumnKey = "enabledMark", ColumnValue = "状态", Required = true, SelectList = new List<string>() { "启用", "禁用", "锁定" } });
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
    /// 导入用户数据函数.
    /// </summary>
    /// <param name="list">list.</param>
    /// <returns>[成功列表,失败列表].</returns>
    private async Task<object[]> ImportUserData(List<UserListImportDataInput> list)
    {
        List<UserListImportDataInput> userInputList = list;

        #region 初步排除错误数据

        if (userInputList == null || userInputList.Count() < 1)
            throw Oops.Oh(ErrorCode.D5019);
        if (userInputList.Count > 1000)
            throw Oops.Oh(ErrorCode.D5029);

        var regex = new Regex("^[a-z0-9A-Z\u4e00-\u9fa5]+$");

        // 上传重复的账号
        userInputList.ForEach(item =>
        {
            if (userInputList.Count(x => x.account == item.account) > 1)
            {
                var errorItems = userInputList.Where(x => x.account.IsNotEmptyOrNull() && x.account == item.account).ToList();
                for (var i = 1; i < errorItems.Count; i++) errorItems[i].errorsInfo = "账户值已存在,";
            }
        });

        // 用户账号 (匹配直属主管 和 验证重复账号)
        List<UserEntity>? _userRepositoryList = await _repository.AsQueryable().Where(it => it.DeleteMark == null).Select(it => new UserEntity() { Id = it.Id, Account = it.Account }).ToListAsync();

        // 已存在的账号
        List<UserEntity>? repeat = _userRepositoryList.Where(u => userInputList.Select(x => x.account).Contains(u.Account)).ToList();

        // 已存在的账号 列入 错误列表
        if (repeat.Any())
        {
            var addList = userInputList.Where(u => u.account.IsNotEmptyOrNull() && repeat.Select(x => x.Account).Contains(u.account)).ToList();
            addList.ForEach(item => item.errorsInfo = "账户值已存在,");
        }

        #endregion

        List<UserEntity>? userList = new List<UserEntity>();

        #region 预处理关联表数据

        // 组织机构
        var orgInfoList = _organizeService.GetOrgListTreeName();

        List<PositionEntity>? _positionRepositoryList = await _repository.AsSugarClient().Queryable<PositionEntity>().Where(x => x.DeleteMark == null).ToListAsync(); // 岗位
        List<RoleEntity>? _roleRepositoryList = await _repository.AsSugarClient().Queryable<RoleEntity>().Where(x => x.DeleteMark == null).ToListAsync(); // 角色
        List<OrganizeRelationEntity>? _roleOrgRelation = await _repository.AsSugarClient().Queryable<OrganizeRelationEntity>().Where(x => x.DeleteMark == null && x.ObjectType.Equals("Role")).ToListAsync(); // 角色

        DictionaryTypeEntity? typeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "963255a34ea64a2584c5d1ba269c1fe6" || x.EnCode == "sex") && x.DeleteMark == null).FirstAsync();
        List<DictionaryDataEntity>? _genderList = await _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null).ToListAsync(); // 性别

        typeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "b6cd65a763fa45eb9fe98e5057693e40" || x.EnCode == "Nation") && x.DeleteMark == null).FirstAsync();
        List<DictionaryDataEntity>? _nationList = await _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null).ToListAsync(); // 民族

        typeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "7866376d5f694d4d851c7164bd00ebfc" || x.EnCode == "certificateType") && x.DeleteMark == null).FirstAsync();
        List<DictionaryDataEntity>? certificateTypeList = await _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null).ToListAsync(); // 证件类型

        typeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "6a6d6fb541b742fbae7e8888528baa16" || x.EnCode == "Education") && x.DeleteMark == null).FirstAsync();
        List<DictionaryDataEntity>? educationList = await _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null).ToListAsync(); // 文化程度

        typeEntity = await _repository.AsSugarClient().Queryable<DictionaryTypeEntity>().Where(x => (x.Id == "485719133245509" || x.EnCode == "Rank") && x.DeleteMark == null).FirstAsync();
        List<DictionaryDataEntity>? ranksList = await _repository.AsSugarClient().Queryable<DictionaryDataEntity>().Where(d => d.DictionaryTypeId == typeEntity.Id && d.DeleteMark == null).ToListAsync(); // 职级

        #endregion

        // 处理多租户限定账户额度
        var maxAccountCount = 0;
        var addCount = 0;
        if (_tenant.MultiTenancy)
        {
            var tenatInfo = await _tenantManager.GetTenant(_userManager.TenantId);
            if (tenatInfo.accountNum != 0)
                maxAccountCount = (int)(tenatInfo.accountNum - await _repository.AsQueryable().CountAsync(x => x.DeleteMark == null));
        }

        var regexFullName = @"^([\u4e00-\u9fa5]|[a-zA-Z0-9])+$";

        // 用户关系数据
        List<UserRelationEntity>? userRelationList = new List<UserRelationEntity>();
        foreach (UserListImportDataInput? item in userInputList)
        {
            addCount++;

            if (item.account.IsNullOrWhiteSpace()) item.errorsInfo += "账户不能为空,";
            else if (item.account.Length > 50) item.errorsInfo += "账户值超出最多输入字符限制,";
            if (item.account.IsNotEmptyOrNull() && !regex.IsMatch(item.account)) item.errorsInfo += "账户值不能含有特殊符号,";

            if (item.realName.IsNullOrWhiteSpace()) item.errorsInfo += "姓名不能为空,";
            else if (item.realName.Length > 50) item.errorsInfo += "姓名值超出最多输入字符限制,";
            if (item.realName.IsNotEmptyOrNull() && !Regex.IsMatch(item.realName, regexFullName)) item.errorsInfo += "姓名值不能含有特殊符号,";

            if (item.gender.IsNullOrWhiteSpace()) item.errorsInfo += "性别不能为空,";
            if (item.organizeId.IsNullOrWhiteSpace()) item.errorsInfo += "所属组织不能为空,";
            if (item.enabledMark.IsNullOrWhiteSpace()) item.errorsInfo += "状态不能为空,";
            if (item.sortCode.IsNotEmptyOrNull())
            {
                try
                {
                    var sortCode = long.Parse(item.sortCode);
                    if (sortCode < 0) item.errorsInfo += "排序值不能小于0,";
                    else if (sortCode > 999999) item.errorsInfo += "排序值不能大于999999,";
                }
                catch (Exception ex)
                {
                    item.errorsInfo += "排序值不正确,";
                }
            }
            if (item.enabledMark.IsNotEmptyOrNull() && !item.enabledMark.Equals("禁用") && !item.enabledMark.Equals("启用") && !item.enabledMark.Equals("锁定")) item.errorsInfo += "找不到该状态值,";
            if (item.entryDate.IsNotEmptyOrNull())
            {
                try
                {
                    DateTime.Parse(item.entryDate);
                }
                catch (Exception ex)
                {
                    item.errorsInfo += "入职时间值不正确,";
                }
            }
            if (item.birthday.IsNotEmptyOrNull())
            {
                try
                {
                    DateTime.Parse(item.birthday);
                }
                catch (Exception ex)
                {
                    item.errorsInfo += "出生年月值不正确,";
                }
            }

            List<string>? orgIds = new List<string>(); // 多组织 , 号隔开
            List<string>? posIds = new List<string>(); // 多岗位 , 号隔开

            UserEntity? uentity = new UserEntity();
            uentity.Id = SnowflakeIdHelper.NextId();
            if (string.IsNullOrEmpty(uentity.HeadIcon)) uentity.HeadIcon = "001.png";
            uentity.Secretkey = Guid.NewGuid().ToString();

            var defaultPassWord = await _repository.AsSugarClient().Queryable<SysConfigEntity>()
                .Where(it => it.Key.Equals("newUserDefaultPassword"))
                .Select(it => it.Value)
                .FirstAsync();
            uentity.Password = MD5Encryption.Encrypt(MD5Encryption.Encrypt(defaultPassWord) + uentity.Secretkey); // 初始化密码

            // 寻找主管
            if(item.managerId.IsNotEmptyOrNull())
            {
                if (_userRepositoryList.Any(x => x.Account == item.managerId?.Split('/').LastOrDefault()))
                {
                    uentity.ManagerId = _userRepositoryList.Find(x => x.Account == item.managerId?.Split('/').LastOrDefault())?.Id;
                }
                else
                {
                    item.errorsInfo += "找不到该直属主管,";
                }
            }

            // 寻找组织
            if (item.organizeId.IsNotEmptyOrNull())
            {
                var errorOrgList = new List<string>();
                string[]? userOidList = item.organizeId?.Split(",");
                if (userOidList != null && userOidList.Any())
                {
                    foreach (string? oinfo in userOidList)
                    {
                        if (orgInfoList.Any(x => x.Description.Equals(oinfo))) orgIds.Add(orgInfoList.Find(x => x.Description.Equals(oinfo)).Id);
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
                    if (userOidList.Length == 1)
                        item.errorsInfo += "找不到该所属组织,";
                    else
                        item.errorsInfo += string.Format("找不到该所属组织({0}),", string.Join("、", errorOrgList));
                }
            }

            // 寻找角色
            if (item.roleId.IsNotEmptyOrNull() && item.roleId.Split(",").Any())
            {
                var roleList = item.roleId.Split(",").ToList();
                var itemRoleList = new List<string>();
                foreach (var it in roleList) itemRoleList.Add(it.Split("/").Last());
                var orgRoleIds = _roleOrgRelation.Where(x => orgIds.Contains(x.OrganizeId)).Select(x => x.ObjectId).ToList();
                var itemRoleIds = _roleRepositoryList.Where(r => itemRoleList.Contains(r.EnCode) && (r.GlobalMark.Equals(1) || orgRoleIds.Contains(r.Id))).Select(x => new { Id = x.Id, FullName = x.FullName }).ToList();
                uentity.RoleId = string.Join(",", itemRoleIds.Select(x => x.Id));
                if (itemRoleList.Count != itemRoleIds.Count)
                {
                    if (itemRoleList.Count.Equals(1))
                    {
                        item.errorsInfo += "找不到该角色,";
                    }
                    else
                    {
                        var exList = itemRoleList.Except(itemRoleIds.Select(x => x.FullName));
                        item.errorsInfo += string.Format("找不到该角色({0}),", string.Join("、", exList));
                    }
                }
            }

            // 性别
            if (item.gender.IsNotEmptyOrNull() && !_genderList.Any(x => x.FullName == item.gender))
            {
                item.errorsInfo += "找不到该性别值,";
            }

            // 寻找岗位
            if (item.positionId.IsNotEmptyOrNull() && item.positionId.Split(",").Any())
            {
                var itemPosList = item.positionId.Split(",").ToList();
                var exList = new List<string>();
                itemPosList.ForEach(it =>
                {
                    string[]? pinfo = it.Split("/");
                    string? pid = _positionRepositoryList.Find(x => orgIds.Contains(x.OrganizeId) && x.FullName.Trim() == pinfo.FirstOrDefault().Trim() && x.EnCode == pinfo.LastOrDefault())?.Id;
                    if (pid.IsNotEmptyOrNull()) posIds.Add(pid); // 多岗位
                    else exList.Add(it);
                });
                if (exList.Any())
                {
                    if (itemPosList.Count.Equals(1))
                    {
                        item.errorsInfo += "找不到该岗位,";
                    }
                    else
                    {
                        item.errorsInfo += string.Format("找不到该岗位({0}),", string.Join("、", exList));
                    }
                }
            }

            // 民族
            if (item.nation.IsNotEmptyOrNull() && !_nationList.Any(x => x.FullName == item.nation))
            {
                item.errorsInfo += "找不到该民族值,";
            }

            // 文化程度
            if (item.education.IsNotEmptyOrNull() && !educationList.Any(x => x.FullName == item.education))
            {
                item.errorsInfo += "找不到该文化程度值,";
            }

            // 证件类型
            if (item.certificatesType.IsNotEmptyOrNull() && !certificateTypeList.Any(x => x.FullName == item.certificatesType))
            {
                item.errorsInfo += "找不到该证件类型值,";
            }

            // 职级
            if (item.ranks.IsNotEmptyOrNull() && !ranksList.Any(x => x.FullName == item.ranks))
            {
                item.errorsInfo += "找不到该职级值,";
            }

            uentity.Gender = _genderList.Find(x => x.FullName == item.gender)?.EnCode;
            uentity.Nation = _nationList.Find(x => x.FullName == item.nation)?.Id; // 民族
            uentity.Education = educationList.Find(x => x.FullName == item.education)?.Id; // 文化程度
            uentity.CertificatesType = certificateTypeList.Find(x => x.FullName == item.certificatesType)?.Id; // 证件类型
            uentity.Ranks = ranksList.Find(x => x.FullName == item.ranks)?.Id; // 职级
            uentity.Account = item.account;
            uentity.Birthday = item.birthday.IsNotEmptyOrNull() ? item.birthday.ParseToDateTime() : null;
            uentity.CertificatesNumber = item.certificatesNumber;
            uentity.CreatorUserId = _userManager.UserId;
            uentity.CreatorTime = DateTime.Now;
            uentity.Description = item.description;
            uentity.Email = item.email;
            switch (item.enabledMark)
            {
                case "禁用":
                    uentity.EnabledMark = 0;
                    break;
                case "启用":
                    uentity.EnabledMark = 1;
                    break;
                case "锁定":
                    uentity.EnabledMark = 2;
                    break;
            }

            uentity.EntryDate = item.entryDate.IsNotEmptyOrNull() ? item.entryDate.ParseToDateTime() : null;
            uentity.Landline = item.landline;
            uentity.MobilePhone = item.mobilePhone;
            uentity.NativePlace = item.nativePlace;
            uentity.PostalAddress = item.postalAddress;
            uentity.RealName = item.realName;
            uentity.SortCode = item.sortCode.IsNotEmptyOrNull() ? item.sortCode.ParseToLong() : 0;
            uentity.TelePhone = item.telePhone;
            uentity.UrgentContacts = item.urgentContacts;
            uentity.UrgentTelePhone = item.urgentTelePhone;
            uentity.OrganizeId = orgIds.FirstOrDefault();

            if (uentity.OrganizeId.IsNotEmptyOrNull() && item.errorsInfo.IsNullOrEmpty())
            {
                List<UserRelationEntity>? roleList = _userRelationService.CreateUserRelation(uentity.Id, uentity.RoleId, "Role"); // 角色关系
                List<UserRelationEntity>? positionList = _userRelationService.CreateUserRelation(uentity.Id, string.Join(",", posIds), "Position"); // 岗位关系
                List<UserRelationEntity>? organizeList = _userRelationService.CreateUserRelation(uentity.Id, string.Join(",", orgIds), "Organize"); // 组织关系
                userRelationList.AddRange(positionList);
                userRelationList.AddRange(roleList);
                userRelationList.AddRange(organizeList);
            }

            if (_tenant.MultiTenancy && maxAccountCount != 0)
            {
                if (maxAccountCount >= addCount)
                {
                    if (item.errorsInfo.IsNotEmptyOrNull()) userList.Add(uentity);
                }
                else
                {
                    item.errorsInfo += "用户额度已达到上限";
                }
            }
            else
            {
                if (item.errorsInfo.IsNullOrEmpty()) userList.Add(uentity);
            }
        }

        if (userList.Any())
        {
            try
            {
                // 新增用户记录
                UserEntity? newEntity = await _repository.AsInsertable(userList).CallEntityMethod(m => m.Create()).ExecuteReturnEntityAsync();

                // 批量新增用户关系
                if (userRelationList.Count > 0) await _userRelationService.Create(userRelationList);

            }
            catch (Exception)
            {
                userInputList.ForEach(item => item.errorsInfo = "系统异常");
            }
        }

        var errorList = new List<UserListImportDataInput>();
        foreach (var item in userInputList)
        {
            if (item.errorsInfo.IsNotEmptyOrNull())
            {
                item.errorsInfo = item.errorsInfo.TrimEnd(',');
                errorList.Add(item);
            }
        }

        return new object[] { userList, errorList };
    }

    /// <summary>
    /// 获取机构成员列表.
    /// </summary>
    /// <param name="organizeId">机构ID.</param>
    /// <returns></returns>
    [NonAction]
    public async Task<List<OrganizeMemberListOutput>> GetOrganizeMemberList(string organizeId)
    {
        // 获取分级管理组织
        var dataScope = _repository.AsSugarClient().Queryable<OrganizeAdministratorEntity>()
            .Where(it => it.UserId == _userManager.UserId && it.DeleteMark == null)
            .Where(it => it.ThisLayerSelect.Equals(1) || it.SubLayerSelect.Equals(1)
            || it.ThisLayerAdd.Equals(1) || it.SubLayerAdd.Equals(1)
            || it.ThisLayerDelete.Equals(1) || it.SubLayerDelete.Equals(1)
            || it.ThisLayerEdit.Equals(1) || it.SubLayerEdit.Equals(1)).ToList();

        var thisLayer = dataScope.Where(it => it.ThisLayerSelect.Equals(1) || it.ThisLayerAdd.Equals(1) || it.ThisLayerDelete.Equals(1) || it.ThisLayerEdit.Equals(1)).ToList();
        var subLayer = dataScope.Where(it => it.SubLayerSelect.Equals(1) || it.SubLayerAdd.Equals(1) || it.SubLayerDelete.Equals(1) || it.SubLayerEdit.Equals(1)).ToList();

        List<OrganizeMemberListOutput>? output = new List<OrganizeMemberListOutput>();

        if (organizeId.Equals("0"))
        {
            List<OrganizeEntity>? data = await _repository.AsSugarClient().Queryable<OrganizeEntity>().Where(o => o.DeleteMark == null && o.EnabledMark == 1)
                .WhereIF(thisLayer.Any(), x => thisLayer.Select(x => x.OrganizeId).Contains(x.Id))
                .WhereIF(!thisLayer.Any(), x => thisLayer.Select(x => x.OrganizeId).Contains(x.Id)).OrderBy(o => o.SortCode).ToListAsync();

            if (subLayer.Any())
            {
                subLayer.ForEach(item =>
                {
                    var itemRes = _repository.AsSugarClient().Queryable<OrganizeEntity>().Where(o => o.DeleteMark == null && o.EnabledMark == 1)
                   .Where(x => x.OrganizeIdTree.Contains(item.OrganizeId) && !x.Id.Equals(item.OrganizeId)).OrderBy(o => o.SortCode).ToList();
                    data.AddRange(itemRes);
                });
            }

            data.ForEach(o =>
            {
                output.Add(new OrganizeMemberListOutput
                {
                    id = o.Id,
                    fullName = o.FullName,
                    enabledMark = o.EnabledMark,
                    type = o.Category,
                    icon = o.Category.Equals("company") ? "icon-ym icon-ym-tree-organization3" : "icon-ym icon-ym-tree-department1",
                    organizeIdTree = o.OrganizeIdTree,
                    hasChildren = true,
                    isLeaf = false
                });
            });
        }
        else
        {
            var userRelationList = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.ObjectType.Equals("Organize") && x.ObjectId.Equals(organizeId)).Select(x => x.UserId).ToListAsync();
            List<UserEntity>? userList = await _repository.AsSugarClient().Queryable<UserEntity>()
                .Where(u => userRelationList.Contains(u.Id) && u.EnabledMark > 0 && u.DeleteMark == null).OrderBy(o => o.SortCode).ToListAsync();
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
                    isLeaf = true
                });
            });

            List<OrganizeEntity>? departmentList = await _repository.AsSugarClient().Queryable<OrganizeEntity>().Where(o => o.DeleteMark == null && o.EnabledMark == 1)
                .Where(x => x.ParentId.Equals(organizeId))
                .WhereIF(thisLayer.Any(), x => thisLayer.Select(x => x.OrganizeId).Contains(x.Id))
                .WhereIF(!thisLayer.Any(), x => thisLayer.Select(x => x.OrganizeId).Contains(x.Id)).OrderBy(o => o.SortCode).ToListAsync();

            if (subLayer.Any())
            {
                subLayer.ForEach(item =>
                {
                    var itemRes = _repository.AsSugarClient().Queryable<OrganizeEntity>().Where(o => o.DeleteMark == null && o.EnabledMark == 1)
                   .Where(x => x.OrganizeIdTree.Contains(item.OrganizeId) && !x.Id.Equals(item.OrganizeId) && x.OrganizeIdTree.Contains(organizeId)).OrderBy(o => o.SortCode).ToList();
                    departmentList.AddRange(itemRes);
                });
            }

            departmentList.ForEach(o =>
            {
                output.Add(new OrganizeMemberListOutput()
                {
                    id = o.Id,
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

        if (!organizeId.Equals("0")) output.RemoveAll(x => x.id.Equals(organizeId));

        // 获取组织树
        var orgTree = _organizeService.GetOrgListTreeName();

        // 组织断层处理
        output.Where(x => x.parentId != "-1" && x.organizeIdTree.IsNotEmptyOrNull()).ToList().ForEach(item =>
        {
            item.fullName = orgTree.Find(x => x.Id.Equals(item.id)).Description;
            if (!output.Any(x => x.id.Equals(item.parentId)))
            {
                var pItem = output.Where(x => x.organizeIdTree.IsNotEmptyOrNull() && x.id != item.id && item.organizeIdTree.Contains(x.organizeIdTree)).FirstOrDefault();
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

        output.RemoveAll(x => x.type != "user" && x.parentId != "-1");

        if (!organizeId.Equals("0"))
        {
            var pOrgTreeName = orgTree.Find(x => x.Id.Equals(organizeId)).Description;
            output.ForEach(item => item.fullName = item.fullName.Replace(pOrgTreeName + "/", string.Empty));
        }

        return output;
    }

    /// <summary>
    /// 强制用户下线.
    /// </summary>
    /// <param name="oldUserEntity"></param>
    /// <param name="isDel"></param>
    private async Task ForcedOffline(UserEntity oldUserEntity, bool isDel)
    {
        var onlineCacheKey = string.Format("{0}:{1}", CommonConst.CACHEKEYONLINEUSER, _userManager.TenantId);
        var list = await _cacheManager.GetAsync<List<UserOnlineModel>>(onlineCacheKey);
        if (list != null && list.Any())
        {
            var user = list.Find(it => it.tenantId == _userManager.TenantId && it.userId == oldUserEntity.Id);
            if (user != null)
            {
                if(((oldUserEntity.Standing.Equals(3) && !user.isMobileDevice) || (oldUserEntity.AppStanding.Equals(3) && user.isMobileDevice)) || isDel)
                {
                    await _imHandler.SendMessageAsync(user.connectionId, new { method = "logout", msg = "用户信息已变更，请重新登录！" }.ToJsonString());

                    // 删除在线用户ID
                    list.RemoveAll((x) => x.connectionId == user.connectionId);
                    await _cacheManager.SetAsync(onlineCacheKey, list);

                    // 删除用户登录信息缓存
                    var cacheKey = string.Format("{0}:{1}:{2}", _userManager.TenantId, CommonConst.CACHEKEYUSER, user.userId);
                    await _cacheManager.DelAsync(cacheKey);
                }
            }
        }
    }
    #endregion

    #region 单点登录 数据同步

    /// <summary>
    /// 同步数据导maxkey.
    /// </summary>
    /// <param name="userEntity"></param>
    /// <param name="method"></param>
    /// <param name="tenantId"></param>
    public async Task syncUserInfo(UserEntity userEntity, string method, string tenantId)
    {
        try
        {
            if (_oauthOptions.Enabled)
            {
                var userName = string.Format("{0}:{1}", _oauthOptions.Pull.UserName, _oauthOptions.Pull.Password).ToBase64String();
                var map = parse(userEntity);
                tenantId = tenantId != null && tenantId.Length > 0 ? tenantId : "1";
                if (tenantId.Equals("default")) tenantId = "1";
                map.Add("instId", tenantId);
                var resString = string.Empty;
                var headers = new Dictionary<string, object>();
                headers.Add("Authorization", _oauthOptions.Pull.CredentialType + " " + userName);

                if (method.Equals("create"))
                {
                    resString = await (_oauthOptions.Pull.CreateRestAddress + "?appId=" + _oauthOptions.Pull.UserName).SetHeaders(headers).SetBody(map).PostAsStringAsync();
                }
                else if (method.Equals("update"))
                {
                    resString = await (_oauthOptions.Pull.ReplaceRestAddress + "?appId=" + _oauthOptions.Pull.UserName).SetHeaders(headers).SetBody(map).PutAsStringAsync();
                }
                else if (method.Equals("delete"))
                {
                    resString = await (_oauthOptions.Pull.DeleteRestAddress + "?appId=" + _oauthOptions.Pull.UserName).SetHeaders(headers).SetBody(map).DeleteAsStringAsync();
                }
                else if (method.Equals("modifyPassword"))
                {
                    resString = await (_oauthOptions.Pull.ChangePasswordRestAddress + "?appId=" + _oauthOptions.Pull.UserName).SetHeaders(headers).SetBody(map).PostAsStringAsync();
                }

                //            else if (method.Equals("modifyPassword")) {
                //                jsonObject = HttpUtil.httpRequest(_oauthOptions.Pull.getGetRestAddress() + username
                //                        , "GET"
                //                        , null, _oauthOptions.Pull.getCredentialType() + " " + _oauthOptions.Pull.getUserName() + "Og==" + _oauthOptions.Pull.getPassword()
                //                        , null);
                //            }
            }
        }
        catch (Exception)
        {

        }
    }

    private Dictionary<string, object> parse(UserEntity userEntity)
    {
        var map = new Dictionary<string, object>();

        // map.Add("id", userEntity.get("id"));
        map.Add("username", userEntity.Account);
        map.Add("password", userEntity.Password);
        map.Add("mobile", userEntity.MobilePhone);
        map.Add("email", userEntity.Email);
        map.Add("gender", userEntity.Gender);
        map.Add("createdBy", userEntity.CreatorUserId);
        map.Add("createdDate", userEntity.CreatorTime);
        map.Add("modifiedBy", userEntity.LastModifyUserId);
        map.Add("modifiedDate", userEntity.LastModifyTime);
        map.Add("displayName", userEntity.RealName);

        // map.Add("managerId", userEntity.get("managerId"));
        // map.Add("departmentId", userEntity.get("organizeId"));
        map.Add("loginCount", userEntity.LogSuccessCount);
        map.Add("badPasswordCount", userEntity.LogErrorCount);
        map.Add("lastLoginIp", userEntity.LastLogIP);
        map.Add("lastLoginTime", userEntity.LastLogTime);
        map.Add("status", userEntity.EnabledMark != null ? (userEntity.EnabledMark == 1 ? 1 : 4) : 4);
        return map;
    }

    #endregion
}