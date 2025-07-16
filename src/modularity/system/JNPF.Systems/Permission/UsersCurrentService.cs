﻿using Aop.Api.Domain;
using JNPF.Common.Configuration;
using JNPF.Common.Const;
using JNPF.Common.Core.Handlers;
using JNPF.Common.Core.Manager;
using JNPF.Common.Enums;
using JNPF.Common.Extension;
using JNPF.Common.Filter;
using JNPF.Common.Manager;
using JNPF.Common.Models.User;
using JNPF.Common.Security;
using JNPF.DataEncryption;
using JNPF.DependencyInjection;
using JNPF.DynamicApiController;
using JNPF.FriendlyException;
using JNPF.Systems.Entitys.Dto.Permission.UsersCurrent;
using JNPF.Systems.Entitys.Dto.UsersCurrent;
using JNPF.Systems.Entitys.Entity.Permission;
using JNPF.Systems.Entitys.Model.UsersCurrent;
using JNPF.Systems.Entitys.Permission;
using JNPF.Systems.Entitys.System;
using JNPF.Systems.Interfaces.Permission;
using JNPF.Systems.Interfaces.System;
using JNPF.WorkFlow.Entitys.Entity;
using Mapster;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NPOI.Util;
using SqlSugar;

namespace JNPF.Systems;

/// <summary>
/// 业务实现:个人资料.
/// </summary>
[ApiDescriptionSettings(Tag = "Permission", Name = "Current", Order = 168)]
[Route("api/permission/Users/[controller]")]
public class UsersCurrentService : IUsersCurrentService, IDynamicApiController, ITransient
{
    /// <summary>
    /// 基础仓储.
    /// </summary>
    private readonly ISqlSugarRepository<UserEntity> _repository;

    private readonly IDictionaryDataService _dictionaryDataService;

    /// <summary>
    /// 功能模块.
    /// </summary>
    private readonly ModuleService _moduleService;

    /// <summary>
    /// 功能按钮.
    /// </summary>
    private readonly IModuleButtonService _moduleButtonService;

    /// <summary>
    /// 功能列.
    /// </summary>
    private readonly IModuleColumnService _columnService;

    /// <summary>
    /// 功能数据权限计划.
    /// </summary>
    private readonly IModuleDataAuthorizeSchemeService _moduleDataAuthorizeSchemeService;

    /// <summary>
    /// 功能表单.
    /// </summary>
    private readonly IModuleFormService _formService;

    /// <summary>
    /// 门户管理.
    /// </summary>
    private readonly IPortalManageService _portalManageService;

    /// <summary>
    /// 组织管理.
    /// </summary>
    private readonly IOrganizeService _organizeService;

    /// <summary>
    /// 缓存管理器.
    /// </summary>
    private readonly ICacheManager _cacheManager;

    /// <summary>
    /// 系统配置.
    /// </summary>
    private readonly ISysConfigService _sysConfigService;

    /// <summary>
    /// 用户管理.
    /// </summary>
    private readonly IUserManager _userManager;

    /// <summary>
    /// 操作权限服务.
    /// </summary>
    private readonly OnlineUserService _onlineUserService;

    /// <summary>
    /// 多租户配置选项.
    /// </summary>
    private readonly TenantOptions _tenant;

    /// <summary>
    /// IM中心处理程序.
    /// </summary>
    private IMHandler _imHandler;

    /// <summary>
    /// 初始化一个<see cref="UsersCurrentService"/>类型的新实例.
    /// </summary>
    public UsersCurrentService(
        ISqlSugarRepository<UserEntity> userRepository,
        IModuleButtonService moduleButtonService,
        IModuleColumnService columnService,
        IModuleDataAuthorizeSchemeService moduleDataAuthorizeSchemeService,
        IModuleFormService formService,
        IPortalManageService portalManageService,
        ModuleService moduleService,
        IOrganizeService organizeService,
        ICacheManager cacheManager,
        ISysConfigService sysConfigService,
        OnlineUserService onlineUserService,
        IUserManager userManager,
        IDictionaryDataService dictionaryDataService,
        IOptions<TenantOptions> tenantOptions,
        IMHandler imHandler)
    {
        _repository = userRepository;
        _dictionaryDataService = dictionaryDataService;
        _moduleButtonService = moduleButtonService;
        _columnService = columnService;
        _moduleDataAuthorizeSchemeService = moduleDataAuthorizeSchemeService;
        _formService = formService;
        _portalManageService = portalManageService;
        _moduleService = moduleService;
        _organizeService = organizeService;
        _cacheManager = cacheManager;
        _sysConfigService = sysConfigService;
        _onlineUserService = onlineUserService;
        _userManager = userManager;
        _tenant = tenantOptions.Value;
        _imHandler = imHandler;
    }

    #region GET

    /// <summary>
    /// 获取我的下属.
    /// </summary>
    /// <param name="id">用户Id.</param>
    /// <returns></returns>
    [HttpGet("Subordinate/{id}")]
    public async Task<dynamic> GetSubordinate(string id)
    {
        // 获取用户Id 下属 ,顶级节点为 自己
        List<string>? userIds = new List<string>();
        if (id == "0") userIds.Add(_userManager.UserId);
        else userIds = await _repository.AsQueryable().Where(m => m.ManagerId == id && m.DeleteMark == null).Select(m => m.Id).ToListAsync();

        if (userIds.Any())
        {
            return await _repository.AsSugarClient().Queryable<UserEntity, OrganizeEntity, PositionEntity>((a, b, c) => new JoinQueryInfos(JoinType.Left, b.Id == a.OrganizeId, JoinType.Left, c.Id == a.PositionId))
                .WhereIF(userIds.Any(), a => userIds.Contains(a.Id))
                .Where(a => a.DeleteMark == null && a.EnabledMark == 1)
                .OrderBy(a => a.SortCode)
                .Select((a, b, c) => new UsersCurrentSubordinateOutput
                {
                    id = a.Id,
                    avatar = SqlFunc.MergeString("/api/File/Image/userAvatar/", a.HeadIcon),
                    userName = SqlFunc.MergeString(a.RealName, "/", a.Account),
                    isLeaf = false,
                    department = b.FullName,
                    position = c.FullName
                })
                .ToListAsync();
        }
        else
        {
            return new List<UsersCurrentSubordinateOutput>();
        }
    }

