using JNPF.Common.Const;
using JNPF.Common.Enums;
using JNPF.Common.Extension;
using JNPF.Common.Manager;
using JNPF.Common.Models.Authorize;
using JNPF.Common.Models.User;
using JNPF.Common.Net;
using JNPF.Common.Security;
using JNPF.DependencyInjection;
using JNPF.LinqBuilder;
using JNPF.Systems.Entitys.Entity.Permission;
using JNPF.Systems.Entitys.Permission;
using JNPF.Systems.Entitys.System;
using Mapster;
using Microsoft.AspNetCore.Http;
using NPOI.Util;
using SqlSugar;
using System.Security.Claims;

namespace JNPF.Common.Core.Manager;

/// <summary>
/// 当前登录用户.
/// </summary>
public class UserManager : IUserManager, IScoped
{
    /// <summary>
    /// 用户表仓储.
    /// </summary>
    private readonly ISqlSugarRepository<UserEntity> _repository;

    /// <summary>
    /// 缓存管理.
    /// </summary>
    private readonly ICacheManager _cacheManager;

    /// <summary>
    /// 当前Http请求.
    /// </summary>
    private readonly HttpContext _httpContext;

    /// <summary>
    /// 用户Claim主体.
    /// </summary>
    private readonly ClaimsPrincipal _user;

    /// <summary>
    /// 初始化一个<see cref="UserManager"/>类型的新实例.
    /// </summary>
    /// <param name="repository">用户仓储.</param>
    /// <param name="cacheManager">缓存管理.</param>
    public UserManager(
        ISqlSugarRepository<UserEntity> repository,
        ICacheManager cacheManager)
    {
        _repository = repository;
        _cacheManager = cacheManager;
        _httpContext = App.HttpContext;
        _user = _httpContext?.User;
    }

    /// <summary>
    /// 用户信息.
    /// </summary>
    public UserEntity User
    {
        get
        {
            if (_userEntity == null) _userEntity = _repository.GetSingle(u => u.Id == UserId);
            return _userEntity;
        }
    }
    private UserEntity _userEntity { get; set; }

    /// <summary>
    /// 用户ID.
    /// </summary>
    public string UserId
    {
        get => _user.FindFirst(ClaimConst.CLAINMUSERID)?.Value;
    }

    /// <summary>
    /// 获取用户角色.
    /// </summary>
    public List<string> Roles
    {
        get
        {
            if (_roles == null)
            {
                var user = _repository.GetSingle(u => u.Id == UserId);
                _roles = GetUserRoleIds(user.RoleId, user.OrganizeId);
            }

            return _roles;
        }
    }
    private List<string> _roles { get; set; }

    /// <summary>
    /// 用户权限组Ids.
    /// </summary>
    public List<string> PermissionGroup
    {
        get
        {
            if (_permissionGroup == null) _permissionGroup = GetPermissionGroupIds();
            return _permissionGroup;
        }
    }
    private List<string> _permissionGroup { get; set; }

    /// <summary>
    /// 多租户场景忽略过滤的应用和菜单Ids.
    /// </summary>
    public List<string>? TenantIgnoreModuleIdList
    {
        get => GetGlobalTenantCache().Where(x => x.TenantId.Equals(TenantId)).FirstOrDefault()?.moduleIdList;
    }

    /// <summary>
    /// 多租户场景忽略过滤的应用和菜单urlList.
    /// </summary>
    public List<string>? TenantIgnoreUrlAddressList
    {
        get
        {
            var urlAddressList = GetGlobalTenantCache().Where(x => x.TenantId.Equals(TenantId)).FirstOrDefault()?.urlAddressList;
            if (TenantIgnoreModuleIdList.IsNotEmptyOrNull() && urlAddressList == null) return new List<string>();
            return urlAddressList;
        }
    }

    /// <summary>
    /// 用户账号.
    /// </summary>
    public string Account
    {
        get => _user.FindFirst(ClaimConst.CLAINMACCOUNT)?.Value;
    }

    /// <summary>
    /// 用户昵称.
    /// </summary>
    public string RealName
    {
        get => _user.FindFirst(ClaimConst.CLAINMREALNAME)?.Value;
    }

    /// <summary>
    /// 租户ID.
    /// </summary>
    public string TenantId
    {
        get => _user.FindFirst(ClaimConst.TENANTID)?.Value;
    }

    /// <summary>
    /// 租户数据库名称.
    /// </summary>
    public string TenantDbName
    {
        get
        {
            var tenant = GetGlobalTenantCache(TenantId);
            if (tenant == null) return null;
            else return tenant.connectionConfig.ConfigList.FirstOrDefault().ServiceName;
        }
    }

    /// <summary>
    /// 当前用户 token.
    /// </summary>
    public string ToKen
    {
        get => string.IsNullOrEmpty(App.HttpContext?.Request.Headers["Authorization"]) ? App.HttpContext?.Request.Query["token"] : App.HttpContext?.Request.Headers["Authorization"];
    }

    /// <summary>
    /// 是否是管理员.
    /// </summary>
    public bool IsAdministrator
    {
        get => User.IsAdministrator.Equals(1) && Standing.Equals(1);
    }

    /// <summary>
    /// 当前租户配置.
    /// </summary>
    public GlobalTenantCacheModel CurrentTenantInformation
    {
        get => GetGlobalTenantCache(TenantId);
    }

    /// <summary>
    /// 当前用户下属.
    /// </summary>
    public List<string> Subordinates
    {
        get
        {
            if (_subordinates == null) _subordinates = GetSubordinates(UserId).ToList();
            return _subordinates;
        }
    }
    private List<string> _subordinates { get; set; }

    /// <summary>
    /// 当前用户及下属.
    /// </summary>
    public List<string> CurrentUserAndSubordinates
    {
        get
        {
            if (_currentUserAndSubordinates == null)
            {
                _currentUserAndSubordinates = new List<string> { UserId };
                _currentUserAndSubordinates.AddRange(GetSubordinates(UserId).ToList());
            }
            return _currentUserAndSubordinates;
        }
    }
    private List<string> _currentUserAndSubordinates { get; set; }

    /// <summary>
    /// 当前组织及子组织.
    /// </summary>
    public List<string> CurrentOrganizationAndSubOrganizations
    {
        get
        {
            if (_currentOrganizationAndSubOrganizations == null)
            {
                _currentOrganizationAndSubOrganizations = new List<string> { User.OrganizeId };
                _currentOrganizationAndSubOrganizations.AddRange(GetSubsidiary(User.OrganizeId).ToObject<List<string>>());
            }
            return _currentOrganizationAndSubOrganizations;
        }
    }
    private List<string> _currentOrganizationAndSubOrganizations { get; set; }

    /// <summary>
    /// 当前用户子组织.
    /// </summary>
    public List<string> CurrentUserSubOrganization
    {
        get
        {
            return GetSubsidiary(User.OrganizeId).ToObject<List<string>>();
        }
    }

    public bool IsOrganizeAdmin
    {
        get
        {
            if (_isOrganizeAdmin == null) _isOrganizeAdmin = _repository.AsSugarClient().Queryable<OrganizeAdministratorEntity>().Any(x => x.UserId.Equals(UserId));
            return (bool)_isOrganizeAdmin;
        }
    }
    private bool? _isOrganizeAdmin { get; set; }

    /// <summary>
    /// 当前pc/app身份.
    /// </summary>
    public int Standing
    {
        get
        {
            if (_standing == null) _standing = !"pc".Equals(UserOrigin) ? User.AppStanding : User.Standing;
            return _standing.IsNotEmptyOrNull() ? (int)_standing : 3;
        }
    }
    private int? _standing { get; set; }

