﻿namespace JNPF.TimeCrontab;

/// <summary>
/// Cron 字段种类
/// </summary>
internal enum CrontabFieldKind
{
    /// <summary>
    /// 秒
    /// </summary>
    Second = 0,

    /// <summary>
    /// 分
    /// </summary>
    Minute = 1,

    /// <summary>
    /// 时
    /// </summary>
    Hour = 2,

    /// <summary>
    /// 天
    /// </summary>
    Day = 3,

    /// <summary>
    /// 月
    /// </summary>
    Month = 4,

    /// <summary>
    /// 星期
    /// </summary>
    DayOfWeek = 5,

    /// <summary>
    /// 年
    /// </summary>
    Year = 6
}