    /// <summary>
    /// 获取个人资料.
    /// </summary>
    /// <returns></returns>
    [HttpGet("BaseInfo")]
    public async Task<dynamic> GetBaseInfo()
    {
        UsersCurrentInfoOutput? data = await _repository.AsSugarClient().Queryable<UserEntity>().Where(x => x.Id.Equals(_userManager.UserId))
            .Select(a => new UsersCurrentInfoOutput
            {
                id = a.Id,
                account = SqlFunc.IIF(KeyVariable.MultiTenancy == true, SqlFunc.MergeString(_userManager.TenantId, "@", a.Account), a.Account),
                realName = a.RealName,
                position = string.Empty,
                positionId = a.PositionId,
                organizeId = a.OrganizeId,
                manager = SqlFunc.Subqueryable<UserEntity>().EnableTableFilter().Where(x => x.Id.Equals(a.ManagerId) && x.EnabledMark != 0 && x.DeleteMark == null).Select(x => SqlFunc.MergeString(x.RealName, "/", x.Account)),
                roleId = string.Empty,
                roleIds = a.RoleId,
                creatorTime = a.CreatorTime,
                prevLogTime = a.PrevLogTime,
                signature = a.Signature,
                gender = a.Gender.ToString(),
                nation = a.Nation,
                nativePlace = a.NativePlace,
                entryDate = a.EntryDate,
                certificatesType = a.CertificatesType,
                certificatesNumber = a.CertificatesNumber,
                education = a.Education,
                birthday = a.Birthday,
                telePhone = a.TelePhone,
                landline = a.Landline,
                mobilePhone = a.MobilePhone,
                email = a.Email,
                urgentContacts = a.UrgentContacts,
                urgentTelePhone = a.UrgentTelePhone,
                postalAddress = a.PostalAddress,
                theme = a.Theme,
                language = a.Language,
                ranks = SqlFunc.Subqueryable<DictionaryDataEntity>().EnableTableFilter().Where(x => x.Id.Equals(a.Ranks) && x.EnabledMark != 0 && x.DeleteMark == null).Select(x => x.FullName),
                ranksId = a.Ranks,
                avatar = SqlFunc.IIF(SqlFunc.IsNullOrEmpty(SqlFunc.ToString(a.HeadIcon)), string.Empty, SqlFunc.MergeString("/api/File/Image/userAvatar/", SqlFunc.ToString(a.HeadIcon)))
            }).FirstAsync();

        // 获取组织树
        var orgTree = _organizeService.GetOrgListTreeName();

        // 组织结构
        data.organize = orgTree.FirstOrDefault(x => x.Id.Equals(data.organizeId))?.Description;

        // 获取当前用户、当前组织下的所有岗位
        List<string>? pNameList = await _repository.AsSugarClient().Queryable<PositionEntity, UserRelationEntity>((a, b) => new JoinQueryInfos(JoinType.Left, a.Id == b.ObjectId))
            .Where((a, b) => b.ObjectType == "Position" && b.UserId == _userManager.UserId && a.OrganizeId == data.organizeId && a.EnabledMark != 0 && a.DeleteMark == null).Select(a => a.FullName).ToListAsync();
        data.position = string.Join(",", pNameList);

        // 获取当前用户、全局角色 和当前组织下的所有角色
        List<string>? roleList = _userManager.GetUserRoleIds(data.roleIds, data.organizeId);
        data.roleId = await _userManager.GetRoleNameByIds(string.Join(",", roleList));

        if (_tenant.MultiTenancy)
        {
            data.isTenant = true;
            var tenantInfo = _userManager.GetGlobalTenantCache().Where(x => x.TenantId.Equals(_userManager.TenantId)).FirstOrDefault();
            data.currentTenantInfo = tenantInfo.Adapt<UsersCurrentTenantInfoOutput>();
        }

        return data;
    }