    /// <summary>
    /// 获取用户的数据范围.
    /// </summary>
    public List<UserDataScopeModel> DataScope
    {
        get
        {
            if (_dataScope == null) _dataScope = GetUserDataScope(UserId);
            return _dataScope;
        }
    }

    private List<UserDataScopeModel> _dataScope { get; set; }

    /// <summary>
    /// 获取请求端类型 pc 、 app.
    /// </summary>
    public string UserOrigin
    {
        get => _httpContext?.Request.Headers["jnpf-origin"];
    }

    /// <summary>
    /// 获取请求vue版本
    /// 3-vue3,其他-vue2.
    /// </summary>
    public int VueVersion
    {

        get => (_httpContext?.Request.Headers["vue-version"].FirstOrDefault()).ParseToInt();
    }

    /// <summary>
    /// 获取公用菜单 编码 .
    /// </summary>
    /// <returns></returns>
    public List<string> CommonModuleEnCodeList
    {
        get
        {
            var list = new List<string> { "workFlow.addFlow", "workFlow.flowLaunch", "workFlow.entrust", "workFlow", "workFlow.flowTodo", "workFlow.flowDone", "workFlow.flowCirculate", "workFlow.flowToSign", "workFlow.flowDoing", "workFlow.schedule", "workFlow.document", "workFlow.printTemplate" };
            if (_repository.AsSugarClient().Queryable<SysConfigEntity>().Any(s => s.Category.Equals("SysConfig") && SqlFunc.ToString(s.Value) == "0" && s.Key.Equals("flowSign")))
            {
                list.Remove("workFlow.flowToSign");
            }
            if (_repository.AsSugarClient().Queryable<SysConfigEntity>().Any(s => s.Category.Equals("SysConfig") && SqlFunc.ToString(s.Value) == "0" && s.Key.Equals("flowTodo")))
            {
                list.Remove("workFlow.flowTodo");
            }
            return list;
        }
    }

    /// <summary>
    /// 获取用户登录信息.
    /// </summary>
    /// <returns></returns>
    public async Task<UserInfoModel> GetUserInfo()
    {
        UserAgent userAgent = new UserAgent(_httpContext);
        var data = new UserInfoModel();
        var userCache = string.Format("{0}:{1}:{2}", TenantId, CommonConst.CACHEKEYUSER, UserId);
        var userDataScope = GetUserDataScope(UserId);

        var ipAddress = NetHelper.Ip;
        var ipAddressName = await NetHelper.GetLocation(ipAddress);
        var sysConfigInfo = await _repository.AsSugarClient().Queryable<SysConfigEntity>().FirstAsync(s => s.Category.Equals("SysConfig") && s.Key.ToLower().Equals("tokentimeout"));
        _userEntity = null;
        _dataScope = null;
        _isOrganizeAdmin = null;
        _permissionGroup = null;
        _roles = null;
        _standing = null;
        data = await _repository.AsQueryable().Where(it => it.Id == UserId)
           .Select(a => new UserInfoModel
           {
               userId = a.Id,
               headIcon = SqlFunc.MergeString("/api/File/Image/userAvatar/", a.HeadIcon),
               userAccount = a.Account,
               userName = a.RealName,
               gender = a.Gender,
               organizeId = a.OrganizeId,
               departmentId = a.OrganizeId,
               departmentName = SqlFunc.Subqueryable<OrganizeEntity>().EnableTableFilter().Where(o => o.Id == a.OrganizeId && o.Category.Equals("department")).Select(o => o.FullName),
               organizeName = SqlFunc.Subqueryable<OrganizeEntity>().EnableTableFilter().Where(o => o.Id == a.OrganizeId).Select(o => o.OrganizeIdTree),
               managerId = a.ManagerId,
               isAdministrator = SqlFunc.IIF(a.IsAdministrator == 1, true, false),
               portalId = a.PortalId,
               positionId = a.PositionId,
               roleId = a.RoleId,
               prevLoginTime = a.PrevLogTime,
               prevLoginIPAddress = SqlFunc.IIF(a.PrevLogIP != null, a.PrevLogIP, "127.0.0.1"),
               landline = a.Landline,
               telePhone = a.TelePhone,
               manager = SqlFunc.Subqueryable<UserEntity>().EnableTableFilter().Where(u => u.Id == a.ManagerId).Select(u => SqlFunc.MergeString(u.RealName, "/", u.Account)),
               mobilePhone = a.MobilePhone,
               email = a.Email,
               birthday = a.Birthday,
               systemId = a.SystemId,
               appSystemId = a.AppSystemId,
               signImg = SqlFunc.Subqueryable<SignImgEntity>().EnableTableFilter().Where(a => a.CreatorUserId == UserId && a.IsDefault == 1).Select(a => a.SignImg),
               signId = SqlFunc.Subqueryable<SignImgEntity>().EnableTableFilter().Where(a => a.CreatorUserId == UserId && a.IsDefault == 1).Select(a => a.Id),
               changePasswordDate = a.ChangePasswordDate,
               loginTime = DateTime.Now,
               standing = a.Standing,
               appStanding = a.AppStanding,
           }).FirstAsync();

        if (data != null && data.organizeName.IsNotEmptyOrNull())
        {
            var orgIdTree = data?.organizeName?.Split(',');
            data.organizeIdList = orgIdTree.ToList();
            var organizeName = await _repository.AsSugarClient().Queryable<OrganizeEntity>().Where(x => orgIdTree.Contains(x.Id)).OrderBy(x => x.SortCode).OrderBy(x => x.CreatorTime).Select(x => x.FullName).ToListAsync();
            data.organizeName = string.Join("/", organizeName);
        }
        else
        {
            data.organizeName = data.departmentName;
        }
        data.prevLogin = (await _repository.AsSugarClient().Queryable<SysConfigEntity>().FirstAsync(x => x.Category.Equals("SysConfig") && x.Key.ToLower().Equals("lastlogintimeswitch"))).Value.ParseToInt();
        data.loginIPAddress = ipAddress;
        data.loginIPAddressName = ipAddressName;
        data.prevLoginIPAddressName = await NetHelper.GetLocation(data.prevLoginIPAddress);
        data.loginPlatForm = userAgent.RawValue;
        if (data.organizeId.IsNotEmptyOrNull())
            data.subsidiary = await GetSubsidiaryAsync(data.organizeId, data.isAdministrator);
        data.subordinates = await this.GetSubordinatesAsync(UserId);

        if (data.positionId.IsNotEmptyOrNull())
        {
            var positionIdList = await GetPosition(data.organizeId);
            if (positionIdList.Select(it => it.id).Contains(data.positionId))
            {
                var mainPosition = positionIdList.Find(it => it.id.Equals(data.positionId));
                positionIdList.Remove(mainPosition);
                positionIdList.Insert(0, mainPosition);
            }
            data.positionIds = positionIdList;
        }
        else
        {
            data.positionIds = new List<PositionInfoModel>();
        }

        data.positionName = await _repository.AsSugarClient().Queryable<PositionEntity>().Where(it => it.DeleteMark == null && it.Id.Equals(data.positionId)).Select(it => it.FullName).FirstAsync();

        var roleList = GetUserRoleIds(data.roleId, data.organizeId);
        data.roleName = await GetRoleNameByIds(string.Join(",", roleList));
        data.roleIds = roleList.ToArray();
        data.groupIds = await _repository.AsSugarClient().Queryable<GroupEntity, UserRelationEntity>((a, b) => new JoinQueryInfos(JoinType.Left, a.Id.Equals(b.ObjectId) && b.ObjectType.Equals("Group"))).Where((a, b) => a.EnabledMark == 1 && a.DeleteMark == null && b.UserId.Equals(data.userId)).Select((a, b) => b.ObjectId).ToListAsync();
        data.groupNames = await _repository.AsSugarClient().Queryable<GroupEntity>().Where(it => data.groupIds.Contains(it.Id)).Select(x => x.FullName).ToListAsync();
        data.overdueTime = TimeSpan.FromMinutes(sysConfigInfo.Value.ParseToDouble());
        data.dataScope = userDataScope;
        data.tenantId = TenantId;

        var currSysId = UserOrigin.Equals("pc") ? User.SystemId : User.AppSystemId;
        data.workflowEnabled = await _repository.AsSugarClient().Queryable<SystemEntity>().Where(it => it.Id.Equals(currSysId) && it.DeleteMark == null).Select(it => it.WorkflowEnabled).FirstAsync();

        if (data.isAdministrator)
        {
            var addItem = new UserStanding() { id = "1", name = "超级管理员" };
            if (data.standing == null)
            {
                addItem.currentStanding = true;
                await _repository.AsSugarClient().Updateable<UserEntity>().SetColumns(x => new UserEntity() { Standing = 1 }).Where(x => x.Id.Equals(UserId)).ExecuteCommandAsync();
                _userEntity = _repository.GetSingle(u => u.Id == UserId);
                data.standing = 1;
            }

            if (data.appStanding == null)
            {
                addItem.currentStanding = true;
                await _repository.AsSugarClient().Updateable<UserEntity>().SetColumns(x => new UserEntity() { AppStanding = 1 }).Where(x => x.Id.Equals(UserId)).ExecuteCommandAsync();
                _userEntity = _repository.GetSingle(u => u.Id == UserId);
                data.appStanding = 1;
            }

            addItem.currentStanding = UserOrigin.Equals("pc") ? data.standing.Equals(1) : data.appStanding.Equals(1);
            data.standingList.Add(addItem);
        }
        if (IsOrganizeAdmin)
        {
            var addItem = new UserStanding() { id = "2", name = "普通管理员" };

            if (data.standing == null)
            {
                addItem.currentStanding = true;
                await _repository.AsSugarClient().Updateable<UserEntity>().SetColumns(x => new UserEntity() { Standing = 2 }).Where(x => x.Id.Equals(UserId)).ExecuteCommandAsync();
                _userEntity = _repository.GetSingle(u => u.Id == UserId);
                data.standing = 2;
            }

            if (data.appStanding == null)
            {
                addItem.currentStanding = true;
                await _repository.AsSugarClient().Updateable<UserEntity>().SetColumns(x => new UserEntity() { AppStanding = 2 }).Where(x => x.Id.Equals(UserId)).ExecuteCommandAsync();
                _userEntity = _repository.GetSingle(u => u.Id == UserId);
                data.appStanding = 2;
            }

            addItem.currentStanding = UserOrigin.Equals("pc") ? data.standing.Equals(2) : data.appStanding.Equals(2);
            data.standingList.Add(addItem);
        }

        var item = new UserStanding() { id = "3", name = "普通用户" };

        if (data.standing == null)
        {
            item.currentStanding = true;
            await _repository.AsSugarClient().Updateable<UserEntity>().SetColumns(x => new UserEntity() { Standing = 3 }).Where(x => x.Id.Equals(UserId)).ExecuteCommandAsync();
            _userEntity = _repository.GetSingle(u => u.Id == UserId);
            data.standing = 3;
        }

        if (data.appStanding == null)
        {
            item.currentStanding = true;
            await _repository.AsSugarClient().Updateable<UserEntity>().SetColumns(x => new UserEntity() { AppStanding = 3 }).Where(x => x.Id.Equals(UserId)).ExecuteCommandAsync();
            _userEntity = _repository.GetSingle(u => u.Id == UserId);
            data.appStanding = 3;
        }

        item.currentStanding = UserOrigin.Equals("pc") ? data.standing.Equals(3) : data.appStanding.Equals(3);
        data.standingList.Add(item);
        if (data.userAccount.Equals("admin")) data.standingList.Clear();

        // 根据系统配置过期时间自动过期
        await SetUserInfo(userCache, data, TimeSpan.FromMinutes(sysConfigInfo.Value.ParseToDouble()));

        return data;
    }

