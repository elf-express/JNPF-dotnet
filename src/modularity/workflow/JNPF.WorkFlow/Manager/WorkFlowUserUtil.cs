﻿using Aop.Api.Domain;
using JNPF.Common.Core.Manager;
using JNPF.Common.Dtos.Datainterface;
using JNPF.Common.Enums;
using JNPF.Common.Extension;
using JNPF.Common.Filter;
using JNPF.Common.Manager;
using JNPF.Common.Models.WorkFlow;
using JNPF.Common.Security;
using JNPF.FriendlyException;
using JNPF.Systems.Entitys.Dto.User;
using JNPF.Systems.Entitys.Permission;
using JNPF.Systems.Interfaces.Permission;
using JNPF.Systems.Interfaces.System;
using JNPF.WorkFlow.Entitys.Entity;
using JNPF.WorkFlow.Entitys.Enum;
using JNPF.WorkFlow.Entitys.Model;
using JNPF.WorkFlow.Entitys.Model.Conifg;
using JNPF.WorkFlow.Entitys.Model.Properties;
using JNPF.WorkFlow.Entitys.Model.WorkFlow;
using JNPF.WorkFlow.Factory;
using JNPF.WorkFlow.Interfaces.Repository;
using Mapster;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json.Linq;
using SqlSugar;

namespace JNPF.WorkFlow.Manager;

public class WorkFlowUserUtil
{
    private readonly IWorkFlowRepository _repository;
    private readonly IUsersService _usersService;
    private readonly IOrganizeService _organizeService;
    private readonly IDepartmentService _departmentService;
    private readonly IUserRelationService _userRelationService;
    private readonly IDataInterfaceService _dataInterfaceService;
    private readonly IUserManager _userManager;

    public WorkFlowUserUtil(IWorkFlowRepository repository, IUsersService usersService, IOrganizeService organizeService, IDepartmentService departmentService, IUserRelationService userRelationService, IDataInterfaceService dataInterfaceService, IUserManager userManager)
    {
        _repository = repository;
        _usersService = usersService;
        _organizeService = organizeService;
        _departmentService = departmentService;
        _userRelationService = userRelationService;
        _dataInterfaceService = dataInterfaceService;
        _userManager = userManager;
    }

    /// <summary>
    /// 候选人员列表.
    /// </summary>
    /// <param name="nextNode">下一节点.</param>
    /// <param name="flowHandleModel">审批参数.</param>
    /// <param name="hasCandidates">是否存在候选人.</param>
    /// <returns></returns>
    public dynamic GetCandidateItems(List<string> userIds, WorkFlowHandleModel flowHandleModel, string type = "", bool hasCandidates = true)
    {
        var input = new UserConditionInput()
        {
            userIds = userIds,
            type = type,
            pagination = flowHandleModel,
            userId = flowHandleModel.delegateUser
        };
        return _userRelationService.GetUserPage(input, ref hasCandidates);
    }

    /// <summary>
    /// 获取候选人节点信息.
    /// </summary>
    /// <param name="candidateModels"></param>
    /// <param name="nextNodeList"></param>
    /// <param name="nodeList"></param>
    /// <returns></returns>
    public async Task GetCandidates(List<CandidateModel> candidateModels, List<WorkFlowNodeModel> nextNodeList)
    {
        foreach (var item in nextNodeList)
        {
            if (item.nodePro.IsNotEmptyOrNull() && (item.nodeType == WorkFlowNodeTypeEnum.subFlow.ToString() || item.nodeType == WorkFlowNodeTypeEnum.approver.ToString() || item.nodeType == WorkFlowNodeTypeEnum.processing.ToString()))
            {
                var candidateItem = new CandidateModel();
                candidateItem.nodeCode = item.nodeCode;
                candidateItem.nodeName = item.nodePro.nodeName;
                candidateItem.isCandidates = item.nodePro.assigneeType == 7;
                var flag = false;//是否有数据
                var input = new UserConditionInput()
                {
                    userIds = item.nodePro.approvers,
                    pagination = new PageInputBase()
                };
                _userRelationService.GetUserPage(input, ref flag);
                candidateItem.hasCandidates = flag;
                candidateModels.Add(candidateItem);
            }
        }
    }