    /// <summary>
    /// 获取系统权限 .
    /// </summary>
    /// <returns></returns>
    [HttpGet("Authorize")]
    public async Task<dynamic> GetAuthorize()
    {
        var dataScope = _userManager.DataScope.Select(x => x.organizeId).Distinct().ToList();
        string? userId = _userManager.UserId;
        bool isAdmin = _userManager.IsAdministrator;
        UsersCurrentAuthorizeOutput? output = new UsersCurrentAuthorizeOutput()
        {
            module = new List<UsersCurrentAuthorizeMoldel>(),
            button = new List<UsersCurrentAuthorizeMoldel>(),
            column = new List<UsersCurrentAuthorizeMoldel>(),
            form = new List<UsersCurrentAuthorizeMoldel>(),
            resource = new List<UsersCurrentAuthorizeMoldel>(),
            portal = new List<UsersCurrentAuthorizeMoldel>(),
            flow = new List<UsersCurrentAuthorizeMoldel>(),
            print = new List<UsersCurrentAuthorizeMoldel>(),
        };

        // 根据多租户返回结果moduleIdList :[菜单id] 过滤应用菜单
        var ignoreIds = _userManager.TenantIgnoreModuleIdList;
        var ignoreUrls = _userManager.TenantIgnoreUrlAddressList;

        string cacheKey = string.Format("{0}:{1}", CommonConst.CACHEKEYONLINEUSER, _userManager.TenantId);
        var onlineList = await _cacheManager.GetAsync<List<UserOnlineModel>>(cacheKey);
        var systemId = onlineList.Find(it => it.token.Equals(_userManager.ToKen) && it.userId.Equals(_userManager.UserId)).systemId;

        var mList = await _moduleService.GetUserModuleListByIds("Web", systemId, ignoreIds);
        if (ignoreUrls != null) mList = mList.Where(x => !ignoreUrls.Contains(x.urlAddress)).ToList();
        var appMList = await _moduleService.GetUserModuleListByIds("App", systemId, ignoreIds);
        if (ignoreUrls != null) appMList = appMList.Where(x => !ignoreUrls.Contains(x.urlAddress)).ToList();
        mList.AddRange(appMList);
        List<ModuleEntity>? moduleList = mList.Where(x => !_userManager.CommonModuleEnCodeList.Contains(x.enCode)).Adapt<List<ModuleEntity>>();

        if (moduleList.Any(it => it.Category.Equals("Web")))
        {
            moduleList.Where(it => it.Category.Equals("Web") && it.ParentId.Equals("-1")).ToList().ForEach(it =>
            {
                it.ParentId = "1";
            });
            moduleList.Add(new ModuleEntity()
            {
                Id = "1",
                FullName = "WEB菜单",
                Icon = "icon-ym icon-ym-pc",
                ParentId = "-1",
                Category = "Web",
                Type = 1,
                SortCode = 99998
            });
        }
        if (moduleList.Any(it => it.Category.Equals("App")))
        {
            moduleList.Where(it => it.Category.Equals("App") && it.ParentId.Equals("-1")).ToList().ForEach(it =>
            {
                it.ParentId = "2";
            });
            moduleList.Add(new ModuleEntity()
            {
                Id = "2",
                FullName = "APP菜单",
                Icon = "icon-ym icon-ym-mobile",
                ParentId = "-1",
                Category = "App",
                Type = 1,
                SortCode = 99999
            });
        }

        var dataScopeModuleIds = await _repository.AsSugarClient().Queryable<ModuleEntity>().WhereIF(_userManager.Standing.Equals(2), x => dataScope.Contains(x.SystemId)).WhereIF(!_userManager.Standing.Equals(2), x => x.Id == null).Select(x => x.Id).ToListAsync();
        var buttonList = await _moduleButtonService.GetUserModuleButtonList(dataScopeModuleIds);
        var columnList = await _columnService.GetUserModuleColumnList(dataScopeModuleIds);
        var resourceList = await _moduleDataAuthorizeSchemeService.GetResourceList(dataScopeModuleIds);
        var formList = await _formService.GetUserModuleFormList(dataScopeModuleIds);
        var portalManageList = await _portalManageService.GetSysPortalManageList(_userManager.User.SystemId);
        if (moduleList.Count != 0)
            output.module = moduleList.Adapt<List<UsersCurrentAuthorizeMoldel>>().ToTree("-1");
        if (buttonList.Count != 0)
        {
            List<UsersCurrentAuthorizeMoldel>? buttonData = buttonList.Select(r => new UsersCurrentAuthorizeMoldel
            {
                id = r.id,
                parentId = r.moduleId,
                fullName = r.fullName
            }).ToList();
            List<UsersCurrentAuthorizeMoldel>? menuAuthorizeData = new List<UsersCurrentAuthorizeMoldel>();
            List<string>? pids = buttonList.Select(bt => bt.moduleId).ToList();
            GetParentsModuleList(pids, moduleList, ref menuAuthorizeData);
            output.button = menuAuthorizeData.Union(buttonData.Adapt<List<UsersCurrentAuthorizeMoldel>>()).ToList().ToTree("-1");
        }

        if (columnList.Count != 0)
        {
            List<UsersCurrentAuthorizeMoldel>? columnData = columnList.Select(r => new UsersCurrentAuthorizeMoldel
            {
                id = r.id,
                parentId = r.moduleId,
                fullName = r.fullName
            }).ToList();
            List<UsersCurrentAuthorizeMoldel>? menuAuthorizeData = new List<UsersCurrentAuthorizeMoldel>();
            List<string>? pids = columnList.Select(bt => bt.moduleId).ToList();
            GetParentsModuleList(pids, moduleList, ref menuAuthorizeData);
            output.column = menuAuthorizeData.Union(columnData.Adapt<List<UsersCurrentAuthorizeMoldel>>()).ToList().ToTree("-1");
        }

        if (resourceList.Count != 0)
        {
            List<UsersCurrentAuthorizeMoldel>? resourceData = resourceList.Select(r => new UsersCurrentAuthorizeMoldel
            {
                id = r.id,
                parentId = r.moduleId,
                fullName = r.fullName
            }).ToList();
            List<UsersCurrentAuthorizeMoldel>? menuAuthorizeData = new List<UsersCurrentAuthorizeMoldel>();
            List<string>? pids = resourceList.Select(bt => bt.moduleId).ToList();
            GetParentsModuleList(pids, moduleList, ref menuAuthorizeData);
            output.resource = menuAuthorizeData.Union(resourceData.Adapt<List<UsersCurrentAuthorizeMoldel>>()).ToList().ToTree("-1");
        }

        if (formList.Count != 0)
        {
            List<UsersCurrentAuthorizeMoldel>? formData = formList.Select(r => new UsersCurrentAuthorizeMoldel
            {
                id = r.id,
                parentId = r.moduleId,
                fullName = r.fullName
            }).ToList();
            List<UsersCurrentAuthorizeMoldel>? menuAuthorizeData = new List<UsersCurrentAuthorizeMoldel>();
            List<string>? pids = formList.Select(bt => bt.moduleId).ToList();
            GetParentsModuleList(pids, moduleList, ref menuAuthorizeData);
            output.form = menuAuthorizeData.Union(formData.Adapt<List<UsersCurrentAuthorizeMoldel>>()).ToList().ToTree("-1");
        }

        if (portalManageList.Count != 0)
        {
            var portalManageData = new List<UsersCurrentAuthorizeMoldel>();
            if (portalManageList.Any(it => it.platform.Equals("Web")))
            {
                portalManageData.Add(new UsersCurrentAuthorizeMoldel
                {
                    id = "1",
                    fullName = "WEB菜单",
                    icon = "icon-ym icon-ym-pc",
                    parentId = "-1"
                });
                portalManageData.AddRange(portalManageList.Where(it => it.platform.Equals("Web")).Select(it => new UsersCurrentAuthorizeMoldel
                {
                    id = it.id,
                    fullName = it.fullName,
                    parentId = "1"
                }).ToList());
            }
            if (portalManageList.Any(it => it.platform.Equals("App")))
            {
                portalManageData.Add(new UsersCurrentAuthorizeMoldel
                {
                    id = "2",
                    fullName = "APP菜单",
                    icon = "icon-ym icon-ym-mobile",
                    parentId = "-1"
                });
                portalManageData.AddRange(portalManageList.Where(it => it.platform.Equals("App")).Select(it => new UsersCurrentAuthorizeMoldel
                {
                    id = it.id,
                    fullName = it.fullName,
                    parentId = "2"
                }).ToList());
            }

            output.portal = portalManageData.ToTree("-1");
        }

        #region 流程、打印权限
        var roles = _userManager.PermissionGroup;
        var isGetAll = (_userManager.Standing.Equals(1) && _userManager.IsAdministrator) || (_userManager.Standing.Equals(2) && _userManager.IsOrganizeAdmin);
        if (roles.Any() || isGetAll)
        {
            var items = new List<string>();
            var printItems = new List<string>();
            if (!isGetAll)
            {
                var authorize = await _repository.AsSugarClient().Queryable<AuthorizeEntity>().In(a => a.ObjectId, roles).ToListAsync();
                items = authorize.FindAll(x => x.ItemType == "flow").Select(x => x.ItemId).ToList();
                printItems = authorize.FindAll(x => x.ItemType == "print").Select(x => x.ItemId).ToList();
            }

            //var VFID = await _repository.AsSugarClient().Queryable<WorkFlowVersionEntity>().WhereIF(!isGetAll, x => items.Contains(x.Id)).Where(x => SqlFunc.ToInt32(SqlFunc.JsonField(x.FlowConfig, "visibleType")) == 2 && x.Status.Equals(1)).Select(x => x.Id).ToListAsync();
            var flows = await _repository.AsSugarClient().Queryable<WorkFlowTemplateEntity>().Where(a => a.VisibleType == 2 && a.Status == 1).Where(a => a.EnabledMark == 1 && a.DeleteMark == null)
                .WhereIF(!isGetAll, a => items.Contains(a.Id))
                .Select(a => new UsersCurrentAuthorizeMoldel
                {
                    id = a.FlowId,
                    parentId = a.Category,
                    fullName = a.FullName,
                    icon = a.Icon,
                    sortCode = a.SortCode,
                    isLeaf = true,
                }).OrderBy(q => q.sortCode).ToListAsync();

            if (flows.Any())
            {
                var dicDataInfo = await _dictionaryDataService.GetInfo(flows.FirstOrDefault().parentId);
                var dicDataList = await _dictionaryDataService.GetList(dicDataInfo.DictionaryTypeId);
                foreach (var item in dicDataList)
                {
                    flows.Add(new UsersCurrentAuthorizeMoldel()
                    {
                        fullName = item.FullName,
                        parentId = "0",
                        id = item.Id,
                    });
                }
            }

            output.flow.AddRange(flows.ToTree().Where(x => x.children != null && x.children.Any()).ToList());

            var prints = await _repository.AsSugarClient().Queryable<PrintDevEntity>()
                .Where(x => x.DeleteMark == null && x.CommonUse == 1 && x.VisibleType == 2)
                .WhereIF(!isGetAll, x => printItems.Contains(x.Id))
                .OrderBy(x => x.SortCode)
                .OrderBy(x => x.CreatorTime, OrderByType.Desc)
                .Select(x => new UsersCurrentAuthorizeMoldel
                {
                    id = x.Id,
                    parentId = x.Category,
                    fullName = x.FullName,
                    icon = x.Icon,
                    sortCode = x.SortCode,
                    isLeaf = true,
                }).ToListAsync();

            if (prints.Any())
            {
                var dicDataInfo = await _dictionaryDataService.GetInfo(prints.FirstOrDefault().parentId);
                var dicDataList = await _dictionaryDataService.GetList(dicDataInfo.DictionaryTypeId);
                foreach (var item in dicDataList)
                {
                    prints.Add(new UsersCurrentAuthorizeMoldel()
                    {
                        fullName = item.FullName,
                        parentId = "0",
                        id = item.Id,
                    });
                }
            }

            output.print.AddRange(prints.ToTree().Where(x => x.children != null && x.children.Any()).ToList());
        }
        #endregion

        return output;
    }

