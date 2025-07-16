using JNPF.Common.Core.Manager;
using JNPF.Common.Enums;
using JNPF.Common.Extension;
using JNPF.Common.Filter;
using JNPF.Common.Models.WorkFlow;
using JNPF.DependencyInjection;
using JNPF.DynamicApiController;
using JNPF.FriendlyException;
using JNPF.VisualDev.Interfaces;
using JNPF.WorkFlow.Entitys.Dto.Operator;
using JNPF.WorkFlow.Entitys.Dto.Task;
using JNPF.WorkFlow.Entitys.Enum;
using JNPF.WorkFlow.Entitys.Model;
using JNPF.WorkFlow.Entitys.Model.WorkFlow;
using JNPF.WorkFlow.Interfaces.Manager;
using JNPF.WorkFlow.Interfaces.Repository;
using JNPF.WorkFlow.Interfaces.Service;
using Mapster;
using Microsoft.AspNetCore.Mvc;

namespace JNPF.WorkFlow.Service;

/// <summary>
/// 流程任务.
/// </summary>
[ApiDescriptionSettings(Tag = "WorkFlowTask", Name = "Task", Order = 306)]
[Route("api/workflow/[controller]")]
public class TaskService : ITaskService, IDynamicApiController, ITransient
{
    private readonly IWorkFlowRepository _repository;
    private readonly IWorkFlowManager _workFlowManager;
    private readonly IUserManager _userManager;
    private readonly IRunService _runService;

    public TaskService(IWorkFlowRepository repository, IWorkFlowManager workFlowManager, IUserManager userManager, IRunService runService)
    {
        _repository = repository;
        _workFlowManager = workFlowManager;
        _userManager = userManager;
        _runService = runService;
    }

    #region Get

    /// <summary>
    /// 获取任务详情.
    /// </summary>
    /// <param name="taskId">任务id.</param>
    /// <param name="flowId">流程id.</param>
    /// <param name="opType">操作类型.</param>
    /// <param name="operatorId">操作id.</param>
    /// <returns></returns>
    [HttpGet("{taskId}")]
    [UnifySerializerSetting("special")]
    public async Task<dynamic> GetInfo(string taskId, [FromQuery] string flowId, [FromQuery] string opType, [FromQuery] string type, [FromQuery] string operatorId)
    {
        try
        {
            if (type.IsNotEmptyOrNull() && type == "2")
            {
                return await _workFlowManager.GetTriggerTaskInfo(taskId);
            }
            else
            {
                return await _workFlowManager.GetTaskInfo(taskId, flowId, opType, operatorId);
            }
        }
        catch (AppFriendlyException ex)
        {
            throw Oops.Oh(ex.ErrorCode);
        }
    }

    /// <summary>
    /// 子流程详情.
    /// </summary>
    /// <param name="taskId">任务id.</param>
    /// <param name="nodeCode">节点编码.</param>
    /// <returns></returns>
    [HttpGet("SubFlowInfo")]
    public async Task<dynamic> SubFlowInfo(string taskId, string nodeCode)
    {
        var output = new List<object>();
        var subFlowTaskList = _repository.GetTaskList(x => x.ParentId == taskId && x.SubCode == nodeCode);
        foreach (var item in subFlowTaskList)
        {
            var subFlowTaskInfo = await _workFlowManager.GetTaskInfo(item.Id, item.FlowId, "0", null);
            output.Add(subFlowTaskInfo);
        }
        return output;
    }

    /// <summary>
    /// 发起列表.
    /// </summary>
    /// <param name="input">请求参数.</param>
    /// <returns></returns>
    [HttpGet("")]
    public dynamic GetLaunchList([FromQuery] TaskListQuery input)
    {
        return _repository.GetLaunchList(input);
    }

    /// <summary>
    /// 任务相关人员列表.
    /// </summary>
    /// <param name="taskId"></param>
    /// <returns></returns>
    [HttpGet("TaskUserList/{taskId}")]
    public dynamic GetTaskUserList(string taskId, [FromQuery] PageInputBase pageInputBase)
    {
        WorkFlowHandleModel handleModel = pageInputBase.Adapt<WorkFlowHandleModel>();
        return _workFlowManager.GetUserIdList(taskId, handleModel, 1);
    }

    /// <summary>
    ///  查看发起表单.
    /// </summary>
    /// <param name="taskId"></param>
    /// <returns></returns>
    [HttpGet("ViewStartForm/{taskId}")]
    public async Task<dynamic> ViewStartForm(string taskId)
    {
        var output = new TaskInfoOutput();
        var wfParamter = _repository.GetWorkFlowParamterByTaskId(taskId, null);
        output.formInfo = _repository.GetFromEntity(wfParamter.startPro.formId).Adapt<FormModel>();
        output.formData = await _runService.GetOrDelFlowFormData(wfParamter.node.formId, taskId, 0, wfParamter.flowInfo.flowId);
        return output;
    }
    #endregion

    #region Post

