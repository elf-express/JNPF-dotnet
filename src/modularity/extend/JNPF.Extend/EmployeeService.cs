﻿using JNPF.Common.Configuration;
using JNPF.Common.Core.Manager;
using JNPF.Common.Core.Manager.Files;
using JNPF.Common.Enums;
using JNPF.Common.Extension;
using JNPF.Common.Filter;
using JNPF.Common.Helper;
using JNPF.Common.Manager;
using JNPF.Common.Models.NPOI;
using JNPF.Common.Security;
using JNPF.DataEncryption;
using JNPF.DependencyInjection;
using JNPF.DynamicApiController;
using JNPF.Extend.Entitys;
using JNPF.Extend.Entitys.Dto.Employee;
using JNPF.FriendlyException;
using JNPF.LinqBuilder;
using Mapster;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SqlSugar;

namespace JNPF.Extend;

/// <summary>
/// 职员管理（导入导出）
/// 版 本：V3.2
/// 版 权：引迈信息技术有限公司（https://www.jnpfsoft.com）
/// 作 者：JNPF开发平台组
/// 日 期：2021-06-01 .
/// </summary>
[ApiDescriptionSettings(Tag = "Extend", Name = "Employee", Order = 600)]
[Route("api/extend/[controller]")]
public class EmployeeService : IDynamicApiController, ITransient
{
    private readonly ISqlSugarRepository<EmployeeEntity> _repository;
    private readonly IFileManager _fileManager;
    private readonly IUserManager _userManager;
    private readonly ICacheManager _cacheManager;

    public EmployeeService(ISqlSugarRepository<EmployeeEntity> repository, IFileManager fileManager, IUserManager userManager, ICacheManager cacheManager)
    {
        _repository = repository;
        _fileManager = fileManager;
        _userManager = userManager;
        _cacheManager = cacheManager;
    }

    #region GET

    /// <summary>
    /// 列表.
    /// </summary>
    /// <param name="input">请求参数.</param>
    /// <returns></returns>
    [HttpGet("")]
    public async Task<dynamic> GetList([FromQuery] EmployeeListQuery input)
    {
        var whereLambda = LinqExpression.And<EmployeeEntity>();
        whereLambda = whereLambda.And(x => x.DeleteMark == null);
        if (input.condition.IsNotEmptyOrNull() && input.keyword.IsNotEmptyOrNull())
        {
            string propertyName = input.condition;
            string propertyValue = input.keyword;
            switch (propertyName)
            {
                case "EnCode": // 工号
                    whereLambda = whereLambda.And(t => t.EnCode.Contains(propertyValue));
                    break;
                case "FullName": // 姓名
                    whereLambda = whereLambda.And(t => t.FullName.Contains(propertyValue));
                    break;
                case "Telephone": // 电话
                    whereLambda = whereLambda.And(t => t.Telephone.Contains(propertyValue));
                    break;
                case "DepartmentName": // 部门
                    whereLambda = whereLambda.And(t => t.DepartmentName.Contains(propertyValue));
                    break;
                case "PositionName": // 职位
                    whereLambda = whereLambda.And(t => t.PositionName.Contains(propertyValue));
                    break;
            }
        }

        var list = await _repository.AsQueryable().Where(whereLambda)
            .OrderBy(x => x.SortCode).OrderBy(x => x.CreatorTime, OrderByType.Desc)
            .OrderByIF(!string.IsNullOrEmpty(input.keyword), t => t.LastModifyTime, OrderByType.Desc).ToPagedListAsync(input.currentPage, input.pageSize);
        var pageList = new SqlSugarPagedList<EmployeeListOutput>()
        {
            list = list.list.Adapt<List<EmployeeListOutput>>(),
            pagination = list.pagination
        };
        return PageResult<EmployeeListOutput>.SqlSugarPageResult(pageList);
    }

    /// <summary>
    /// 信息
    /// </summary>
    /// <param name="id">主键值</param>
    /// <returns></returns>
    [HttpGet("{id}")]
    public async Task<dynamic> GetInfo(string id)
    {
        return await _repository.GetFirstAsync(x => x.Id == id && x.DeleteMark == null);
    }

    /// <summary>
    /// 导入预览.
    /// </summary>
    /// <returns></returns>
    [HttpGet("ImportPreview")]
    public async Task<dynamic> ImportPreview(string fileName)
    {
        try
        {
            var filePath = FileVariable.TemporaryFilePath;
            var savePath = Path.Combine(filePath, fileName);
            //得到数据
            var sr = await _fileManager.GetFileStream(savePath);
            var excelData = ExcelImportHelper.ToDataTable(savePath, sr);
            excelData.Rows.RemoveAt(0);
            foreach (var item in excelData.Columns)
            {
                excelData.Columns[item.ToString()].ColumnName = GetFiledEncode(item.ToString());
            }
            //删除文件
            _fileManager.DeleteFile(savePath);
            //返回结果
            return new { dataRow = excelData };
        }
        catch (Exception)
        {

            throw Oops.Oh(ErrorCode.D3006);
        }
    }
    #endregion