    /// <summary>
    /// 获取系统日志.
    /// </summary>
    /// <param name="input">参数.</param>
    /// <returns></returns>
    [HttpGet("SystemLog")]
    public async Task<dynamic> GetSystemLog([FromQuery] UsersCurrentSystemLogQuery input)
    {
        string? userId = _userManager.UserId;
        PageInputBase? requestParam = input.Adapt<PageInputBase>();
        SqlSugarPagedList<UsersCurrentSystemLogOutput>? data = await _repository.AsSugarClient().Queryable<SysLogEntity>()
            .WhereIF(input.startTime.IsNotEmptyOrNull() && input.endTime.IsNotEmptyOrNull(), s => SqlFunc.Between(s.CreatorTime, input.startTime, input.endTime))
            .WhereIF(input.loginType.IsNotEmptyOrNull(), s => s.LoginType.Equals(input.loginType))
            .WhereIF(input.loginMark.IsNotEmptyOrNull(), s => s.LoginMark.Equals(input.loginMark))
            .WhereIF(!input.keyword.IsNullOrEmpty(), s => s.UserName.Contains(input.keyword) || s.IPAddress.Contains(input.keyword))
            .Where(s => s.Type == input.category && s.UserId == userId).OrderBy(o => o.CreatorTime, OrderByType.Desc)
            .Select(a => new UsersCurrentSystemLogOutput
            {
                id = a.Id,
                creatorTime = a.CreatorTime,
                userName = a.UserName,
                ipAddress = a.IPAddress,
                moduleName = a.ModuleName,
                category = a.Type,
                userId = a.UserId,
                platForm = a.PlatForm,
                requestURL = a.RequestURL,
                requestMethod = a.RequestMethod,
                requestDuration = a.RequestDuration,
                ipAddressName = a.IPAddressName,
                browser = a.Browser,
                loginMark = a.LoginMark,
                loginType = a.LoginType,
                abstracts = a.Description
            }).ToPagedListAsync(requestParam.currentPage, requestParam.pageSize);
        return PageResult<UsersCurrentSystemLogOutput>.SqlSugarPageResult(data);
    }

    #endregion

    #region Post

    /// <summary>
    /// 修改密码.
    /// </summary>
    /// <returns></returns>
    [HttpPost("Actions/ModifyPassword")]
    [NonUnify]
    public async Task<dynamic> ModifyPassword([FromBody] UsersCurrentActionsModifyPasswordInput input)
    {
        UserEntity? user = _userManager.User;
        //if(user.Id.ToLower().Equals("admin")) // admin账号不可修改密码
        //    throw Oops.Oh(ErrorCode.D5024);
        if (MD5Encryption.Encrypt(input.oldPassword + user.Secretkey) != user.Password.ToLower())
            return new { code = 400, msg = "旧密码错误，请重新输入" };
        string? imageCode = await GetCode(input.timestamp);
        if (!input.code.ToLower().Equals(imageCode.ToLower()))
        {
            return new { code = 400, msg = "验证码输入错误，请重新输入！" };
        }
        else
        {
            if (await PwdStrategy(input)) return new { code = 400, msg = "修改失败，新建密码不能与旧密码一样" };
            await DelCode(input.timestamp);
            await DelUserInfo(_userManager.TenantId, user.Id);

            user.Password = MD5Encryption.Encrypt(input.password + user.Secretkey);
            user.ChangePasswordDate = DateTime.Now;
            user.LastModifyTime = DateTime.Now;
            user.LastModifyUserId = _userManager.UserId;
            int isOk = await _repository.AsUpdateable(user).UpdateColumns(it => new {
                it.Password,
                it.ChangePasswordDate,
                it.LastModifyUserId,
                it.LastModifyTime
            }).IgnoreColumns(ignoreAllNullColumns: true).ExecuteCommandAsync();
            if (!(isOk > 0)) return new { code = 400, msg = "修改密码失败" };

            // 修改密码会立即退出登录
            var onlineCacheKey = string.Format("{0}:{1}", CommonConst.CACHEKEYONLINEUSER, _userManager.TenantId);
            var list = await _cacheManager.GetAsync<List<UserOnlineModel>>(onlineCacheKey);
            var onlineUser = list.Find(it => it.tenantId == _userManager.TenantId && it.userId == _userManager.UserId);
            if (onlineUser != null)
            {
                //await _imHandler.SendMessageAsync(onlineUser.connectionId, new { method = "logout", msg = "密码已变更，请重新登录！" }.ToJsonString());

                // 删除在线用户ID
                list.RemoveAll((x) => x.connectionId == onlineUser.connectionId);
                await _cacheManager.SetAsync(onlineCacheKey, list);

                // 删除用户登录信息缓存
                var cacheKey = string.Format("{0}:{1}:{2}", _userManager.TenantId, CommonConst.CACHEKEYUSER, onlineUser.userId);
                await _cacheManager.DelAsync(cacheKey);
            }
        }

        return new { code = 200, msg = "修改成功，请牢记新密码。" };
    }

