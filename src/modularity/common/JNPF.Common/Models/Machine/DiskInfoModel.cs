﻿using JNPF.DependencyInjection;

namespace JNPF.Common.Model.Machine;

/// <summary>
/// 硬盘信息模型.
/// </summary>
[SuppressSniffer]
public class DiskInfoModel
{
    /// <summary>
    /// 硬盘总容量.
    /// </summary>
    public string total { get; set; }

    /// <summary>
    /// 空闲硬盘.
    /// </summary>
    public string available { get; set; }

    /// <summary>
    /// 已使用硬盘.
    /// </summary>
    public string used { get; set; }

    /// <summary>
    /// 已使用百分比.
    /// </summary>
    public string usageRate { get; set; }
}