    public async Task AddOperatorByType(WorkFlowParamter wfParamter, WorkFlowNodeModel nextNode)
    {
        try
        {
            if (WorkFlowNodeTypeEnum.approver.ToString().Equals(nextNode.nodeType)|| WorkFlowNodeTypeEnum.processing.ToString().Equals(nextNode.nodeType))
            {
                var errorUserId = new List<string>();
                if (wfParamter.errorRuleUserList.IsNotEmptyOrNull() && wfParamter.errorRuleUserList.ContainsKey(nextNode.nodeCode))
                {
                    errorUserId = wfParamter.errorRuleUserList[nextNode.nodeCode];
                }
                var handleIds = await GetFlowUserId(wfParamter, nextNode);
                if (handleIds.Count == 0)
                {
                    switch (wfParamter.globalPro.errorRule)
                    {
                        case 1:
                            handleIds.Add(_userManager.GetAdminUserId());
                            break;
                        case 2:
                            if ((await _usersService.GetUserListByExp(x => wfParamter.globalPro.errorRuleUser.Contains(x.Id) && x.DeleteMark == null && x.EnabledMark == 1)).Any())
                            {
                                handleIds = wfParamter.globalPro.errorRuleUser;
                            }
                            else
                            {
                                handleIds.Add(_userManager.GetAdminUserId());
                            }
                            break;
                        case 3:
                            if (errorUserId.IsNotEmptyOrNull() && errorUserId.Count > 0)
                            {
                                handleIds = errorUserId;
                            }
                            else
                            {
                                if (!wfParamter.errorNodeList.Select(x => x.nodeCode).Contains(nextNode.nodeCode))
                                {
                                    wfParamter.errorNodeList.Add(nextNode.Adapt<CandidateModel>());
                                }
                            }
                            break;
                        case 4:
                            // 异常节点下一节点是否存在候选人节点.
                            var errorNextNodeList = (await BpmnEngineFactory.CreateBmpnEngine().GetNextNode(string.Empty, string.Empty, nextNode.nodeId)).Select(x => x.id).ToList();
                            var flag = wfParamter.nodeList.
                                Any(x => errorNextNodeList.Contains(x.nodeCode)
                                && WorkFlowNodeTypeEnum.approver.ToString().Equals(x.nodeType)
                                && x.nodePro.assigneeType == 7);
                            if (flag)
                            {
                                handleIds.Add(_userManager.GetAdminUserId());
                            }
                            else
                            {
                                handleIds.Add("jnpf");
                            }
                            break;
                        case 5:
                            throw Oops.Oh(ErrorCode.WF0035);
                    }
                }
                if (nextNode.nodePro.counterSign == 2)
                {
                    wfParamter.nextOperatorEntityList.Add(new WorkFlowOperatorEntity
                    {
                        Id = SnowflakeIdHelper.NextId(),
                        NodeCode = nextNode.nodeCode,
                        NodeName = nextNode.nodePro.nodeName,
                        NodeId = nextNode.nodeId,
                        HandleId = handleIds.FirstOrDefault(),
                        HandleAll = string.Join(",", handleIds),
                        EngineType = wfParamter.taskEntity.EngineType,
                        TaskId = wfParamter.taskEntity.Id,
                        Status = WorkFlowOperatorStatusEnum.Runing.ParseToInt(),
                        SignTime = wfParamter.globalPro.hasSignFor ? null : DateTime.Now,
                        StartHandleTime = _userManager.CommonModuleEnCodeList.Contains("workFlow.flowTodo") ? null : DateTime.Now,
                        CreatorTime = DateTime.Now,
                        Completion = 0,
                        IsProcessing = nextNode.nodeType == WorkFlowNodeTypeEnum.processing.ToString() ? 1 : 0,
                    });
                }
                else
                {
                    foreach (var item in handleIds)
                    {
                        WorkFlowOperatorEntity operatorEntity = new WorkFlowOperatorEntity();
                        operatorEntity.Id = SnowflakeIdHelper.NextId();
                        operatorEntity.NodeCode = nextNode.nodeCode;
                        operatorEntity.NodeName = nextNode.nodePro.nodeName;
                        operatorEntity.NodeId = nextNode.nodeId;
                        operatorEntity.TaskId = wfParamter.taskEntity.Id;
                        operatorEntity.Status = WorkFlowOperatorStatusEnum.Runing.ParseToInt();
                        operatorEntity.SignTime = wfParamter.globalPro.hasSignFor ? null : DateTime.Now;
                        operatorEntity.EngineType = wfParamter.taskEntity.EngineType;
                        operatorEntity.HandleId = item;
                        operatorEntity.CreatorTime = DateTime.Now;
                        operatorEntity.StartHandleTime = _userManager.CommonModuleEnCodeList.Contains("workFlow.flowTodo") ? null : DateTime.Now;
                        operatorEntity.Completion = 0;
                        operatorEntity.IsProcessing = nextNode.nodeType == WorkFlowNodeTypeEnum.processing.ToString() ? 1 : 0;
                        wfParamter.nextOperatorEntityList.Add(operatorEntity);
                    }
                }
            }
        }
        catch (AppFriendlyException ex)
        {
            throw Oops.Oh(ex.ErrorCode);
        }
    }