    /// <summary>
    /// 修改个人资料.
    /// </summary>
    /// <returns></returns>
    [HttpPut("BaseInfo")]
    public async Task UpdateBaseInfo([FromBody] UsersCurrentInfoUpInput input)
    {
        UserEntity? userInfo = input.Adapt<UserEntity>();
        userInfo.Id = _userManager.UserId;
        userInfo.IsAdministrator = Convert.ToInt32(_userManager.IsAdministrator);
        userInfo.LastModifyTime = DateTime.Now;
        userInfo.LastModifyUserId = _userManager.UserId;
        int isOk = await _repository.AsUpdateable(userInfo).UpdateColumns(it => new {
            it.RealName,
            it.Signature,
            it.Gender,
            it.Nation,
            it.NativePlace,
            it.CertificatesType,
            it.CertificatesNumber,
            it.Education,
            it.Birthday,
            it.TelePhone,
            it.Landline,
            it.MobilePhone,
            it.Email,
            it.UrgentContacts,
            it.UrgentTelePhone,
            it.PostalAddress,
            it.LastModifyUserId,
            it.LastModifyTime
        }).IgnoreColumns(ignoreAllNullColumns: true).ExecuteCommandAsync();
        if (!(isOk > 0)) throw Oops.Oh(ErrorCode.D5009);
    }

    /// <summary>
    /// 修改主题.
    /// </summary>
    /// <returns></returns>
    [HttpPut("SystemTheme")]
    public async Task UpdateBaseInfo([FromBody] UsersCurrentSysTheme input)
    {
        UserEntity? user = _userManager.User;
        user.Theme = input.theme;
        user.LastModifyTime = DateTime.Now;
        user.LastModifyUserId = _userManager.UserId;
        int isOk = await _repository.AsUpdateable(user).UpdateColumns(it => new {
            it.Theme,
            it.LastModifyUserId,
            it.LastModifyTime
        }).IgnoreColumns(ignoreAllNullColumns: true).ExecuteCommandAsync();
        if (!(isOk > 0)) throw Oops.Oh(ErrorCode.D5010);
    }

    /// <summary>
    /// 修改语言.
    /// </summary>
    /// <returns></returns>
    [HttpPut("SystemLanguage")]
    public async Task UpdateLanguage([FromBody] UsersCurrentSysLanguage input)
    {
        UserEntity? user = _userManager.User;
        user.Language = input.language;
        user.LastModifyTime = DateTime.Now;
        user.LastModifyUserId = _userManager.UserId;
        int isOk = await _repository.AsUpdateable(user).UpdateColumns(it => new {
            it.Language,
            it.LastModifyUserId,
            it.LastModifyTime
        }).IgnoreColumns(ignoreAllNullColumns: true).ExecuteCommandAsync();
        if (!(isOk > 0)) throw Oops.Oh(ErrorCode.D5011);
    }

    /// <summary>
    /// 修改头像.
    /// </summary>
    /// <returns></returns>
    [HttpPut("Avatar/{name}")]
    public async Task UpdateAvatar(string name)
    {
        UserEntity? user = _userManager.User;
        user.HeadIcon = name;
        user.LastModifyTime = DateTime.Now;
        user.LastModifyUserId = _userManager.UserId;
        int isOk = await _repository.AsUpdateable(user).UpdateColumns(it => new {
            it.HeadIcon,
            it.LastModifyUserId,
            it.LastModifyTime
        }).IgnoreColumns(ignoreAllNullColumns: true).ExecuteCommandAsync();
        if (!(isOk > 0)) throw Oops.Oh(ErrorCode.D5012);
    }

