using JNPF.Common.Const;
using JNPF.Common.Core.Manager;
using JNPF.Common.Enums;
using JNPF.Common.Extension;
using JNPF.Common.Manager;
using JNPF.DependencyInjection;
using JNPF.DynamicApiController;
using JNPF.FriendlyException;
using JNPF.WorkFlow.Entitys.Dto.Task;
using JNPF.WorkFlow.Entitys.Entity;
using JNPF.WorkFlow.Entitys.Enum;
using JNPF.WorkFlow.Entitys.Model;
using JNPF.WorkFlow.Interfaces.Manager;
using JNPF.WorkFlow.Interfaces.Repository;
using Microsoft.AspNetCore.Mvc;
using NPOI.SS.Formula.Functions;
using SqlSugar;

namespace JNPF.WorkFlow.Service;

/// <summary>
/// 流程监控.
/// </summary>
[ApiDescriptionSettings(Tag = "WorkFlowMonitor", Name = "Monitor", Order = 304)]
[Route("api/workflow/[controller]")]
public class MonitorService : IDynamicApiController, ITransient
{
    private readonly IWorkFlowRepository _repository;
    private readonly IWorkFlowManager _workFlowManager;
    private readonly ICacheManager _cacheManager;
    private readonly IUserManager _userManager;

    public MonitorService(IWorkFlowRepository repository, IWorkFlowManager workFlowManager, ICacheManager cacheManager, IUserManager userManager)
    {
        _repository = repository;
        _workFlowManager = workFlowManager;
        _cacheManager = cacheManager;
        _userManager = userManager;
    }

    /// <summary>
    /// 列表.
    /// </summary>
    /// <param name="input">请求参数.</param>
    /// <returns></returns>
    [HttpGet("")]
    public dynamic GetList([FromQuery] TaskListQuery input)
    {
        return _repository.GetMonitorList(input);
    }

    /// <summary>
    /// 任务是否存在异步子流程.
    /// </summary>
    /// <param name="taskId">任务id.</param>
    /// <returns></returns>
    [HttpGet("AnySubFlow/{taskId}")]
    public async Task<dynamic> AnySubFlow(string taskId)
    {
        return _repository.AnyTask(x => x.ParentId == taskId && x.IsAsync == 1 && x.DeleteMark == null);
    }

    /// <summary>
    /// 批量删除.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    [HttpDelete]
    public async Task Delete([FromBody] TaskBatchInput input)
    {
        var tsakList = _repository.GetTaskList(x => input.ids.Contains(x.Id) && !x.ParentId.Equals("0") && !SqlFunc.IsNullOrEmpty(x.ParentId));
        if (tsakList.Any()) throw Oops.Oh(ErrorCode.WF0003, tsakList.FirstOrDefault().FullName);
        tsakList = _repository.GetTaskList(x => input.ids.Contains(x.Id) && x.Status == WorkFlowTaskStatusEnum.Pause.ParseToInt());
        if (tsakList.Any()) throw Oops.Oh(ErrorCode.WF0047, tsakList.FirstOrDefault().FullName);
        tsakList = _repository.GetTaskList(x => input.ids.Contains(x.ParentId) && x.DeleteMark == null && x.Status == WorkFlowTaskStatusEnum.Pause.ParseToInt());
        if (tsakList.Any()) throw Oops.Oh(ErrorCode.WF0047, tsakList.FirstOrDefault().FullName);
        tsakList = _repository.GetTaskList(x => input.ids.Contains(x.Id) && x.DeleteMark == null);
        var childTaskList = new List<WorkFlowTaskEntity>();
        foreach (var item in tsakList)
        {
            if (!_repository.GetOrgAdminAuthorize(item.CreatorUserId, 1)) throw Oops.Oh(ErrorCode.WF0055, item.FullName);
            childTaskList = childTaskList.Union(_repository.GetChildTaskList(item.Id, true)).ToList();
        }
        tsakList = tsakList.Union(childTaskList).ToList();
        await _workFlowManager.DeleteTask(tsakList);
    }

    /// <summary>
    /// 终止.
    /// </summary>
    /// <param name="taskId">任务id.</param>
    /// <param name="handleModel">审批参数.</param>
    /// <returns></returns>
    [HttpPost("Cancel/{taskId}")]
    public async Task Cancel(string taskId, [FromBody] WorkFlowHandleModel handleModel)
    {
        if (_repository.AnyTask(x => x.Id == taskId && x.DeleteMark == null))
        {
            var wfParamter = _repository.GetWorkFlowParamterByTaskId(taskId, handleModel);
            await _workFlowManager.Cancel(wfParamter);
        }
        else
        {
            var triggerTask = _repository.GetTriggerTaskList(x => x.Id == taskId && x.DeleteMark == null).FirstOrDefault();
            if (triggerTask != null)
            {
                triggerTask.Status = WorkFlowTaskStatusEnum.Cancel.ParseToInt();
                _cacheManager.Set("TriggerTask", triggerTask.Id);
                var cacheKey = string.Format("{0}:{1}", "TriggerTask", _userManager.TenantId);
                List<string> caCheList = await _cacheManager.GetAsync<List<string>>(cacheKey);
                caCheList ??= new List<string>();
                caCheList?.Add(triggerTask.Id);
                await _cacheManager.SetAsync(cacheKey, caCheList);
                _repository.UpdateTriggerTask(triggerTask);
            }
        }
    }

    /// <summary>
    /// 指派.
    /// </summary>
    /// <param name="taskId">任务id.</param>
    /// <param name="handleModel">审批参数.</param>
    /// <returns></returns>
    [HttpPost("Assign/{taskId}")]
    public async Task Assigned(string taskId, [FromBody] WorkFlowHandleModel handleModel)
    {
        var wfParamter = _repository.GetWorkFlowParamterByTaskId(taskId, handleModel);
        await _workFlowManager.Assigned(wfParamter);
    }

    /// <summary>
    /// 复活.
    /// </summary>
    /// <param name="handleModel">审批参数.</param>
    /// <returns></returns>
    [HttpPost("Activate/{taskId}")]
    public async Task Activate(string taskId, [FromBody] WorkFlowHandleModel handleModel)
    {
        // 清除依次经办数据
        var wfParamter = _repository.GetWorkFlowParamterByTaskId(taskId, handleModel);
        if (wfParamter.taskEntity.Status == WorkFlowTaskStatusEnum.Pause.ParseToInt()) throw Oops.Oh(ErrorCode.WF0046);
        if (wfParamter.flowInfo.status != 1) throw Oops.Oh(ErrorCode.WF0070);
        await _workFlowManager.Activate(wfParamter);
    }

    /// <summary>
    /// 暂停.
    /// </summary>
    /// <param name="taskId">任务id.</param>
    /// <param name="handleModel">审批参数.</param>
    /// <returns></returns>
    [HttpPost("Pause/{taskId}")]
    public async Task Pause(string taskId, [FromBody] WorkFlowHandleModel handleModel)
    {
        var wfParamter = _repository.GetWorkFlowParamterByTaskId(taskId, handleModel);
        await _workFlowManager.Pause(wfParamter);
    }

    /// <summary>
    /// 恢复.
    /// </summary>
    /// <param name="taskId">任务id.</param>
    /// <param name="handleModel">审批参数.</param>
    /// <returns></returns>
    [HttpPost("Reboot/{taskId}")]
    public async Task Reboot(string taskId, [FromBody] WorkFlowHandleModel handleModel)
    {
        var wfParamter = _repository.GetWorkFlowParamterByTaskId(taskId, handleModel);
        await _workFlowManager.Reboot(wfParamter);
    }
}
