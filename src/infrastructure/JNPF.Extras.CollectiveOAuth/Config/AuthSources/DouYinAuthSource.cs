﻿using JNPF.Extras.CollectiveOAuth.Enums;

namespace JNPF.Extras.CollectiveOAuth.Config;

/// <summary>
/// 抖音.
/// </summary>
public class DouyinAuthSource : IAuthSource
{
    public string authorize()
    {
        return "https://open.douyin.com/platform/oauth/connect";
    }

    public string accessToken()
    {
        return "https://open.douyin.com/oauth/access_token/";
    }

    public string userInfo()
    {
        return "https://open.douyin.com/oauth/userinfo/";
    }

    public string revoke()
    {
        throw new System.NotImplementedException();
    }

    public string refresh()
    {
        return "https://open.douyin.com/oauth/refresh_token/";
    }

    public string getName()
    {
        return DefaultAuthSourceEnum.DOUYIN.ToString();
    }
}