    /// <summary>
    /// 切换 默认 ： 组织、岗位、系统.
    /// </summary>
    /// <returns></returns>
    [HttpPut("major")]
    public async Task DefaultOrganize([FromBody] UsersCurrentDefaultOrganizeInput input)
    {
        var noContainsMIdList = _userManager.TenantIgnoreModuleIdList;
        var noContainsMUrlList = _userManager.TenantIgnoreUrlAddressList;
        UserEntity? userInfo = _userManager.User;

        var type = "Web";
        if (input.menuType.Equals(1)) type = "App";

        switch (input.majorType)
        {
            case "Organize": // 组织
                {
                    var onlineCacheKey = string.Format("{0}:{1}", CommonConst.CACHEKEYONLINEUSER, _userManager.TenantId);
                    var list = await _cacheManager.GetAsync<List<UserOnlineModel>>(onlineCacheKey);
                    var onlineUser = list.Find(it => it.tenantId == _userManager.TenantId && it.userId == _userManager.UserId && it.token.Equals(_userManager.ToKen));

                    var pIds = _userManager.GetPermissionByCurrentOrgId(_userManager.UserId, input.majorId);

                    if (_userManager.Standing.Equals(3))
                    {
                        // 验证权限
                        if (!_userManager.DataScope.Any(x => x.organizeType != null && x.organizeType.Equals("System") && x.organizeId.Equals(input.majorId)) && !pIds.Any())
                        {
                            if (onlineUser.isSeparate) throw Oops.Oh(ErrorCode.D4020);
                            else throw Oops.Oh(ErrorCode.D4015);
                        }

                        if (!await _repository.AsSugarClient().Queryable<AuthorizeEntity>().AnyAsync(a => pIds.Contains(a.ObjectId) && a.ItemType == "system"))
                        {
                            if (onlineUser.isSeparate) throw Oops.Oh(ErrorCode.D4020);
                            else throw Oops.Oh(ErrorCode.D4015);
                        }
                    }

                    userInfo.OrganizeId = input.majorId;

                    // 获取切换组织 Id 下的所有岗位
                    List<string>? pList = await _repository.AsSugarClient().Queryable<PositionEntity>().Where(x => x.OrganizeId == input.majorId).Select(x => x.Id).ToListAsync();

                    // 获取切换组织的 岗位，如果该组织没有岗位则为空
                    List<string>? idList = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
                        .Where(x => x.UserId == userInfo.Id && pList.Contains(x.ObjectId) && x.ObjectType == "Position").Select(x => x.ObjectId).ToListAsync();
                    userInfo.PositionId = idList.FirstOrDefault() == null ? string.Empty : idList.FirstOrDefault();

                    // 切换掉 当前组织有权限的应用
                    if (pIds != null && pIds.Any() && _userManager.Standing.Equals(3))
                    {
                        var firstSystem = await _repository.AsSugarClient().Queryable<AuthorizeEntity>()
                            .Where(a => pIds.Contains(a.ObjectId) && a.ItemType == "system")
                            .WhereIF(onlineUser != null && onlineUser.isSeparate && onlineUser.systemId.IsNotEmptyOrNull(), a => a.ItemId == onlineUser.systemId)
                            .FirstAsync();
                        if (firstSystem == null) throw Oops.Oh(ErrorCode.D4020);
                        if (input.menuType.Equals(1)) userInfo.AppSystemId = firstSystem.ItemId;
                        else userInfo.SystemId = firstSystem.ItemId;
                    }

                    if (onlineUser != null && onlineUser.systemId.IsNotEmptyOrNull() && onlineUser.isSeparate) userInfo.SystemId = onlineUser.systemId;
                }

                break;
            case "Position": // 岗位
                userInfo.PositionId = input.majorId;
                break;

            case "System": // 系统
                // 当前系统已被管理员禁用.
                var switchSystem = await _repository.AsSugarClient().Queryable<SystemEntity>().FirstAsync(it => input.majorId.Equals(it.Id));
                if (switchSystem != null && !switchSystem.EnabledMark.Equals(1)) throw Oops.Oh(ErrorCode.D4014);
                if (switchSystem != null && switchSystem.DeleteMark != null) throw Oops.Oh(ErrorCode.D4016);
                var menuList = await _moduleService.GetUserTreeModuleList(type, input.majorId);
                if (!menuList.Any()) throw Oops.Oh(ErrorCode.D4018);
                if (input.menuType.Equals(1)) userInfo.AppSystemId = input.majorId;
                else userInfo.SystemId = input.majorId;
                break;
            case "Standing": // 用户身份
                switch (input.majorId)
                {
                    case "1":
                        if (!userInfo.IsAdministrator.Equals(1)) throw Oops.Oh(ErrorCode.D4024);
                        break;
                    case "2":
                        if (!_userManager.IsOrganizeAdmin)
                        {
                            throw Oops.Oh(ErrorCode.D4024);
                        }
                        else
                        {
                            // 根据多租户返回结果moduleIdList :[菜单id] 过滤菜单
                            var sysId = _userManager.UserOrigin.Equals("pc") ? userInfo.SystemId : userInfo.AppSystemId;
                            var isAnyModule = false;

                            var mList = await GetModuleBySystemId(sysId, type, noContainsMIdList, noContainsMUrlList);
                            if (!mList.Any())
                            {
                                var sysIdList = _userManager.DataScope.Where(x => x.organizeType != null && x.organizeType.IsNotEmptyOrNull() && x.organizeId != sysId).Select(x => x.organizeId).ToList();
                                foreach (var item in sysIdList)
                                {
                                    mList = await GetModuleBySystemId(item, type, noContainsMIdList, noContainsMUrlList);
                                    if (mList.Any())
                                    {
                                        if (_userManager.UserOrigin.Equals("pc")) userInfo.SystemId = item;
                                        else userInfo.AppSystemId = item;
                                        isAnyModule = true;
                                        break;
                                    }
                                }

                                if (!isAnyModule) throw Oops.Oh(ErrorCode.D4024);

                                if (_userManager.UserOrigin.Equals("pc")) userInfo.SystemId = "2";
                                else userInfo.AppSystemId = "2";
                            }
                        }

                        break;
                    case "3":
                        var pIds = _userManager.GetPermissionByUserId(_userManager.UserId);
                        var sIds = _repository.AsSugarClient().Queryable<AuthorizeEntity>().Where(a => pIds.Contains(a.ObjectId) && a.ItemType == "system").Select(a => a.ItemId).ToList();
                        if (!sIds.Any())
                        {
                            throw Oops.Oh(ErrorCode.D4024);
                        }
                        else
                        {
                            var isAnyModule = false;

                            await _userManager.SetUserOrganizeByPermission(_userManager.UserId);
                            var oldStanding = _userManager.User.Copy();
                            if (type.Equals("Web"))
                                await _repository.AsSugarClient().Updateable<UserEntity>().SetColumns(x => new UserEntity() { Standing = 3 }).Where(x => x.Id.Equals(_userManager.UserId)).ExecuteCommandAsync();
                            else if (type.Equals("App"))
                                await _repository.AsSugarClient().Updateable<UserEntity>().SetColumns(x => new UserEntity() { AppStanding = 3 }).Where(x => x.Id.Equals(_userManager.UserId)).ExecuteCommandAsync();

                            await _userManager.GetUserInfo();

                            foreach (var item in sIds)
                            {
                                if ((await _moduleService.GetUserModuleListByIds(type, item, noContainsMIdList, noContainsMUrlList)).Any())
                                {
                                    isAnyModule = true;
                                    break;
                                }
                            }

                            if (!isAnyModule)
                            {
                                await _repository.AsSugarClient().Updateable<UserEntity>().SetColumns(x => new UserEntity() { Standing = oldStanding.Standing, AppStanding = oldStanding.AppStanding }).Where(x => x.Id.Equals(_userManager.UserId)).ExecuteCommandAsync();
                                throw Oops.Oh(ErrorCode.D4024);
                            }
                        }

                        break;
                }

                if (input.menuType.Equals(1)) userInfo.AppStanding = input.majorId.ParseToInt();
                else userInfo.Standing = input.majorId.ParseToInt();
                break;
        }

        userInfo.LastModifyTime = DateTime.Now;
        userInfo.LastModifyUserId = _userManager.UserId;
        int isOk = await _repository.AsUpdateable(userInfo).UpdateColumns(it => new {
            it.OrganizeId,
            it.PositionId,
            it.LastModifyUserId,
            it.LastModifyTime,
            it.SystemId,
            it.AppSystemId,
            it.Standing,
            it.AppStanding,
        }).IgnoreColumns(ignoreAllNullColumns: true).ExecuteCommandAsync();
        if (!(isOk > 0)) throw Oops.Oh(ErrorCode.D5020);
    }