    /// <summary>
    /// 获取用户数据范围.
    /// </summary>
    /// <param name="userId">用户ID.</param>
    /// <returns></returns>
    public List<UserDataScopeModel> GetUserDataScope(string userId)
    {
        List<UserDataScopeModel> data = new List<UserDataScopeModel>();
        List<UserDataScopeModel> subData = new List<UserDataScopeModel>();
        List<UserDataScopeModel> inteList = new List<UserDataScopeModel>();

        // 填充数据
        foreach (var item in _repository.AsSugarClient().Queryable<OrganizeAdministratorEntity>()
            .Where(it => it.UserId == userId && it.DeleteMark == null).ToList())
        {
            if (item.SubLayerSelect.ParseToBool() || item.SubLayerAdd.ParseToBool() || item.SubLayerEdit.ParseToBool() || item.SubLayerDelete.ParseToBool())
            {
                var subsidiary = GetSubsidiary(item.OrganizeId).ToList();
                subsidiary.Remove(item.OrganizeId);
                subsidiary.ToList().ForEach(it =>
                {
                    subData.Add(new UserDataScopeModel()
                    {
                        organizeId = it,
                        Add = item.SubLayerAdd.ParseToBool(),
                        Edit = item.SubLayerEdit.ParseToBool(),
                        Delete = item.SubLayerDelete.ParseToBool(),
                        Select = item.SubLayerSelect.ParseToBool()
                    });
                });
            }

            if (item.ThisLayerSelect.ParseToBool() || item.ThisLayerAdd.ParseToBool() || item.ThisLayerEdit.ParseToBool() || item.ThisLayerDelete.ParseToBool())
            {
                data.Add(new UserDataScopeModel()
                {
                    organizeId = item.OrganizeId,
                    organizeType = item.OrganizeType,
                    Add = item.ThisLayerAdd.ParseToBool(),
                    Edit = item.ThisLayerEdit.ParseToBool(),
                    Delete = item.ThisLayerDelete.ParseToBool(),
                    Select = item.ThisLayerSelect.ParseToBool()
                });
            }
        }

        /* 比较数据
        所有分级数据权限以本级权限为主 子级为辅
        将本级数据与子级数据对比 对比出子级数据内组织ID存在本级数据的组织ID*/
        var intersection = data.Select(it => it.organizeId).Intersect(subData.Select(it => it.organizeId)).ToList();
        intersection.ForEach(it =>
        {
            var parent = data.Find(item => item.organizeId == it);
            var child = subData.Find(item => item.organizeId == it);
            var add = false;
            var edit = false;
            var delete = false;
            var select = false;
            if (parent.Add || child.Add) add = true;
            if (parent.Edit || child.Edit) edit = true;
            if (parent.Delete || child.Delete) delete = true;
            if (parent.Select || child.Select) select = true;
            inteList.Add(new UserDataScopeModel()
            {
                organizeId = it,
                Add = add,
                Edit = edit,
                Delete = delete,
                Select = select
            });
            data.Remove(parent);
            subData.Remove(child);
        });
        return data.Union(subData).Union(inteList).ToList();
    }

    /// <summary>
    /// 获取数据条件.
    /// </summary>
    /// <typeparam name="T">实体.</typeparam>
    /// <param name="moduleId">模块ID.</param>
    /// <param name="primaryKey">表主键.</param>
    /// <param name="isDataPermissions">是否开启数据权限.</param>
    /// <param name="primaryKeyPolicy">是否自增长Id.</param>
    /// <returns></returns>
    public async Task<List<IConditionalModel>> GetConditionAsync<T>(string moduleId, string primaryKey = "f_id", bool isDataPermissions = true, int primaryKeyPolicy = 1)
        where T : new()
    {
        var resourceList = await GetCodeGenAuthorizeModuleResource<T>(moduleId, primaryKey, primaryKeyPolicy, isDataPermissions);
        var res = new List<IConditionalModel>();
        foreach (var item in resourceList) res.AddRange(item.conditionalModel);
        return res;
    }