    /// <summary>
    /// 获取节点审批人员id.
    /// </summary>
    /// <param name="wfParamter">当前任务参数.</param>
    /// <param name="nodePro">节点属性.</param>
    /// <param name="flowTaskNodeEntity">节点实体.</param>
    /// <returns></returns>
    public async Task<List<string>> GetFlowUserId(WorkFlowParamter wfParamter, WorkFlowNodeModel node)
    {
        var userIdList = new List<string>();
        // 获取全部用户id
        var userList1 = await _usersService.GetUserListByExp(x => x.DeleteMark == null && x.EnabledMark != 0, u => new UserEntity() { Id = u.Id });
        // 发起者本人.
        var userEntity = _usersService.GetInfoByUserId(wfParamter.taskEntity.CreatorUserId);
        switch (node.nodePro.assigneeType)
        {
            // 发起者主管
            case (int)WorkFlowOperatorTypeEnum.LaunchCharge:
                if (node.nodePro.approverType == 2)
                {
                    var operatorList = wfParamter.operatorEntityList.FindAll(x => x.ParentId == "0" && x.HandleId != "jnpf" && x.Status >= 0 && x.HandleStatus != null && x.HandleTime != null).Select(x => x.HandleId).ToList();
                    if (wfParamter.operatorEntity.IsNotEmptyOrNull() && wfParamter.operatorEntity.HandleId != "jnpf")
                        operatorList.Add(wfParamter.operatorEntity.HandleId);
                    if (operatorList.Any())
                    {
                        var managerIdList = (await _usersService.GetUserListByExp(x => x.DeleteMark == null && operatorList.Contains(x.Id))).Select(x => x.ManagerId).ToList();
                        foreach (var item in managerIdList)
                        {
                            var crDirector = await GetManagerByLevel(item, node.nodePro.managerLevel);
                            if (crDirector.IsNotEmptyOrNull())
                                userIdList.Add(crDirector);
                        }
                    }
                }
                else
                {
                    var crDirector = await GetManagerByLevel(userEntity.ManagerId, node.nodePro.managerLevel);
                    if (crDirector.IsNotEmptyOrNull())
                        userIdList.Add(crDirector);
                }
                break;

            // 发起者本人
            case (int)WorkFlowOperatorTypeEnum.InitiatorMe:
                userIdList.Add(userEntity.Id);
                break;

            // 部门主管
            case (int)WorkFlowOperatorTypeEnum.DepartmentCharge:
                if (node.nodePro.managerApproverType == 2)
                {
                    var operatorList = wfParamter.operatorEntityList.FindAll(x => x.ParentId == "0" && x.HandleId != "jnpf" && x.Status >= 0 && x.HandleStatus != null && x.HandleTime != null).Select(x => x.HandleId).ToList();
                    if (wfParamter.operatorEntity.IsNotEmptyOrNull() && wfParamter.operatorEntity.HandleId != "jnpf")
                        operatorList.Add(wfParamter.operatorEntity.HandleId);
                    if (operatorList.Any())
                    {
                        foreach (var item in operatorList)
                        {
                            if (item.IsNullOrEmpty()) continue;
                            var launchUserEntity = _usersService.GetInfoByUserId(item);
                            var organizeEntity = await _organizeService.GetInfoById(launchUserEntity.OrganizeId);
                            if (organizeEntity.IsNotEmptyOrNull() && organizeEntity.OrganizeIdTree.IsNotEmptyOrNull())
                            {
                                var orgTree = organizeEntity.OrganizeIdTree.Split(",").Reverse().ToList();
                                if (orgTree.Count >= node.nodePro.departmentLevel)
                                {
                                    var orgId = orgTree[node.nodePro.departmentLevel - 1];
                                    var organize = await _organizeService.GetInfoById(orgId);
                                    if (organize.IsNotEmptyOrNull() && organize.ManagerId.IsNotEmptyOrNull())
                                    {
                                        userIdList.Add(organize.ManagerId);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    var launchUserEntity = _repository.GetLaunchUserInfo(wfParamter.taskEntity.Id);
                    var organizeEntity = await _organizeService.GetInfoById(launchUserEntity.OrganizeId);
                    if (organizeEntity.IsNotEmptyOrNull() && organizeEntity.OrganizeIdTree.IsNotEmptyOrNull())
                    {
                        var orgTree = organizeEntity.OrganizeIdTree.Split(",").Reverse().ToList();
                        if (orgTree.Count >= node.nodePro.departmentLevel)
                        {
                            var orgId = orgTree[node.nodePro.departmentLevel - 1];
                            var organize = await _organizeService.GetInfoById(orgId);
                            if (organize.IsNotEmptyOrNull() && organize.ManagerId.IsNotEmptyOrNull())
                            {
                                userIdList.Add(organize.ManagerId);
                            }
                        }
                    }
                }
                break;

            // 表单变量
            case (int)WorkFlowOperatorTypeEnum.VariableApprover:
                try
                {
                    var jd = wfParamter.formData.ToObject<JObject>();
                    var fieldValueList = new List<string>();
                    var formField = node.nodePro.formField;
                    if (node.nodePro.assignList.Any() && wfParamter.node.IsNotEmptyOrNull())
                    {
                        var ruleList = node.nodePro.assignList.Find(x => x.nodeId == wfParamter.node.nodeCode)?.ruleList;
                        if (ruleList.IsNotEmptyOrNull() && ruleList.Any(x => x.childField == node.nodePro.formField))
                        {
                            formField = ruleList.Find(x => x.childField == node.nodePro.formField)?.parentField;
                        }
                    }
                    if (jd.ContainsKey(formField))
                    {
                        if (jd[formField] is JArray)
                        {
                            fieldValueList = jd[formField].ToObject<List<string>>();
                        }
                        else if (jd[formField].ToString().Contains("["))
                        {
                            fieldValueList = jd[formField].ToString().ToObject<List<string>>();
                        }
                        else
                        {
                            if (jd[formField].ToString().IsNotEmptyOrNull())
                            {
                                fieldValueList = jd[formField].ToString().Split(",").ToList();
                            }
                        }
                    }
                    else
                    {
                        var fields = formField.Split("-").ToList();
                        // 子表键值
                        var tableField = fields[0];
                        // 子表字段键值
                        var keyField = fields[1];
                        if (jd.ContainsKey(tableField) && jd[tableField] is JArray)
                        {
                            var jar = jd[tableField] as JArray;

                            fieldValueList = jar.Where(x => x.ToObject<JObject>().ContainsKey(keyField)).Select(x => x.ToObject<JObject>()[keyField].ToString()).ToList();
                        }
                    }
                    userIdList = _userRelationService.GetUserId(fieldValueList, string.Empty);
                }
                catch (Exception)
                {

                    break;
                }
                break;

            // 环节(提交时下个节点是环节就跳过，审批则看环节节点是否是当前节点的上级)
            case (int)WorkFlowOperatorTypeEnum.LinkApprover:
                // 环节节点所有经办人(过滤掉加签人)
                userIdList = _repository.GetOperatorList(x =>
                x.TaskId == wfParamter.taskEntity.Id && x.NodeCode.Equals(node.nodePro.approverNodeId)
                && (x.HandleStatus == 0 || x.HandleStatus == 1) && x.ParentId == "0"
                && x.Status != WorkFlowOperatorStatusEnum.Invalid.ParseToInt() && x.HandleId != "jnpf").Select(x => x.HandleId).Distinct().ToList();
                break;

            // 接口(接口结构为{"code":200,"data":{"handleId":"admin"},"msg":""})
            case (int)WorkFlowOperatorTypeEnum.ServiceApprover:
                try
                {
                    if (node.nodePro.interfaceConfig.IsNotEmptyOrNull() && node.nodePro.interfaceConfig.interfaceId.IsNotEmptyOrNull())
                    {
                        var parameters = new Dictionary<string, object>();
                        parameters.Add("@taskId", wfParamter.taskEntity.Id);
                        parameters.Add("@taskNodeId", wfParamter.node.nodeId);
                        parameters.Add("@taskFullName", wfParamter.taskEntity.FullName);
                        parameters.Add("@launchUserId", wfParamter.taskEntity.CreatorUserId);
                        parameters.Add("@launchUserName", _usersService.GetInfoByUserId(wfParamter.taskEntity.CreatorUserId).RealName);
                        parameters.Add("@flowId", wfParamter.taskEntity.FlowId);
                        parameters.Add("@flowFullName", wfParamter.taskEntity.FlowName);
                        var input = new DataInterfacePreviewInput
                        {
                            paramList = node.nodePro.interfaceConfig.templateJson,
                            tenantId = _userManager.TenantId,
                            sourceData = wfParamter.formData,
                            systemParamter = parameters,
                        };
                        var reuslt = await _dataInterfaceService.GetResponseByType(node.nodePro.interfaceConfig.interfaceId, 3, input);
                        if (reuslt.IsNotEmptyOrNull())
                        {
                            var handleIdList = new List<string>();
                            if (reuslt.ToJsonString().FirstOrDefault().Equals('['))
                            {
                                var reusltList = reuslt.ToObject<List<Dictionary<string, object>>>();
                                handleIdList = reusltList.Where(x => x.ContainsKey("handleId")).Select(x => x["handleId"].ToString()).ToList();
                            }
                            else
                            {
                                var jobj = reuslt.ToObject<JObject>();
                                if (jobj.ContainsKey("handleId"))
                                {
                                    handleIdList = jobj["handleId"].ToString().Split(",").ToList();
                                }
                            }
                            var userList2 = await _usersService.GetUserListByExp(x => x.DeleteMark == null, u => new UserEntity() { Id = u.Id });
                            // 利用list交集方法过滤非用户数据
                            userIdList = handleIdList.Intersect(userList2.Select(x => x.Id)).ToList();
                        }
                    }
                }
                catch (AppFriendlyException)
                {
                    break;
                }

                break;

            // 候选人
            case (int)WorkFlowOperatorTypeEnum.CandidateApprover:
                userIdList = _repository.GetCandidates(node.nodeCode, wfParamter.taskEntity.Id);
                break;
            default:
                userIdList = GetUserDefined(node.nodePro);
                userIdList = await GetExtraRuleUsers(userIdList, node.nodePro.extraRule, wfParamter.taskEntity.Id);
                break;
        }
        userIdList = userIdList.Intersect(userList1.Select(x => x.Id)).ToList();// 过滤掉作废人员和非用户人员
        if (userIdList.Count == 0)
        {
            userIdList = _repository.GetCandidates(node.nodeCode, wfParamter.taskEntity.Id);
        }
        return userIdList.Distinct().ToList();

    }

    /// <summary>
    /// 获取级别主管.
    /// </summary>
    /// <param name="managerId">主管id.</param>
    /// <param name="level">级别.</param>
    /// <returns></returns>
    public async Task<string> GetManagerByLevel(string managerId, int level)
    {
        --level;
        if (level == 0)
        {
            return managerId;
        }
        else
        {
            var manager = await _usersService.GetInfoByUserIdAsync(managerId);
            return manager.IsNullOrEmpty() ? string.Empty : await GetManagerByLevel(manager.ManagerId, level);
        }
    }

    /// <summary>
    /// 递归获取加签人.
    /// </summary>
    /// <param name="id">经办id.</param>
    /// <param name="flowTaskOperatorEntities">所有经办.</param>
    /// <returns></returns>
    public async Task<List<WorkFlowOperatorEntity>> GetOperator(string id, List<WorkFlowOperatorEntity> operatorList)
    {
        var childEntity = _repository.GetOperatorInfo(x => x.ParentId == id && x.Status != -1);
        if (childEntity.IsNotEmptyOrNull())
        {
            childEntity.Status = -1;
            operatorList.Add(childEntity);
            return await GetOperator(childEntity.Id, operatorList);
        }
        else
        {
            return operatorList;
        }
    }

    /// <summary>
    /// 获取自定义人员.
    /// </summary>
    /// <param name="approversProperties">节点属性.</param>
    /// <param name="userType">0：审批人员，1：抄送人员.</param>
    /// <returns></returns>
    public List<string> GetUserDefined(NodeProperties nodePro, int userType = 0)
    {
        var userIdList = userType == 0 ? nodePro.approvers : nodePro.circulateUser;
        // 依次审批人员
        if (nodePro.counterSign == 2 && userType == 0 && nodePro.approversSortList.Any())
        {
            userIdList = nodePro.approversSortList;
        }
        userIdList = _userRelationService.GetUserId(userIdList);
        return userIdList;
    }

    /// <summary>
    /// 附加条件过滤.
    /// </summary>
    /// <param name="userList">过滤用户.</param>
    /// <param name="extraRule">过滤规则.</param>
    /// <param name="taskId">任务id.</param>
    /// <returns></returns>
    private async Task<List<string>> GetExtraRuleUsers(List<string> userList, string extraRule, string taskId)
    {
        var launchUserEntity = _repository.GetLaunchUserInfo(taskId);
        if (launchUserEntity.IsNullOrEmpty())
        {
            var subordinate = (await _usersService.GetUserListByExp(u => u.EnabledMark == 1 && u.DeleteMark == null && u.ManagerId == _userManager.UserId)).Select(u => u.Id).ToList().ToJsonString();
            launchUserEntity = new WorkFlowLaunchUserEntity()
            {
                OrganizeId = _userManager.User.OrganizeId,
                PositionId = _userManager.User.PositionId,
                ManagerId = _userManager.User.ManagerId,
                Subordinate = subordinate
            };
        }
        switch (extraRule)
        {
            case "2":
                var orgEntity = await _organizeService.GetInfoById(launchUserEntity.OrganizeId);
                if (orgEntity.IsNotEmptyOrNull() && orgEntity.Category == "department")
                {
                    userList = _userRelationService.GetUserId("Organize", launchUserEntity.OrganizeId).Intersect(userList).ToList();
                }
                else
                {
                    userList = new List<string>();
                }
                break;
            case "3":
                userList = _userRelationService.GetUserId("Position", launchUserEntity.PositionId).Intersect(userList).ToList();
                break;
            case "4":
                userList = new List<string> { launchUserEntity.ManagerId }.Intersect(userList).ToList();
                break;
            case "5":
                userList = launchUserEntity.Subordinate.Split(",").ToList().Intersect(userList).ToList();
                break;
            case "6":
                // 直属公司id
                var companyId = _departmentService.GetCompanyId(launchUserEntity.OrganizeId);
                var objIdList = _departmentService.GetCompanyAllDep(companyId, true).Select(x => x.Id).Distinct().ToList();
                objIdList.Add(companyId);
                userList = _userRelationService.GetUserId(objIdList, "Organize").Intersect(userList).ToList();
                break;
        }
        return userList;
    }

    /// <summary>
    /// 获取抄送人.
    /// </summary>
    /// <param name="wfParamter">任务参数.</param>
    /// <param name="handleStatus">审批类型（0：拒绝，1：同意）.</param>
    public async Task GetflowTaskCirculateEntityList(WorkFlowParamter wfParamter, int handleStatus)
    {
        var circulateUserList = wfParamter.copyIds.IsNotEmptyOrNull() ? wfParamter.copyIds.Split(",").ToList() : new List<string>();
        #region 抄送人
        if (handleStatus == 1)
        {
            var userList = GetUserDefined(wfParamter.nodePro, 1);
            userList = await GetExtraRuleUsers(userList, wfParamter.nodePro.extraCopyRule, wfParamter.taskEntity.Id);
            circulateUserList = circulateUserList.Union(userList).ToList();
            if (wfParamter.nodePro.isInitiatorCopy)
            {
                circulateUserList.Add(wfParamter.taskEntity.CreatorUserId);
            }
            if (wfParamter.nodePro.isFormFieldCopy)
            {
                var jd = wfParamter.formData.ToObject<JObject>();
                var fieldValueList = new List<string>();
                var copyFormField = wfParamter.nodePro.copyFormField;
                if (jd.ContainsKey(copyFormField))
                {
                    if (jd[copyFormField] is JArray)
                    {
                        fieldValueList = jd[copyFormField].ToObject<List<string>>();
                    }
                    else
                    {
                        if (jd[copyFormField].ToString().IsNotEmptyOrNull())
                        {
                            fieldValueList = jd[copyFormField].ToString().Split(",").ToList();
                        }
                    }
                }
                var userIdCopyList = _userRelationService.GetUserId(fieldValueList, string.Empty);
                circulateUserList = circulateUserList.Union(userIdCopyList).ToList();
            }
        }
        foreach (var item in circulateUserList.Distinct())
        {
            wfParamter.circulateEntityList.Add(new WorkFlowCirculateEntity()
            {
                Id = SnowflakeIdHelper.NextId(),
                UserId = item,
                NodeCode = wfParamter.node.nodeCode,
                NodeName = wfParamter.nodePro.nodeName,
                NodeId = wfParamter.operatorEntity.NodeId,
                TaskId = wfParamter.operatorEntity.TaskId,
                OperatorId = wfParamter.operatorEntity.Id,
                CreatorTime = DateTime.Now,
                Read = 0
            });
        }
        #endregion
    }

    public async Task<string> GetApproverUserName(WorkFlowNodeModel node, WorkFlowParamter wfParamter)
    {
        var userNameList = new List<string>();
        var userName = await _usersService.GetUserName(wfParamter.taskEntity.CreatorUserId);
        if (node.nodeType.Equals(WorkFlowNodeTypeEnum.start.ToString()))
        {
            userNameList.Add(userName);
        }
        else if (node.nodeType.Equals(WorkFlowNodeTypeEnum.subFlow.ToString()))
        {
            var userIdList = _repository.GetTaskList(x => x.ParentId == wfParamter.taskEntity.Id && x.SubCode == node.nodeCode && x.DeleteMark == null).Select(x => x.CreatorUserId).Distinct().ToList();
            if (!userIdList.Any())
            {
                userIdList = await GetFlowUserId(wfParamter, node);
            }
            await GetUserNameDefined(node.nodePro, userNameList, userIdList);
        }
        else
        {
            if (wfParamter.isRevoke)
            {
                var operatorList = _repository.GetOperatorList(x => x.TaskId == wfParamter.revokeEntity.TaskId && x.NodeCode == node.nodeCode && x.HandleStatus == 1 && x.Completion == 1 && x.Status != WorkFlowOperatorStatusEnum.Invalid.ParseToInt());
                var userIdList = operatorList.Select(x => x.HandleId).Distinct().ToList();
                await GetUserNameDefined(node.nodePro, userNameList, userIdList);
            }
            else
            {
                var operatorList = _repository.GetOperatorList(x => x.TaskId == wfParamter.taskEntity.Id && x.NodeCode == node.nodeCode && x.ParentId == "0" && x.Status != WorkFlowOperatorStatusEnum.Invalid.ParseToInt());
                var userIdList = operatorList.Select(x => x.HandleId).Distinct().ToList();
                if (node.nodePro.counterSign == 2 && operatorList.FirstOrDefault().IsNotEmptyOrNull() && operatorList.FirstOrDefault().HandleAll.IsNotEmptyOrNull())
                {
                    userIdList.AddRange(operatorList.FirstOrDefault().HandleAll.Split(",").ToList<string>());
                }
                if (!userIdList.Any())
                {
                    userIdList = await GetFlowUserId(wfParamter, node);
                }
                await GetUserNameDefined(node.nodePro, userNameList, userIdList);
            }
        }
        return string.Join(",", userNameList.Distinct());
    }

    /// <summary>
    /// 获取自定义人员名称.
    /// </summary>
    /// <param name="approversProperties">节点属性.</param>
    /// <param name="userNameList">用户名称容器.</param>
    /// <param name="userIdList">用户id容器.</param>
    /// <returns></returns>
    public async Task GetUserNameDefined(NodeProperties approversProperties, List<string> userNameList, List<string> userIdList = null)
    {
        if (userIdList == null)
        {
            userIdList = GetUserDefined(approversProperties).Distinct().ToList();
        }
        foreach (var item in userIdList)
        {
            var name = await _usersService.GetUserName(item);
            if (name.IsNotEmptyOrNull())
                userNameList.Add(name);
        }
    }

    /// <summary>
    /// 获取下一节点异常节点.
    /// </summary>
    /// <param name="wfParamter"></param>
    /// <param name="nextNodeList"></param>
    /// <returns></returns>
    public async Task GetErrorNode(WorkFlowParamter wfParamter, List<WorkFlowNodeModel> nextNodeList)
    {
        foreach (var item in nextNodeList)
        {
            if (WorkFlowNodeTypeEnum.approver.ToString().Equals(item.nodeType)|| WorkFlowNodeTypeEnum.processing.ToString().Equals(item.nodeType))
            {
                var list = await GetFlowUserId(wfParamter, item);
                if (list.Count == 0)
                {
                    if (wfParamter.globalPro.errorRule == 3 && !wfParamter.errorNodeList.Select(x => x.nodeCode).Contains(item.nodeCode))
                    {
                        var candidateItem = item.Adapt<CandidateModel>();
                        wfParamter.errorNodeList.Add(candidateItem);
                    }
                    if (wfParamter.globalPro.errorRule == 5)
                        throw Oops.Oh(ErrorCode.WF0035);
                }
            }
        }
    }

    /// <summary>
    /// 发起按钮权限控制.
    /// </summary>
    /// <param name="wfParamter"></param>
    /// <returns></returns>
    public BtnProperties GetLaunchBtn(WorkFlowParamter wfParamter)
    {
        var btnPro = new BtnProperties();
        // 撤回/退回只能遵循发起时选择的按钮再次提交
        if (wfParamter.taskEntity.IsNotEmptyOrNull() && (wfParamter.taskEntity.Status == WorkFlowTaskStatusEnum.SendBack.ParseToInt() || wfParamter.taskEntity.Status == WorkFlowTaskStatusEnum.Recall.ParseToInt()))
        {
            btnPro.hasSaveBtn = true;
            if (wfParamter.taskEntity.DelegateUserId.IsNotEmptyOrNull())
            {
                btnPro.hasDelegateSubmitBtn = true;
            }
            else
            {
                btnPro.hasSubmitBtn = true;
            }
            return btnPro;
        }

        if (_userManager.Standing != 3)
        {
            btnPro.hasSaveBtn = true;
            btnPro.hasSubmitBtn = true;
        }
        else
        {
            var flowIds_myself = _repository.GetFlowIdList(_userManager.UserId); // 自己可发起
            if (flowIds_myself.Contains(wfParamter.flowInfo.templateId))
            {
                btnPro.hasSaveBtn = true;
                btnPro.hasSubmitBtn = true;
            }
        }
        btnPro.hasDelegateSubmitBtn = GetDelageteBtn(wfParamter.flowInfo);
        if (wfParamter.taskEntity.IsNotEmptyOrNull() && wfParamter.taskEntity.ParentId != "0")
        {
            btnPro.hasDelegateSubmitBtn = false;
        }
        return btnPro;
    }

    /// <summary>
    /// 获取委托发起按钮.
    /// </summary>
    /// <param name="flowInfo"></param>
    /// <returns></returns>
    private bool GetDelageteBtn(FlowModel flowInfo)
    {
        // 选择流程
        var delegateList = _repository.GetDelegateList(x => x.Type == 0 && !SqlFunc.IsNullOrEmpty(x.FlowId) && x.FlowId.Contains(flowInfo.templateId) && x.EndTime > DateTime.Now && x.StartTime < DateTime.Now && x.DeleteMark == null);
        // 全部流程
        var delegateListAll = _repository.GetDelegateList(x => x.Type == 0 && x.FlowName == "全部流程" && x.EndTime > DateTime.Now && x.StartTime < DateTime.Now && x.DeleteMark == null);
        if (flowInfo.visibleType == 2)
        {
            // 当前流程可发起人员
            var objlist = _repository.GetObjIdList(flowInfo.templateId);
            var userIdList = _userRelationService.GetUserId(objlist);
            delegateList = delegateList.FindAll(x => userIdList.Contains(x.UserId));
            delegateListAll = delegateListAll.FindAll(x => userIdList.Contains(x.UserId));
        }
        var delegateIds = delegateList.Union(delegateListAll).Select(x => x.Id).Distinct().ToList();
        // 委托/代理给当前用户的数据
        var delegateInfos = _repository.GetDelegateInfoList(x => x.ToUserId == _userManager.UserId && x.Status == 1 && delegateIds.Contains(x.DelegateId));
        return delegateInfos.Any();
    }

    /// <summary>
    /// 验证发起人是否有发起流程权限.
    /// </summary>
    /// <param name="flowTaskSubmitModel"></param>
    /// <returns></returns>
    public async Task GetLaunchAuthorize(FlowTaskSubmitModel flowTaskSubmitModel)
    {
        var crUser = flowTaskSubmitModel.crUser.IsEmpty() ? _userManager.UserId : flowTaskSubmitModel.crUser;
        var userEntity = await _usersService.GetUserByExp(x => x.Id == crUser && x.DeleteMark == null && x.EnabledMark == 1);
        if (userEntity.IsNullOrEmpty()) throw Oops.Oh(ErrorCode.WF0052);
        var isNormal = !"app".Equals(_userManager.UserOrigin) ? userEntity.Standing == 3 : userEntity.AppStanding == 3; // 普通用户
        if (isNormal)
        {
            var flowInfo = _repository.GetFlowInfo(flowTaskSubmitModel.flowId);
            if (flowInfo == null) throw Oops.Oh(ErrorCode.WF0052);
            if (flowInfo.visibleType == 2)
            {
                var ObjIdList = _repository.GetObjIdList(flowInfo.id);
                var userIds = _userRelationService.GetUserId(ObjIdList);
                if (!userIds.Contains(crUser))
                {
                    throw Oops.Oh(ErrorCode.WF0052);
                }
            }
        }
    }

    public async Task<string> GetOverTimeUserId(TimeOutConfig timeOutConfig, WorkFlowParamter wfParamter, string handleId)
    {
        var userIds = new List<string>();
        if (timeOutConfig.overTimeType == 6)
        {
            userIds = _userRelationService.GetUserId(new List<string> { timeOutConfig.reApprovers });
        }
        else if (timeOutConfig.overTimeType == 9)
        {
            try
            {
                if (timeOutConfig.interfaceId.IsNotEmptyOrNull())
                {
                    var parameters = new Dictionary<string, object>();
                    parameters.Add("@taskId", wfParamter.taskEntity.Id);
                    parameters.Add("@taskNodeId", wfParamter.node.nodeId);
                    parameters.Add("@taskFullName", wfParamter.taskEntity.FullName);
                    parameters.Add("@launchUserId", wfParamter.taskEntity.CreatorUserId);
                    parameters.Add("@launchUserName", _usersService.GetInfoByUserId(wfParamter.taskEntity.CreatorUserId).RealName);
                    parameters.Add("@flowId", wfParamter.taskEntity.FlowId);
                    parameters.Add("@flowFullName", wfParamter.taskEntity.FlowName);
                    var input = new DataInterfacePreviewInput
                    {
                        paramList = timeOutConfig.templateJson,
                        tenantId = _userManager.TenantId,
                        sourceData = wfParamter.formData,
                        systemParamter = parameters,
                    };
                    var reuslt = await _dataInterfaceService.GetResponseByType(timeOutConfig.interfaceId, 3, input);
                    if (reuslt.IsNotEmptyOrNull())
                    {
                        var handleIdList = new List<string>();
                        if (reuslt.ToJsonString().FirstOrDefault().Equals('['))
                        {
                            var reusltList = reuslt.ToObject<List<Dictionary<string, object>>>();
                            handleIdList = reusltList.Where(x => x.ContainsKey("handleId")).Select(x => x["handleId"].ToString()).ToList();
                        }
                        else
                        {
                            var jobj = reuslt.ToObject<JObject>();
                            if (jobj.ContainsKey("handleId"))
                            {
                                handleIdList = jobj["handleId"].ToString().Split(",").ToList();
                            }
                        }
                        var userList2 = await _usersService.GetUserListByExp(x => x.DeleteMark == null, u => new UserEntity() { Id = u.Id });
                        // 利用list交集方法过滤非用户数据
                        userIds = handleIdList.Intersect(userList2.Select(x => x.Id)).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
        else if (timeOutConfig.overTimeType == 11)
        {
            var input = new UserConditionInput()
            {
                type = timeOutConfig.overTimeExtraRule,
                pagination = new PageInputBase(),
                userId = handleId
            };
            var data = _userRelationService.GetUserList(input);
            var ids = data.Select(x => x.id).ToList();
            ids.Remove(handleId);
            return ids.FirstOrDefault();
        }
        if (userIds.Any())
        {
            return userIds.FirstOrDefault();
        }  
        else
            return string.Empty;
    }
}