    /// <summary>
    /// 获取当前用户所有组织.
    /// </summary>
    /// <returns></returns>
    [HttpGet("getUserOrganizes")]
    public async Task<dynamic> GetUserOrganizes()
    {
        UserEntity? userInfo = _userManager.User;

        // 获取当前用户所有关联 组织ID 集合
        List<string>? idList = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
            .Where(x => x.UserId == userInfo.Id && x.ObjectType == "Organize")
            .Select(x => x.ObjectId).ToListAsync();

        // 获取组织树
        var orgTree = _organizeService.GetOrgListTreeName();

        // 根据关联组织ID 查询组织信息
        List<CurrentUserOrganizesOutput>? oList = orgTree.Where(x => idList.Contains(x.Id))
            .Select(x => new CurrentUserOrganizesOutput
            {
                id = x.Id,
                fullName = x.Description
            }).ToList();

        CurrentUserOrganizesOutput? def = oList.Where(x => x.id == userInfo.OrganizeId).FirstOrDefault();
        if (def != null) def.isDefault = true;

        return oList;
    }

    /// <summary>
    /// 获取当前用户所有岗位.
    /// </summary>
    /// <returns></returns>
    [HttpGet("getUserPositions")]
    public async Task<dynamic> GetUserPositions()
    {
        UserEntity? userInfo = _userManager.User;

        // 获取当前用户所有关联 岗位ID 集合
        List<string>? idList = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
            .Where(x => x.UserId == userInfo.Id && x.ObjectType == "Position")
            .Select(x => x.ObjectId).ToListAsync();

        // 根据关联 岗位ID 查询岗位信息
        List<CurrentUserOrganizesOutput>? oList = await _repository.AsSugarClient().Queryable<PositionEntity>()
            .Where(x => x.OrganizeId == userInfo.OrganizeId).Where(x => idList.Contains(x.Id))
            .Select(x => new CurrentUserOrganizesOutput
            {
                id = x.Id,
                fullName = x.FullName
            }).ToListAsync();

        CurrentUserOrganizesOutput? def = oList.Where(x => x.id == userInfo.PositionId).FirstOrDefault();
        if (def != null) def.isDefault = true;

        return oList;
    }

    /// <summary>
    /// 获取当前用户所有签名.
    /// </summary>
    /// <returns></returns>
    [HttpGet("SignImg")]
    public async Task<dynamic> GetSignImg()
    {
        try
        {
            return (await _repository.AsSugarClient().Queryable<SignImgEntity>().Where(x => x.CreatorUserId == _userManager.UserId && x.DeleteMark == null).ToListAsync()).Adapt<List<UsersCurrentSignImgOutput>>();
        }
        catch (Exception)
        {

            throw;
        }
    }

    /// <summary>
    /// 新增签名.
    /// </summary>
    /// <returns></returns>
    [HttpPost("SignImg")]
    public async Task CreateSignImg([FromBody] UsersCurrentSignImgOutput input)
    {
        if (!_repository.AsSugarClient().Queryable<SignImgEntity>().Any(x => x.CreatorUserId == _userManager.UserId))
        {
            input.isDefault = 1;
        }
        var signImgEntity = input.Adapt<SignImgEntity>();
        var entity = await _repository.AsSugarClient().Insertable(signImgEntity).IgnoreColumns(ignoreNullColumn: true).CallEntityMethod(m => m.Creator()).ExecuteReturnEntityAsync();
        if (entity.IsNullOrEmpty())
            throw Oops.Oh(ErrorCode.COM1000);
        if (input.isDefault == 1)
        {
            await _repository.AsSugarClient().Updateable<SignImgEntity>().SetColumns(x => x.IsDefault == 0).Where(x => x.Id != entity.Id && x.CreatorUserId == _userManager.UserId).ExecuteCommandAsync();
        }
    }

    /// <summary>
    /// 设置默认签名.
    /// </summary>
    /// <returns></returns>
    [HttpPut("{id}/SignImg")]
    public async Task UpdateSignImg(string id)
    {
        await _repository.AsSugarClient().Updateable<SignImgEntity>().SetColumns(x => x.IsDefault == 0).Where(x => x.Id != id && x.CreatorUserId == _userManager.UserId).ExecuteCommandAsync();
        await _repository.AsSugarClient().Updateable<SignImgEntity>().SetColumns(x => x.IsDefault == 1).Where(x => x.Id == id).ExecuteCommandAsync();
    }

    /// <summary>
    /// 删除签名.
    /// </summary>
    /// <returns></returns>
    [HttpDelete("{id}/SignImg")]
    public async Task DeleteSignImg(string id)
    {
        var isOk = await _repository.AsSugarClient().Updateable<SignImgEntity>().SetColumns(it => new SignImgEntity()
        {
            DeleteMark = 1,
            DeleteUserId = _userManager.UserId,
            DeleteTime = SqlFunc.GetDate()
        }).Where(it => it.Id.Equals(id)).ExecuteCommandHasChangeAsync();
        if (!isOk)
            throw Oops.Oh(ErrorCode.COM1003);
    }
    #endregion

    #region PrivateMethod

    /// <summary>
    /// 过滤菜单权限数据.
    /// </summary>
    /// <param name="pids">其他权限数据.</param>
    /// <param name="moduleList">勾选菜单权限数据.</param>
    /// <param name="output">返回值.</param>
    private void GetParentsModuleList(List<string> pids, List<ModuleEntity> moduleList, ref List<UsersCurrentAuthorizeMoldel> output)
    {
        List<UsersCurrentAuthorizeMoldel>? authorizeModuleData = moduleList.Adapt<List<UsersCurrentAuthorizeMoldel>>();
        foreach (string? item in pids)
        {
            GteModuleListById(item, authorizeModuleData, output);
        }

        output = output.Distinct().ToList();
    }

    /// <summary>
    /// 根据菜单id递归获取authorizeDataOutputModel的父级菜单.
    /// </summary>
    /// <param name="id">菜单id.</param>
    /// <param name="authorizeModuleData">选中菜单集合.</param>
    /// <param name="output">返回数据.</param>
    private void GteModuleListById(string id, List<UsersCurrentAuthorizeMoldel> authorizeModuleData, List<UsersCurrentAuthorizeMoldel> output)
    {
        UsersCurrentAuthorizeMoldel? data = authorizeModuleData.Find(l => l.id == id);
        if (data != null)
        {
            if (!data.parentId.Equals("-1"))
            {
                if (!output.Contains(data)) output.Add(data);

                GteModuleListById(data.parentId, authorizeModuleData, output);
            }
            else
            {
                if (!output.Contains(data)) output.Add(data);
            }
        }
    }