    /// <summary>
    /// 获取代码生成数据条件 .
    /// </summary>
    /// <typeparam name="T">实体.</typeparam>
    /// <param name="moduleId">模块ID.</param>
    /// <param name="primaryKey">表主键.</param>
    /// <param name="primaryKeyPolicy">是否自增长Id.</param>
    /// <param name="isDataPermissions">是否开启数据权限.</param>
    /// <returns></returns>
    public async Task<List<CodeGenAuthorizeModuleResourceModel>> GetCodeGenAuthorizeModuleResource<T>(string moduleId, string primaryKey, int primaryKeyPolicy, bool isDataPermissions = true)
        where T : new()
    {
        var codeGenConditional = new List<CodeGenAuthorizeModuleResourceModel>();

        // 获取所有数据权限的 表名
        var resourceList = await _repository.AsSugarClient().Queryable<ModuleDataAuthorizeSchemeEntity>().Where(it => it.ModuleId == moduleId && it.DeleteMark == null).ToListAsync();

        var allTableName = new List<string>();
        foreach (var resourceItem in resourceList)
        {
            if (resourceItem != null && resourceItem.ConditionJson != null && resourceItem.ConditionJson.Any())
            {
                var items = resourceItem.ConditionJson.ToList<AuthorizeModuleResourceConditionModel>();
                items.ForEach(it => allTableName.AddRange(it.Groups.Select(x => x.BindTable)));
            }
        }

        var condList = await GetCondition<object>(primaryKey, moduleId, isDataPermissions, primaryKeyPolicy.Equals(2));

        var minTable = GetIConditionalModelListByTableName(condList.Copy(), null);

        if (minTable.Any())
        {
            codeGenConditional.Add(new CodeGenAuthorizeModuleResourceModel()
            {
                conditionalModel = minTable,
                TableName = string.Empty,
                FieldRule = 0
            });
        }

        foreach (var tName in allTableName.Distinct().ToList())
        {
            var tNameConditional = GetIConditionalModelListByTableName(condList.Copy(), tName);

            if (tNameConditional.Any())
            {
                codeGenConditional.Add(new CodeGenAuthorizeModuleResourceModel()
                {
                    conditionalModel = tNameConditional,
                    TableName = tName,
                    FieldRule = -1
                });
            }
        }

        return codeGenConditional;
    }
    private List<IConditionalModel> GetIConditionalModelListByTableName(List<IConditionalModel> cList, string tableName)
    {
        for (int i = 0; i < cList.Count; i++)
        {
            if (cList[i] is ConditionalTree)
            {
                var newItem = (ConditionalTree)cList[i];
                for (int j = 0; j < newItem.ConditionalList.Count; j++)
                {
                    var value = GetIConditionalModelListByTableName(new List<IConditionalModel>() { newItem.ConditionalList[j].Value }, tableName);
                    if (value != null && value.Any())
                    {
                        if (newItem.ConditionalList[j].Equals(newItem.ConditionalList.FirstOrDefault()))
                            newItem.ConditionalList[j] = new KeyValuePair<WhereType, IConditionalModel>(newItem.ConditionalList[j].Key, value.First());
                        else
                            newItem.ConditionalList[j] = new KeyValuePair<WhereType, IConditionalModel>(newItem.ConditionalList[j].Key, value.First());
                    }
                    else
                    {
                        newItem.ConditionalList.RemoveAt(j);
                        j--;
                    }
                }

                if (newItem.ConditionalList.Any())
                {
                    cList[i] = newItem;
                }
                else
                {
                    cList.RemoveAt(i);
                    i--;
                }
            }
            else if (cList[i] is ConditionalCollections)
            {
                var newItemList = (ConditionalCollections)cList[i];

                for (int j = 0; j < newItemList.ConditionalList.Count; j++)
                {
                    if ((tableName.IsNullOrEmpty() && newItemList.ConditionalList[j].Value.FieldName.Contains(".")) || tableName.IsNotEmptyOrNull() && !newItemList.ConditionalList[j].Value.FieldName.Contains(tableName + "."))
                    {
                        newItemList.ConditionalList.RemoveAt(j);
                    }
                    else
                    {
                        newItemList.ConditionalList[j].Value.FieldName = newItemList.ConditionalList[j].Value.FieldName.Split(".").Last();
                    }
                }
                if (newItemList.ConditionalList.Any()) cList[i] = newItemList;
                else cList.RemoveAt(i);
            }
            else if (cList[i] is ConditionalModel)
            {
                var newItem = (ConditionalModel)cList[i];
                if ((tableName.IsNullOrEmpty() && newItem.FieldName.Contains(".")) || tableName.IsNotEmptyOrNull() && !newItem.FieldName.Contains(tableName + "."))
                {
                    cList.RemoveAt(i);
                }
                else
                {
                    newItem.FieldName = newItem.FieldName.Split(".").Last();
                    cList[i] = newItem;
                }
            }
        }

        return cList;
    }