    #region POST

    /// <summary>
    /// 新建.
    /// </summary>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    [HttpPost("")]
    public async Task Create(EmployeeEntity entity)
    {
        var isOk = await _repository.AsSugarClient().Insertable(entity).IgnoreColumns(ignoreNullColumn: true).CallEntityMethod(m => m.Creator()).ExecuteCommandAsync();
        if (isOk < 1)
            throw Oops.Oh(ErrorCode.COM1000);
    }

    /// <summary>
    /// 更新.
    /// </summary>
    /// <param name="id">主键值</param>
    /// <param name="entity">实体对象</param>
    /// <returns></returns>
    [HttpPut("{id}")]
    public async Task Update(string id, EmployeeEntity entity)
    {
        var isOk = await _repository.AsSugarClient().Updateable(entity).IgnoreColumns(ignoreAllNullColumns: true).CallEntityMethod(m => m.LastModify()).ExecuteCommandHasChangeAsync();
        if (!isOk)
            throw Oops.Oh(ErrorCode.COM1001);
    }

    /// <summary>
    /// 删除
    /// </summary>
    /// <param name="id">主键值</param>
    /// <returns></returns>
    [HttpDelete("{id}")]
    public async Task Delete(string id)
    {
        var entity = await _repository.GetFirstAsync(x => x.Id == id && x.DeleteMark == null);
        var isOk = await _repository.AsSugarClient().Updateable(entity).CallEntityMethod(m => m.Delete()).UpdateColumns(it => new { it.DeleteMark, it.DeleteTime, it.DeleteUserId }).ExecuteCommandHasChangeAsync();
        if (!isOk)
            throw Oops.Oh(ErrorCode.COM1002);
    }

    /// <summary>
    /// 导出.
    /// </summary>
    [HttpGet("ExportData")]
    public async Task<dynamic> ExportExcelData([FromQuery] EmployeeListQuery input)
    {
        var dataList = new List<EmployeeEntity>();
        if (input.dataType == "0")
        {
            dataList = await GetPageListData(input);
        }
        else
        {
            dataList = await GetListData();
        }
        ExcelConfig excelconfig = new ExcelConfig();
        excelconfig.FileName = "职员信息.xls";
        excelconfig.HeadFont = "微软雅黑";
        excelconfig.HeadPoint = 10;
        excelconfig.IsAllSizeColumn = true;
        excelconfig.ColumnModel = new List<ExcelColumnModel>();
        var filedList = input.selectKey.Split(",");
        excelconfig.ColumnModel = input.selectKey.Split(",").Select(item => new ExcelColumnModel() { Column = item.ToUpperCase(), ExcelColumn = GetFiledName(item) }).ToList();
        var addPath = Path.Combine(FileVariable.TemporaryFilePath, excelconfig.FileName);
        var stream = ExcelExportHelper<EmployeeEntity>.ExportMemoryStream(dataList, excelconfig);
        await _fileManager.UploadFileByType(stream, FileVariable.TemporaryFilePath, excelconfig.FileName);
        _cacheManager.Set(excelconfig.FileName, string.Empty);
        return new { name = excelconfig.FileName, url = "/api/file/Download?encryption=" + DESEncryption.Encrypt(_userManager.UserId + "|" + excelconfig.FileName + "|" + addPath, "JNPF") };
    }

    /// <summary>
    /// 上传文件.
    /// </summary>
    /// <returns></returns>
    [HttpPost("Uploader")]
    public async Task<dynamic> Uploader(IFormFile file)
    {
        var _filePath = _fileManager.GetPathByType(string.Empty);
        var _fileName = DateTime.Now.ToString("yyyyMMdd") + "_" + SnowflakeIdHelper.NextId() + Path.GetExtension(file.FileName);
        var stream = file.OpenReadStream();
        await _fileManager.UploadFileByType(stream, _filePath, _fileName);
        _cacheManager.Set(_fileName, string.Empty);
        return new { name = _fileName, url = string.Format("/api/File/Image/{0}/{1}", string.Empty, _fileName) };
    }

    /// <summary>
    /// 导入数据.
    /// </summary>
    /// <param name="input">请求参数</param>
    /// <returns></returns>
    [HttpPost("ImportData")]
    public async Task<dynamic> ImportData_Api([FromBody] ImportDataInput input)
    {
        var output = new ImportDataOutput();
        foreach (var item in input.list)
        {
            try
            {
                var entity = item.Adapt<EmployeeEntity>();
                var isOk = await _repository.AsSugarClient().Insertable(entity).CallEntityMethod(m => m.Creator()).ExecuteCommandAsync();
                if (isOk < 1)
                {
                    output.failResult.Add(item);
                    output.fnum++;
                }
                else
                {
                    output.snum++;
                }
            }
            catch (Exception)
            {
                output.failResult.Add(item);
                output.fnum++;
            }
        }
        if (output.snum == input.list.Count)
        {
            output.resultType = 0;
        }
        return output;
    }