    /// <summary>
    /// 获取验证码.
    /// </summary>
    /// <param name="timestamp">时间戳.</param>
    /// <returns></returns>
    private async Task<string> GetCode(string timestamp)
    {
        string? cacheKey = string.Format("{0}{1}", CommonConst.CACHEKEYCODE, timestamp);
        return await _cacheManager.GetAsync<string>(cacheKey);
    }

    /// <summary>
    /// 删除验证码.
    /// </summary>
    /// <param name="timestamp">时间戳.</param>
    /// <returns></returns>
    private Task<bool> DelCode(string timestamp)
    {
        string? cacheKey = string.Format("{0}{1}", CommonConst.CACHEKEYCODE, timestamp);
        _cacheManager.DelAsync(cacheKey);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 删除用户登录信息缓存.
    /// </summary>
    /// <param name="tenantId">租户ID.</param>
    /// <param name="userId">用户ID.</param>
    /// <returns></returns>
    private Task<bool> DelUserInfo(string tenantId, string userId)
    {
        string? cacheKey = string.Format("{0}:{1}:{2}", tenantId, CommonConst.CACHEKEYUSER, userId);
        _cacheManager.DelAsync(cacheKey);
        return Task.FromResult(true);
    }

    /// <summary>
    /// 密码策略验证.
    /// </summary>
    /// <returns></returns>
    private async Task<bool> PwdStrategy(UsersCurrentActionsModifyPasswordInput input)
    {
        // 系统配置信息
        var sysInfo = await _sysConfigService.GetInfo();
        // 禁用旧密码
        if (sysInfo.disableOldPassword == 1 && sysInfo.disableTheNumberOfOldPasswords > 0)
        {
            var oldPwdList = _repository.AsSugarClient().Queryable<UserOldPasswordEntity>().Where(x => x.UserId == _userManager.UserId).OrderByDescending(o => o.CreatorTime).Take(sysInfo.disableTheNumberOfOldPasswords).ToList();
            if (oldPwdList.Any())
            {
                foreach (var item in oldPwdList)
                {
                    if (MD5Encryption.Encrypt(input.password + item.Secretkey) == item.OldPassword.ToLower())
                        return true;
                }
            }
        }

        // 保存旧密码数据
        var oldPwdEntity = new UserOldPasswordEntity();
        oldPwdEntity.Id = SnowflakeIdHelper.NextId();
        oldPwdEntity.UserId = _userManager.UserId;
        oldPwdEntity.Account = _userManager.Account;
        oldPwdEntity.OldPassword = MD5Encryption.Encrypt(input.password + _userManager.User.Secretkey);
        oldPwdEntity.Secretkey = _userManager.User.Secretkey;
        oldPwdEntity.CreatorTime = DateTime.Now;
        oldPwdEntity.TenantId = _userManager.TenantId;
        _repository.AsSugarClient().Insertable(oldPwdEntity).ExecuteCommand();
        return false;
    }

    /// <summary>
    /// 获取分管应用权限.
    /// </summary>
    /// <param name="systemId"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    private async Task<List<ModuleEntity>> GetModuleBySystemId(string systemId, string type, List<string> mIds = null, List<string> mUrls = null)
    {
        var result = new List<ModuleEntity>();

        // 获取所有分管系统Ids
        var dataScop = _userManager.DataScope;
        var objectIdList = dataScop.Where(x => x.organizeType != null && (x.organizeType.Equals("System") || x.organizeType.Equals("Module"))).Select(x => x.organizeId).ToList();

        // 当前系统在分管范围内
        if (objectIdList.Any(x => x.Equals(systemId)))
        {
            if (await _repository.AsSugarClient().Queryable<SystemEntity>().AnyAsync(a => a.Id.Equals(systemId) && a.EnCode.Equals("mainSystem")))
            {
                // 当前系统的有分管权限的菜单
                result = await _repository.AsSugarClient().Queryable<ModuleEntity>()
                    .Where(a => (a.SystemId.Equals(systemId) && objectIdList.Contains(a.Id) && a.EnabledMark == 1 && a.Category.Equals(type) && a.DeleteMark == null)
                    || _userManager.CommonModuleEnCodeList.Contains(a.EnCode))
                    .OrderBy(q => q.ParentId).OrderBy(q => q.SortCode).OrderBy(q => q.CreatorTime, OrderByType.Desc).ToListAsync();
            }
            else
            {
                // 当前系统的所有菜单
                result = await _repository.AsSugarClient().Queryable<ModuleEntity>()
                    .Where(a => (a.SystemId.Equals(systemId) && a.EnabledMark == 1 && a.Category.Equals(type) && a.DeleteMark == null)
                    || _userManager.CommonModuleEnCodeList.Contains(a.EnCode))
                    .OrderBy(q => q.ParentId).OrderBy(q => q.SortCode).OrderBy(q => q.CreatorTime, OrderByType.Desc).ToListAsync();
            }
        }

        // 工作流程
        var workflowEnabled = await _repository.AsSugarClient().Queryable<SystemEntity>().Where(it => (it.Id.Equals(systemId) && it.DeleteMark == null)).Select(it => it.WorkflowEnabled).FirstAsync();
        if (type != "Web") workflowEnabled = await _repository.AsSugarClient().Queryable<SystemEntity>().Where(it => (it.Id.Equals(systemId) && !it.EnCode.Equals("mainSystem") && it.DeleteMark == null)).Select(it => it.WorkflowEnabled).FirstAsync();
        var workFlow = result.Where(it => it.EnCode.Equals("workFlow")).FirstOrDefault();
        if (workFlow.IsNotEmptyOrNull())
        {
            var cList = result.Where(x => x.ParentId.Equals(workFlow.Id)).ToList().Copy();
            result.RemoveAll(x => x.ParentId.Equals(workFlow.Id));
            result.RemoveAll(x => x.Id.Equals(workFlow.Id));
            if (workflowEnabled.IsNotEmptyOrNull() && workflowEnabled.Equals(1))
            {
                result.Insert(0, workFlow);
                result.InsertRange(1, cList);
            }
        }

        return result.Where(x => !mIds.Contains(x.Id) && !mUrls.Contains(x.UrlAddress)).ToList();
    }

    #endregion
}