    /// <summary>
    /// 新建.
    /// </summary>
    /// <param name="flowTaskSubmit">请求参数.</param>
    /// <returns></returns>
    [HttpPost("")]
    public async Task<dynamic> Create([FromBody] FlowTaskSubmitModel flowTaskSubmit)
    {
        try
        {
            var output = new OperatorOutput();
            var delegateUserList = new List<string>();
            var templateId = string.Empty;
            var flowInfo = _repository.GetFlowInfo(flowTaskSubmit.flowId);
            if (flowInfo.IsNullOrEmpty() || flowInfo.flowId.IsNullOrEmpty())
            {
                var template = _repository.GetTemplate(flowTaskSubmit.flowId, false);
                if (template.IsNotEmptyOrNull() && template.FlowId.IsNotEmptyOrNull())
                {
                    flowTaskSubmit.flowId = template.FlowId;
                    templateId = template.Id;
                }
                else
                {
                    throw Oops.Oh(ErrorCode.WF0033);
                }
            }
            else
            {
                templateId = flowInfo.templateId;
            }
            if (flowTaskSubmit.delegateUser.IsNotEmptyOrNull()) // 是否委托发起.
            {
                flowTaskSubmit.isDelegate = true;
                delegateUserList = flowTaskSubmit.delegateUser.Split(",").ToList();
            }
            else
            {
                delegateUserList.Add(_userManager.UserId);
            }

            foreach (var item in delegateUserList)
            {
                flowTaskSubmit.crUser = item;
                if (flowTaskSubmit.status == 0)
                {
                    await _workFlowManager.Save(flowTaskSubmit);
                }
                else
                {
                    output = await _workFlowManager.Submit(flowTaskSubmit);
                }
            }
            return output;
        }
        catch (AppFriendlyException ex)
        {
            throw Oops.Oh(ex.ErrorCode, ex.Args);
        }
    }

    /// <summary>
    /// 更新.
    /// </summary>
    /// <param name="id">主键值.</param>
    /// <param name="flowTaskSubmit">请求参数.</param>
    /// <returns></returns>
    [HttpPut("{id}")]
    public async Task<dynamic> Update(string id, [FromBody] FlowTaskSubmitModel flowTaskSubmit)
    {
        try
        {
            var flowInfo = _repository.GetFlowInfo(flowTaskSubmit.flowId);
            if (flowInfo.IsNullOrEmpty() || flowInfo.flowId.IsNullOrEmpty())
            {
                var template = _repository.GetTemplate(flowTaskSubmit.flowId);
                if (template.IsNotEmptyOrNull() && template.FlowId.IsNotEmptyOrNull()) { flowTaskSubmit.flowId = template.FlowId; } else { throw Oops.Oh(ErrorCode.WF0033); }
            }
            if (flowTaskSubmit.status == 0)
            {
                await _workFlowManager.Save(flowTaskSubmit);
                return new List<CandidateModel>();
            }
            else
            {
                return await _workFlowManager.Submit(flowTaskSubmit);
            }
        }
        catch (AppFriendlyException ex)
        {
            throw Oops.Oh(ex.ErrorCode, ex.Args);
        }
    }

    /// <summary>
    /// 删除.
    /// </summary>
    /// <param name="id">主键值.</param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    public async Task Delete(string id)
    {
        var entity = _repository.GetTaskInfo(id);
        if (entity.IsNotEmptyOrNull())
        {
            if (!entity.ParentId.Equals("0") && entity.ParentId.IsNotEmptyOrNull())
                throw Oops.Oh(ErrorCode.WF0003, entity.FullName);
            if (entity.Status == WorkFlowTaskStatusEnum.Pause.ParseToInt()) throw Oops.Oh(ErrorCode.WF0046);
            if (!(entity.Status == WorkFlowTaskStatusEnum.Draft.ParseToInt() || entity.Status == WorkFlowTaskStatusEnum.Recall.ParseToInt()))
                throw Oops.Oh(ErrorCode.WF0024);
            await _repository.DeleteTask(entity);
        }
        var launchFormId = _repository.GetNodeInfo(x => x.NodeType == WorkFlowNodeTypeEnum.start.ParseToString() && x.FlowId == entity.FlowId).FormId;
        await _runService.GetOrDelFlowFormData(launchFormId, id, 1, entity.FlowId);
    }

    /// <summary>
    /// 撤回(发起).
    /// </summary>
    /// <param name="id">主键值.</param>
    /// <param name="input">流程经办.</param>
    /// <returns></returns>
    [HttpPut("Recall/{taskId}")]
    public async Task Recall(string taskId, [FromBody] WorkFlowHandleModel handleModel)
    {
        var wfParamter = _repository.GetWorkFlowParamterByTaskId(taskId, handleModel);
        await _workFlowManager.RecallLaunch(wfParamter);
    }

    /// <summary>
    /// 催办.
    /// </summary>
    /// <param name="taskId"></param>
    /// <returns></returns>
    [HttpPost("Press/{taskId}")]
    public async Task Press(string taskId)
    {
        var wfParamter = _repository.GetWorkFlowParamterByTaskId(taskId, null);
        if (wfParamter.taskEntity.Status == WorkFlowTaskStatusEnum.Pause.ParseToInt()) throw Oops.Oh(ErrorCode.WF0046);
        if (wfParamter.flowInfo.status == 3) throw Oops.Oh(ErrorCode.WF0070);
        await _workFlowManager.Press(wfParamter);
    }

    /// <summary>
    /// 撤销.
    /// </summary>
    /// <param name="id">主键值.</param>
    /// <param name="input">流程经办.</param>
    /// <returns></returns>
    [HttpPut("Revoke/{taskId}")]
    public async Task Revoke(string taskId, [FromBody] WorkFlowHandleModel handleModel)
    {
        var wfParamter = _repository.GetWorkFlowParamterByTaskId(taskId, handleModel);
        if (wfParamter.taskEntity.Status == WorkFlowTaskStatusEnum.Pause.ParseToInt()) throw Oops.Oh(ErrorCode.WF0046);
        if (wfParamter.flowInfo.status == 3) throw Oops.Oh(ErrorCode.WF0070);
        await _workFlowManager.Revoke(wfParamter);
    }

    #endregion
}