    /// <summary>
    /// 获取数据条件(在线开发专用) .
    /// </summary>
    /// <typeparam name="T">实体.</typeparam>
    /// <param name="primaryKey">表主键.</param>
    /// <param name="moduleId">模块ID.</param>
    /// <param name="isDataPermissions">是否开启数据权限.</param>
    /// <param name="primaryKeyPolicy">是否自增长Id.</param>
    /// <returns></returns>
    public async Task<List<IConditionalModel>> GetCondition<T>(string primaryKey, string moduleId, bool isDataPermissions, bool primaryKeyPolicy)
        where T : new()
    {
        var primaryWhere = new KeyValuePair<WhereType, ConditionalModel>(WhereType.And, new ConditionalModel() { FieldName = primaryKey, ConditionalType = ConditionalType.NoEqual, FieldValue = "0", FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(string)) });
        if (primaryKeyPolicy) primaryWhere = new KeyValuePair<WhereType, ConditionalModel>(WhereType.And, new ConditionalModel() { FieldName = primaryKey, ConditionalType = ConditionalType.NoEqual, FieldValue = "0", FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(int)) });

        var conModels = new List<IConditionalModel>();
        if (IsAdministrator) return conModels; // 管理员全部放开
        var dataScope = DataScope.Select(x => x.organizeId).ToList();
        if (_repository.AsSugarClient().Queryable<ModuleEntity>().Any(x => dataScope.Contains(x.SystemId) && x.Id.Equals(moduleId))) return conModels; // 分级管理全部放开

        var roles = PermissionGroup;
        var roleAuthorizeList = _repository.AsSugarClient().Queryable<AuthorizeEntity>()
            .Where(x => roles.Contains(x.ObjectId) && x.ItemType == "resource").Select(a => new { a.ItemId, a.ObjectId }).ToList();

        if (!isDataPermissions)
        {
            conModels.Add(new ConditionalCollections()
            {
                ConditionalList = new List<KeyValuePair<WhereType, SqlSugar.ConditionalModel>>()
                {
                    primaryWhere
                }
            });
            return conModels;
        }
        else if (roleAuthorizeList.Count == 0 && isDataPermissions)
        {
            primaryWhere.Value.ConditionalType = ConditionalType.Equal;
            conModels.Add(new ConditionalCollections()
            {
                ConditionalList = new List<KeyValuePair<WhereType, SqlSugar.ConditionalModel>>()
                {
                    primaryWhere
                }
            });
            return conModels;
        }

        var resourceList = _repository.AsSugarClient().Queryable<ModuleDataAuthorizeSchemeEntity>().In(it => it.Id, roleAuthorizeList.Select(x => x.ItemId).ToList()).Where(it => it.ModuleId == moduleId && it.DeleteMark == null).ToList();

        if (resourceList.Any(x => x.AllData == 1 || x.EnCode.Equals("jnpf_alldata")))
        {
            conModels.Add(new ConditionalCollections()
            {
                ConditionalList = new List<KeyValuePair<WhereType, SqlSugar.ConditionalModel>>() {
                    primaryWhere
                }
            });
        }
        else
        {
            var allList = new List<object>(); // 构造任何层级的条件
            var resultList = new List<object>();
            foreach (var roleId in PermissionGroup)
            {
                var isCurrentRole = true;
                var roleList = new List<object>();
                foreach (var item in resourceList.Where(x => roleAuthorizeList.Where(xx => xx.ObjectId.Equals(roleId)).Select(x => x.ItemId).Contains(x.Id)).ToList())
                {
                    var conditionItemWhere = item.MatchLogic;
                    var groupsList = new List<object>();
                    foreach (var conditionItem in item.ConditionJson.ToList<AuthorizeModuleResourceConditionModel>())
                    {
                        var conditionalList = new List<object>();
                        foreach (var fieldItem in conditionItem.Groups)
                        {
                            var itemField = fieldItem.BindTable.IsNullOrWhiteSpace() ? fieldItem.Field : string.Format("{0}.{1}", fieldItem.BindTable, fieldItem.Field);
                            var itemValue = new object();
                            switch (fieldItem.Value)
                            {
                                case "@currentTime":
                                    itemValue = DateTime.Now;
                                    break;
                                case "@userId":
                                    itemValue = UserId;
                                    break;
                                case "@userAndSubordinates":
                                    itemValue = CurrentUserAndSubordinates.ToJsonStringOld();
                                    break;
                                case "@organizeId":
                                    var organizeTree = await _repository.AsSugarClient().Queryable<OrganizeEntity>()
                                        .Where(it => it.Id.Equals(User.OrganizeId))
                                        .Select(it => it.OrganizeIdTree)
                                        .FirstAsync();
                                    if (organizeTree.IsNotEmptyOrNull())
                                        itemValue = organizeTree.Split(",").ToJsonStringOld();
                                    break;
                                case "@organizationAndSuborganization":
                                    var oList = new List<List<string>>();
                                    foreach (var organizeId in CurrentOrganizationAndSubOrganizations)
                                    {
                                        var oTree = await _repository.AsSugarClient().Queryable<OrganizeEntity>()
                                            .Where(it => it.Id.Equals(organizeId))
                                            .Select(it => it.OrganizeIdTree)
                                            .FirstAsync();
                                        if (oTree.IsNotEmptyOrNull())
                                            oList.Add(oTree.Split(",").ToList());
                                    }
                                    itemValue = oList.ToJsonStringOld();
                                    break;
                                case "@branchManageOrganize":
                                    var bList = new List<List<string>>();
                                    var dataScopeList = DataScope.Select(x => x.organizeId).ToList();
                                    var orgTreeList = await _repository.AsSugarClient().Queryable<OrganizeEntity>()
                                        .Where(x => x.DeleteMark == null && x.EnabledMark == 1)
                                        .WhereIF(!IsAdministrator, x => dataScope.Contains(x.Id))
                                        .Select(x => x.OrganizeIdTree)
                                        .ToListAsync();
                                    if (orgTreeList.Count > 0)
                                    {
                                        foreach (var orgTree in orgTreeList)
                                        {
                                            var org = orgTree.Split(",").ToList();
                                            bList.Add(org);
                                        }
                                        itemValue = bList.ToJsonStringOld();
                                    }
                                    else
                                    {
                                        itemValue = "jnpfNullList";
                                    }
                                    break;
                                default:
                                    if (fieldItem.Value.IsNotEmptyOrNull() && fieldItem.Value.ToString().Contains("["))
                                        itemValue = fieldItem.Value.ToString().Replace("\r\n", "").Replace(" ", "");
                                    else
                                        itemValue = fieldItem.Value;
                                    break;
                            }
                            fieldItem.Op = ReplaceOp(fieldItem.Op);
                            var itemMethod = (QueryType)System.Enum.Parse(typeof(QueryType), fieldItem.Op);

                            var cmodel = GetConditionalModel(itemMethod, itemField, User.OrganizeId);

                            var between = new List<string>();
                            if (itemMethod.Equals(QueryType.Between))
                                between = itemValue.ToString().ToObject<List<string>>();

                            string? cSharpTypeName = null;
                            if (itemValue.IsNotEmptyOrNull())
                            {
                                switch (fieldItem.Type)
                                {
                                    case "datetime":
                                        if (itemMethod.Equals(QueryType.Between))
                                        {
                                            var startTime = between[0].TimeStampToDateTime();
                                            var endTime = between[1].TimeStampToDateTime();
                                            between[0] = startTime.ToString();
                                            between[1] = endTime.ToString();
                                        }
                                        else
                                        {
                                            if (itemValue is DateTime)
                                                itemValue = itemValue.ToString();
                                            else
                                                itemValue = itemValue.ToString().TimeStampToDateTime().ToString();
                                        }

                                        cSharpTypeName = "datetime";
                                        break;
                                }
                            }

                            switch (itemMethod)
                            {
                                case QueryType.Equal:
                                    conditionalList.Add(new { Key = conditionItem.Logic.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = itemValue, ConditionalType = ConditionalType.Equal, CSharpTypeName = cSharpTypeName } });
                                    break;
                                case QueryType.NotEqual:
                                    conditionalList.Add(new { Key = conditionItem.Logic.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = itemValue, ConditionalType = ConditionalType.NoEqual, CSharpTypeName = cSharpTypeName } });
                                    break;
                                case QueryType.Included:
                                    conditionalList.Add(new { Key = conditionItem.Logic.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = itemValue, ConditionalType = ConditionalType.Like, CSharpTypeName = cSharpTypeName } });
                                    break;
                                case QueryType.NotIncluded:
                                    conditionalList.Add(new { Key = conditionItem.Logic.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = itemValue, ConditionalType = ConditionalType.NoLike, CSharpTypeName = cSharpTypeName } });
                                    break;
                                case QueryType.GreaterThan:
                                    conditionalList.Add(new { Key = conditionItem.Logic.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = itemValue, ConditionalType = ConditionalType.GreaterThan, CSharpTypeName = cSharpTypeName } });
                                    break;
                                case QueryType.GreaterThanOrEqual:
                                    conditionalList.Add(new { Key = conditionItem.Logic.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = itemValue, ConditionalType = ConditionalType.GreaterThanOrEqual, CSharpTypeName = cSharpTypeName } });
                                    break;
                                case QueryType.LessThan:
                                    conditionalList.Add(new { Key = conditionItem.Logic.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = itemValue, ConditionalType = ConditionalType.LessThan, CSharpTypeName = cSharpTypeName } });
                                    break;
                                case QueryType.LessThanOrEqual:
                                    conditionalList.Add(new { Key = conditionItem.Logic.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = itemValue, ConditionalType = ConditionalType.LessThanOrEqual, CSharpTypeName = cSharpTypeName } });
                                    break;
                                case QueryType.Between:
                                    if (between.IsNotEmptyOrNull())
                                    {
                                        conditionalList.Add(new { Key = conditionItem.Logic.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = between[0], ConditionalType = ConditionalType.GreaterThanOrEqual, CSharpTypeName = cSharpTypeName } });
                                        conditionalList.Add(new { Key = (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = between[1], ConditionalType = ConditionalType.LessThanOrEqual, CSharpTypeName = cSharpTypeName } });
                                        continue;
                                    }
                                    break;
                                case QueryType.Null:
                                    if (fieldItem.Type.Equals("double") || fieldItem.Type.Equals("int") || fieldItem.Type.Equals("bigint"))
                                        conditionalList.Add(new { Key = conditionItem.Logic.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = itemValue, ConditionalType = ConditionalType.EqualNull, CSharpTypeName = cSharpTypeName } });
                                    else
                                        conditionalList.Add(new { Key = conditionItem.Logic.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = itemValue, ConditionalType = ConditionalType.IsNullOrEmpty, CSharpTypeName = cSharpTypeName } });
                                    break;
                                case QueryType.NotNull:
                                    conditionalList.Add(new { Key = conditionItem.Logic.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = itemValue, ConditionalType = ConditionalType.IsNot, CSharpTypeName = cSharpTypeName } });
                                    break;
                                case QueryType.In:
                                case QueryType.NotIn:
                                    if (itemValue != null && itemValue.ToString().Contains('['))
                                    {
                                        var ids = new List<string>();
                                        if (itemValue.ToString().Contains("[[")) ids = itemValue.ToString().ToObject<List<List<string>>>().Select(x => x.Last()).ToList();
                                        else ids = itemValue.ToString().ToObject<List<string>>();

                                        for (var i = 0; i < ids.Count; i++)
                                        {
                                            var it = ids[i];
                                            var conditionWhereType = WhereType.And;
                                            if (itemMethod.Equals(QueryType.In)) conditionWhereType = i.Equals(0) && conditionItem.Logic.Equals("and") ? WhereType.And : WhereType.Or;
                                            else conditionWhereType = i.Equals(0) && conditionItem.Logic.Equals("or") ? WhereType.Or : WhereType.And;

                                            conditionalList.Add(new { Key = (int)conditionWhereType, Value = new { FieldName = itemField, FieldValue = it, ConditionalType = itemMethod.Equals(QueryType.In) ? ConditionalType.Like : ConditionalType.NoLike, CSharpTypeName = cSharpTypeName } });
                                        }

                                        if (itemMethod.Equals(QueryType.NotIn))
                                        {
                                            conditionalList.Add(new { Key = (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = "null", ConditionalType = ConditionalType.IsNot, CSharpTypeName = cSharpTypeName } });
                                            conditionalList.Add(new { Key = (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = string.Empty, ConditionalType = ConditionalType.IsNot, CSharpTypeName = cSharpTypeName } });
                                        }

                                        continue;
                                    }
                                    else
                                    {
                                        conditionalList.Add(new { Key = conditionItem.Logic.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { FieldName = itemField, FieldValue = itemValue, ConditionalType = ConditionalType.Equal, CSharpTypeName = cSharpTypeName } });
                                    }
                                    break;
                            }
                        }

                        if (conditionalList.Any())
                        {
                            var firstItem = conditionalList.First().ToJsonStringOld().ToObject<dynamic>();
                            firstItem.Key = 0;
                            conditionalList[0] = firstItem;
                            groupsList.Add(new { Key = conditionItemWhere.Equals("or") ? (int)WhereType.Or : (int)WhereType.And, Value = new { ConditionalList = conditionalList } });
                        }
                    }

                    if (groupsList.Any()) roleList.Add(new { Key = isCurrentRole ? (int)WhereType.Or : (int)WhereType.And, Value = new { ConditionalList = groupsList } });
                    isCurrentRole = false;
                }

                if (roleList.Any()) allList.Add(new { Key = (int)WhereType.Or, Value = new { ConditionalList = roleList } });
            }

            if (allList.Any()) resultList.Add(new { ConditionalList = allList });

            if (resultList.Any()) conModels.AddRange(_repository.AsSugarClient().Utilities.JsonToConditionalModels(resultList.ToJsonString()));
        }

        if (resourceList.Count == 0)
        {
            primaryWhere.Value.ConditionalType = ConditionalType.Equal;
            conModels.Add(new ConditionalCollections()
            {
                ConditionalList = new List<KeyValuePair<WhereType, SqlSugar.ConditionalModel>>()
                    {
                        primaryWhere
                    }
            });
        }

        return conModels;
    }

    /// <summary>
    /// 下属机构.
    /// </summary>
    /// <param name="organizeId">机构ID.</param>
    /// <param name="isAdmin">是否管理员.</param>
    /// <returns></returns>
    private async Task<string[]> GetSubsidiaryAsync(string organizeId, bool isAdmin)
    {
        var data = await _repository.AsSugarClient().Queryable<OrganizeEntity>().Where(it => it.DeleteMark == null && it.EnabledMark.Equals(1)).ToListAsync();
        if (!isAdmin)
            data = data.TreeChildNode(organizeId, t => t.Id, t => t.ParentId);

        return data.Select(m => m.Id).ToArray();
    }

    /// <summary>
    /// 下属机构.
    /// </summary>
    /// <param name="organizeId">机构ID.</param>
    /// <returns></returns>
    private string[] GetSubsidiary(string organizeId)
    {
        var data = _repository.AsSugarClient().Queryable<OrganizeEntity>().Where(it => it.DeleteMark == null && it.EnabledMark.Equals(1)).ToList();
        data = data.TreeChildNode(organizeId, t => t.Id, t => t.ParentId);

        return data.Select(m => m.Id).ToArray();
    }

    /// <summary>
    /// 获取下属.
    /// </summary>
    /// <param name="managerId">主管Id.</param>
    /// <returns></returns>
    private async Task<string[]> GetSubordinatesAsync(string managerId)
    {
        List<string> data = new List<string>();
        var userIds = await _repository.AsQueryable().Where(m => m.ManagerId == managerId && m.DeleteMark == null).Select(m => m.Id).ToListAsync();
        data.AddRange(userIds);

        // 关闭无限级我的下属
        // data.AddRange(await GetInfiniteSubordinats(userIds.ToArray()));
        return data.ToArray();
    }

    /// <summary>
    /// 获取下属.
    /// </summary>
    /// <param name="managerId">主管Id.</param>
    /// <returns></returns>
    private string[] GetSubordinates(string managerId)
    {
        List<string> data = new List<string>();
        var userIds = _repository.AsQueryable().Where(m => m.ManagerId == managerId && m.DeleteMark == null).Select(m => m.Id).ToList();
        data.AddRange(userIds);

        // 关闭无限级我的下属
        // data.AddRange(await GetInfiniteSubordinats(userIds.ToArray()));
        return data.ToArray();
    }

    /// <summary>
    /// 获取下属无限极.
    /// </summary>
    /// <param name="parentIds"></param>
    /// <returns></returns>
    private async Task<List<string>> GetInfiniteSubordinats(string[] parentIds)
    {
        List<string> data = new List<string>();
        if (parentIds.ToList().Count > 0)
        {
            var userIds = await _repository.AsQueryable().In(it => it.ManagerId, parentIds).Where(it => it.DeleteMark == null).OrderBy(it => it.SortCode).Select(it => it.Id).ToListAsync();
            data.AddRange(userIds);
            data.AddRange(await GetInfiniteSubordinats(userIds.ToArray()));
        }

        return data;
    }

    /// <summary>
    /// 获取当前用户岗位信息.
    /// </summary>
    /// <param name="PositionIds"></param>
    /// <returns></returns>
    private async Task<List<PositionInfoModel>> GetPosition(string organizeId)
    {
        return await _repository.AsSugarClient().Queryable<PositionEntity, UserRelationEntity>((a, b) => new JoinQueryInfos(JoinType.Left, a.Id.Equals(b.ObjectId) && b.ObjectType.Equals("Position"))).Where((a, b) => a.OrganizeId.Equals(organizeId) && b.UserId.Equals(UserId)).Select(a => new PositionInfoModel { id = a.Id, name = a.FullName }).ToListAsync();
    }

    /// <summary>
    /// 获取条件模型.
    /// </summary>
    /// <returns></returns>
    private ConditionalModel GetConditionalModel(QueryType expressType, string fieldName, string fieldValue, string dataType = "string")
    {
        switch (expressType)
        {
            // 模糊
            case QueryType.Contains:
                return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.Like, FieldValue = fieldValue };

            // 等于
            case QueryType.Equal:
                switch (dataType)
                {
                    case "Double":
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.Equal, FieldValue = fieldValue, FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(double)) };
                    case "Int32":
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.Equal, FieldValue = fieldValue, FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(int)) };
                    default:
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.Equal, FieldValue = fieldValue };
                }

            // 不等于
            case QueryType.NotEqual:
                switch (dataType)
                {
                    case "Double":
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.NoEqual, FieldValue = fieldValue, FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(double)) };
                    case "Int32":
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.NoEqual, FieldValue = fieldValue, FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(int)) };
                    default:
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.NoEqual, FieldValue = fieldValue };
                }

            // 小于
            case QueryType.LessThan:
                switch (dataType)
                {
                    case "Double":
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.LessThan, FieldValue = fieldValue, FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(double)) };
                    case "Int32":
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.LessThan, FieldValue = fieldValue, FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(int)) };
                    default:
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.LessThan, FieldValue = fieldValue };
                }

            // 小于等于
            case QueryType.LessThanOrEqual:
                switch (dataType)
                {
                    case "Double":
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.LessThanOrEqual, FieldValue = fieldValue, FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(double)) };
                    case "Int32":
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.LessThanOrEqual, FieldValue = fieldValue, FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(int)) };
                    default:
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.LessThanOrEqual, FieldValue = fieldValue };
                }

            // 大于
            case QueryType.GreaterThan:
                switch (dataType)
                {
                    case "Double":
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.GreaterThan, FieldValue = fieldValue, FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(double)) };
                    case "Int32":
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.GreaterThan, FieldValue = fieldValue, FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(int)) };
                    default:
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.GreaterThan, FieldValue = fieldValue };
                }

            // 大于等于
            case QueryType.GreaterThanOrEqual:
                switch (dataType)
                {
                    case "Double":
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.GreaterThanOrEqual, FieldValue = fieldValue, FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(double)) };
                    case "Int32":
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.GreaterThanOrEqual, FieldValue = fieldValue, FieldValueConvertFunc = it => SqlSugar.UtilMethods.ChangeType2(it, typeof(int)) };
                    default:
                        return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.GreaterThanOrEqual, FieldValue = fieldValue };
                }

            // 包含
            case QueryType.In:
                return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.In, FieldValue = fieldValue };
            case QueryType.Included:
                return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.Like, FieldValue = fieldValue };
            // 不包含
            case QueryType.NotIn:
                return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.NotIn, FieldValue = fieldValue };
            case QueryType.NotIncluded:
                return new ConditionalModel() { FieldName = fieldName, ConditionalType = ConditionalType.NoLike, FieldValue = fieldValue };
        }

        return new ConditionalModel();
    }

    /// <summary>
    /// 获取角色名称 根据 角色Ids.
    /// </summary>
    /// <param name="ids"></param>
    /// <returns></returns>
    public async Task<string> GetRoleNameByIds(string ids)
    {
        if (ids.IsNullOrEmpty())
            return string.Empty;

        var idList = ids.Split(",").ToList();
        var nameList = new List<string>();
        var roleList = await _repository.AsSugarClient().Queryable<RoleEntity>().Where(x => x.DeleteMark == null && x.EnabledMark == 1).ToListAsync();
        foreach (var item in idList)
        {
            var info = roleList.Find(x => x.Id == item);
            if (info != null && info.FullName.IsNotEmptyOrNull())
            {
                nameList.Add(info.FullName);
            }
        }

        return string.Join(",", nameList);
    }

    /// <summary>
    /// 根据角色Ids和组织Id 获取组织下的角色以及全局角色.
    /// </summary>
    /// <param name="roleIds">角色Id集合.</param>
    /// <param name="organizeId">组织Id.</param>
    /// <returns></returns>
    public List<string> GetUserRoleIds(string roleIds, string organizeId)
    {
        if (roleIds.IsNotEmptyOrNull())
        {
            var userRoleIds = roleIds.Split(",");

            // 当前组织下的角色Id 集合
            var roleList = _repository.AsSugarClient().Queryable<OrganizeRelationEntity>()
                .Where(x => x.OrganizeId == organizeId && x.ObjectType == "Role" && userRoleIds.Contains(x.ObjectId)).Select(x => x.ObjectId).ToList();

            // 全局角色Id 集合
            var gRoleList = _repository.AsSugarClient().Queryable<RoleEntity>().Where(x => userRoleIds.Contains(x.Id) && x.GlobalMark == 1)
                .Where(r => r.EnabledMark == 1 && r.DeleteMark == null).Select(x => x.Id).ToList();

            roleList.AddRange(gRoleList); // 组织角色 + 全局角色

            return roleList;
        }
        else
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// 用户权限组Ids.
    /// </summary>
    /// <returns></returns>
    public List<string> GetPermissionGroupIds()
    {
        var res = GetPermissionByCurrentOrgId(UserId, User.OrganizeId);

        // 如果当前组织没有任何权限组 则切换所属组织
        if (!DataScope.Any(x => x.organizeType.IsNotEmptyOrNull()) && (!res.Any() || !_repository.AsSugarClient().Queryable<AuthorizeEntity>().Any(a => res.Contains(a.ObjectId) && a.ItemType == "system")))
        {
            var orgIds = _repository.AsSugarClient().Queryable<UserRelationEntity>()
                .Where(x => x.UserId.Equals(UserId) && x.ObjectType.Equals("Organize") && !x.ObjectId.Equals(User.OrganizeId)).Select(x => x.ObjectId).ToList();
            if (orgIds != null && orgIds.Any())
            {
                foreach (var item in orgIds)
                {
                    res = GetPermissionByCurrentOrgId(UserId, item);
                    if (res.Any() && _repository.AsSugarClient().Queryable<AuthorizeEntity>().Any(a => res.Contains(a.ObjectId) && a.ItemType == "system"))
                    {
                        _repository.AsSugarClient().Updateable<UserEntity>().SetColumns(x => x.OrganizeId == item).Where(x => x.Id.Equals(UserId)).ExecuteCommand();

                        // 获取切换组织 Id 下的所有岗位
                        var pList = _repository.AsSugarClient().Queryable<PositionEntity>().Where(x => x.OrganizeId == item).Select(x => x.Id).ToList();

                        // 获取切换组织的 岗位，如果该组织没有岗位则为空
                        var idList = _repository.AsSugarClient().Queryable<UserRelationEntity>()
                            .Where(x => x.UserId == UserId && pList.Contains(x.ObjectId) && x.ObjectType == "Position").Select(x => x.ObjectId).ToList();
                        User.PositionId = idList.FirstOrDefault() == null ? string.Empty : idList.FirstOrDefault();
                        break;
                    }
                }
            }
        }

        return res;
    }
    public List<string> GetPermissionByCurrentOrgId(string userId, string orgId)
    {
        // 当前用户所属组织下的 部门、角色、岗位
        var orgIdList = new List<string>() { orgId };
        var posIdList = _repository.AsSugarClient().Queryable<PositionEntity>().Where(x => orgIdList.Contains(x.OrganizeId) && x.DeleteMark == null).Select(x => x.Id).ToList();
        var roleIdList = _repository.AsSugarClient().Queryable<OrganizeRelationEntity>().Where(x => orgIdList.Contains(x.OrganizeId) && x.ObjectType.Equals("Role")).Select(x => x.ObjectId).ToList();
        var groupIdList = _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.UserId.Equals(userId) && x.ObjectType.Equals("Group")).Select(x => x.ObjectId).ToList();
        orgIdList.AddRange(posIdList);
        orgIdList.AddRange(roleIdList);
        orgIdList.AddRange(groupIdList);
        var objIdList = _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => orgIdList.Contains(x.ObjectId) && x.UserId.Equals(userId)).Select(x => x.ObjectId).ToList();
        var roleGMIds = _repository.AsSugarClient().Queryable<UserRelationEntity, RoleEntity>((u, r) => new JoinQueryInfos(JoinType.Left, u.ObjectId == r.Id && r.DeleteMark == null))
            .Where((u, r) => u.UserId.Equals(userId) && u.ObjectType.Equals("Role") && r.GlobalMark.Equals(1)).Select((u, r) => u.ObjectId).ToList();
        objIdList.AddRange(roleGMIds);
        objIdList.Add(userId);

        // 查询业务平台权限
        var querList = LinqExpression.Or<PermissionGroupEntity>();
        objIdList.ForEach(item => querList = querList.Or(x => x.PermissionMember.Contains(item)));
        querList = querList.Or(x => x.Type.Equals(0));
        return _repository.AsSugarClient().Queryable<PermissionGroupEntity>().Where(x => x.DeleteMark == null && x.EnabledMark.Equals(1)).Where(querList).Select(x => x.Id).ToList();
    }

    /// <summary>
    /// 普通身份切到有权限的所属组织.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public async Task SetUserOrganizeByPermission(string userId)
    {
        var userOrgList = await _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.ObjectType.Equals("Organize") && x.UserId.Equals(userId)).Select(x => x.ObjectId).ToListAsync();

        if (!GetPermissionByCurrentOrgId(userId, User.OrganizeId).Any())
        {
            foreach (var orgId in userOrgList)
            {
                if (GetPermissionByCurrentOrgId(userId, orgId).Any())
                {
                    // 获取切换组织 Id 下的所有岗位
                    List<string>? pList = await _repository.AsSugarClient().Queryable<PositionEntity>().Where(x => x.OrganizeId == orgId).Select(x => x.Id).ToListAsync();

                    // 获取切换组织的 岗位，如果该组织没有岗位则为空
                    var pid = await _repository.AsSugarClient().Queryable<UserRelationEntity>()
                        .Where(x => x.UserId == userId && pList.Contains(x.ObjectId) && x.ObjectType == "Position").Select(x => x.ObjectId).FirstAsync();
                    var positionId = pid.IsNullOrEmpty() ? string.Empty : pid;

                    await _repository.AsSugarClient().Updateable<UserEntity>().SetColumns(x => new UserEntity() { OrganizeId = orgId, PositionId = positionId }).Where(x => x.Id.Equals(userId)).ExecuteCommandAsync();
                    User.OrganizeId = orgId;
                    break;
                }
            }
        }

    }

    /// <summary>
    /// 获取当前用户所有 权限组.
    /// </summary>
    /// <param name="userId"></param>
    /// <returns></returns>
    public List<string> GetPermissionByUserId(string userId)
    {
        // 当前用户所属组织下的 部门、角色、岗位
        var objIdList = _repository.AsSugarClient().Queryable<UserRelationEntity>().Where(x => x.UserId.Equals(userId)).Select(x => x.ObjectId).ToList();
        objIdList.Add(userId);

        // 查询业务平台权限
        var querList = LinqExpression.Or<PermissionGroupEntity>();
        objIdList.ForEach(item => querList = querList.Or(x => x.PermissionMember.Contains(item)));
        querList = querList.Or(x => x.Type.Equals(0));
        return _repository.AsSugarClient().Queryable<PermissionGroupEntity>().Where(x => x.DeleteMark == null && x.EnabledMark.Equals(1)).Where(querList).Select(x => x.Id).ToList();
    }

    /// <summary>
    /// 会否存在用户缓存.
    /// </summary>
    /// <param name="cacheKey"></param>
    /// <returns></returns>
    private async Task<bool> ExistsUserInfo(string cacheKey)
    {
        return await _cacheManager.ExistsAsync(cacheKey);
    }

    /// <summary>
    /// 保存用户登录信息.
    /// </summary>
    /// <param name="cacheKey">key.</param>
    /// <param name="userInfo">用户信息.</param>
    /// <param name="timeSpan">过期时间.</param>
    /// <returns></returns>
    private async Task<bool> SetUserInfo(string cacheKey, UserInfoModel userInfo, TimeSpan timeSpan)
    {
        return await _cacheManager.SetAsync(cacheKey, userInfo, timeSpan);
    }

    /// <summary>
    /// 获取全局租户缓存.
    /// </summary>
    /// <returns></returns>
    private GlobalTenantCacheModel GetGlobalTenantCache(string tenantId)
    {
        string cacheKey = string.Format("{0}", CommonConst.GLOBALTENANT);
        return _cacheManager.Get<List<GlobalTenantCacheModel>>(cacheKey).Find(it => it.TenantId.Equals(tenantId));
    }

    /// <summary>
    /// 获取用户登录信息.
    /// </summary>
    /// <param name="cacheKey">key.</param>
    /// <returns></returns>
    private async Task<UserInfoModel> GetUserInfo(string cacheKey)
    {
        return (await _cacheManager.GetAsync(cacheKey)).Adapt<UserInfoModel>();
    }

    /// <summary>
    /// 获取用户名称.
    /// </summary>
    /// <param name="userId">用户id.</param>
    /// <param name="isAccount">是否带账号.</param>
    /// <returns></returns>
    public string GetUserName(string userId, bool isAccount = true)
    {
        UserEntity? entity = _repository.GetFirst(x => x.Id == userId && x.DeleteMark == null);
        if (entity.IsNullOrEmpty()) return string.Empty;
        return isAccount ? entity.RealName + "/" + entity.Account : entity.RealName;
    }

    /// <summary>
    /// 获取用户名称.
    /// </summary>
    /// <param name="userId">用户id.</param>
    /// <param name="isAccount">是否带账号.</param>
    /// <returns></returns>
    public async Task<string> GetUserNameAsync(string userId, bool isAccount = true)
    {
        UserEntity? entity = await _repository.GetFirstAsync(x => x.Id == userId && x.DeleteMark == null);
        if (entity.IsNullOrEmpty()) return string.Empty;
        return isAccount ? entity.RealName + "/" + entity.Account : entity.RealName;
    }

    /// <summary>
    /// 获取管理员用户id.
    /// </summary>
    public string GetAdminUserId()
    {
        var user = _repository.AsSugarClient().Queryable<UserEntity>().First(x => x.Account == "admin" && x.DeleteMark == null);
        if (user.IsNotEmptyOrNull()) return user.Id;
        return string.Empty;
    }

    /// <summary>
    /// 获取全局租户缓存.
    /// </summary>
    /// <returns></returns>
    public List<GlobalTenantCacheModel> GetGlobalTenantCache()
    {
        string cacheKey = string.Format("{0}", CommonConst.GLOBALTENANT);
        var list = _cacheManager.Get<List<GlobalTenantCacheModel>>(cacheKey);
        return list != null ? list : new List<GlobalTenantCacheModel>();
    }

    /// <summary>
    /// 转换条件符号.
    /// </summary>
    public string ReplaceOp(string op)
    {
        switch (op)
        {
            case "==":
                op = "Equal";
                break;
            case "between":
                op = "Between";
                break;
            case ">":
                op = "GreaterThan";
                break;
            case "<":
                op = "LessThan";
                break;
            case "<>":
                op = "NotEqual";
                break;
            case ">=":
                op = "GreaterThanOrEqual";
                break;
            case "<=":
                op = "LessThanOrEqual";
                break;
            case "like":
                op = "Included";
                break;
            case "notLike":
                op = "NotIncluded";
                break;
            case "in":
                op = "In";
                break;
            case "notIn":
                op = "NotIn";
                break;
            case "null":
                op = "Null";
                break;
            case "notNull":
                op = "NotNull";
                break;
        }

        return op;
    }
}