    /// <summary>
    /// 模板下载.
    /// </summary>
    [HttpGet("TemplateDownload")]
    public async Task<dynamic> TemplateDownload()
    {
        var filePath = Path.Combine(FileVariable.TemplateFilePath, "employee_import_template.xlsx"); //模板路径
        var addFilePath = Path.Combine(FileVariable.TemplateFilePath, "职员信息.xlsx"); // 保存路径
        if (await _fileManager.ExistsFile(addFilePath))
        {
            var stream = await _fileManager.GetFileStream(filePath);
            await _fileManager.UploadFileByType(stream, FileVariable.TemporaryFilePath, "职员信息.xlsx");
        }
        else
        {
            throw Oops.Oh(ErrorCode.D8000);
        }
        _cacheManager.Set("职员信息.xlsx", string.Empty);
        return new { name = "职员信息.xlsx", url = "/api/file/Download?encryption=" + DESEncryption.Encrypt(_userManager.UserId + "|职员信息.xlsx|template", "JNPF") };
    }
    #endregion

    #region PrivateMethod

    /// <summary>
    /// 列表.
    /// </summary>
    /// <returns></returns>
    private async Task<List<EmployeeEntity>> GetListData()
    {
        return await _repository.AsQueryable().Where(x => x.DeleteMark == null).OrderBy(x => x.CreatorTime, OrderByType.Desc).ToListAsync();
    }

    /// <summary>
    /// 分页列表.
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private async Task<List<EmployeeEntity>> GetPageListData(EmployeeListQuery input)
    {
        var whereLambda = LinqExpression.And<EmployeeEntity>();
        whereLambda = whereLambda.And(x => x.DeleteMark == null);
        if (input.condition.IsNotEmptyOrNull() && input.keyword.IsNotEmptyOrNull())
        {
            string propertyName = input.condition;
            string propertyValue = input.keyword;
            switch (propertyName)
            {
                case "EnCode":            //工号
                    whereLambda = whereLambda.And(t => t.EnCode.Contains(propertyValue));
                    break;
                case "FullName":          //姓名
                    whereLambda = whereLambda.And(t => t.FullName.Contains(propertyValue));
                    break;
                case "Telephone":         //电话
                    whereLambda = whereLambda.And(t => t.Telephone.Contains(propertyValue));
                    break;
                case "DepartmentName":    //部门
                    whereLambda = whereLambda.And(t => t.DepartmentName.Contains(propertyValue));
                    break;
                case "PositionName":      //职位
                    whereLambda = whereLambda.And(t => t.PositionName.Contains(propertyValue));
                    break;
                default:
                    break;
            }
        }
        var list = await _repository.AsQueryable().Where(whereLambda).OrderBy(x => x.CreatorTime, OrderByType.Desc).ToPagedListAsync(input.currentPage, input.pageSize);
        return list.list.Adapt<List<EmployeeEntity>>();
    }

    /// <summary>
    /// 获取字段编码.
    /// </summary>
    /// <param name="filed"></param>
    /// <returns></returns>
    private string GetFiledEncode(string filed)
    {
        switch (filed)
        {
            case "工号":
                return "enCode";
            case "姓名":
                return "fullName";
            case "性别":
                return "gender";
            case "部门":
                return "departmentName";
            case "职务":
            case "岗位":
                return "positionName";
            case "用工性质":
                return "workingNature";
            case "身份证号":
                return "idNumber";
            case "联系电话":
                return "telephone";
            case "出生年月":
                return "birthday";
            case "参加工作":
                return "attendWorkTime";
            case "最高学历":
                return "education";
            case "所学专业":
                return "major";
            case "毕业院校":
                return "graduationAcademy";
            case "毕业时间":
                return "graduationTime";
            case "创建时间":
                return "creatorTime";
            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// 获取字段名称.
    /// </summary>
    /// <param name="filed"></param>
    /// <returns></returns>
    private string GetFiledName(string filed)
    {
        switch (filed)
        {
            case "enCode":
                return "工号";
            case "fullName":
                return "姓名";
            case "gender":
                return "性别";
            case "departmentName":
                return "部门";
            case "positionName":
                return "岗位";
            case "workingNature":
                return "用工性质";
            case "idNumber":
                return "身份证号";
            case "telephone":
                return "联系电话";
            case "birthday":
                return "出生年月";
            case "attendWorkTime":
                return "参加工作";
            case "education":
                return "最高学历";
            case "major":
                return "所学专业";
            case "graduationAcademy":
                return "毕业院校";
            case "graduationTime":
                return "毕业时间";
            case "creatorTime":
                return "创建时间";
            default:
                return string.Empty;
        }
    }
    #endregion
}
