﻿using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// IHost 主机拓展类
/// </summary>
public static class IHostExtensions
{
    /// <summary>
    /// 获取主机启动地址
    /// </summary>
    /// <param name="host"></param>
    /// <returns></returns>
    public static IEnumerable<string> GetServerAddresses(this IHost host)
    {
        var server = host.Services.GetRequiredService<IServer>();
        var addressesFeature = server.Features.Get<IServerAddressesFeature>();
        return addressesFeature?.Addresses;
    }

    /// <summary>
    /// 获取主机启动地址
    /// </summary>
    /// <param name="host"></param>
    /// <returns></returns>
    public static string GetServerAddress(this IHost host)
    {
        return host.GetServerAddresses()?.FirstOrDefault();
    }

    /// <summary>
    /// 获取主机启动地址
    /// </summary>
    /// <param name="server"></param>
    /// <returns></returns>
    public static IEnumerable<string> GetServerAddresses(this IServer server)
    {
        var addressesFeature = server.Features.Get<IServerAddressesFeature>();
        return addressesFeature?.Addresses;
    }

    /// <summary>
    /// 获取主机启动地址
    /// </summary>
    /// <param name="server"></param>
    /// <returns></returns>
    public static string GetServerAddress(this IServer server)
    {
        return server.GetServerAddresses()?.FirstOrDefault();
    }
}