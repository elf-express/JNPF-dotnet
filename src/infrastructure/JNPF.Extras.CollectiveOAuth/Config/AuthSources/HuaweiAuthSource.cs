﻿using JNPF.Extras.CollectiveOAuth.Enums;

namespace JNPF.Extras.CollectiveOAuth.Config;

/// <summary>
/// 华为.
/// </summary>
public class HuaweiAuthSource : IAuthSource
{
    public string authorize()
    {
        return "https://oauth-login.cloud.huawei.com/oauth2/v2/authorize";
    }

    public string accessToken()
    {
        return "https://oauth-login.cloud.huawei.com/oauth2/v2/token";
    }

    public string userInfo()
    {
        return "https://api.vmall.com/rest.php";
    }

    public string revoke()
    {
        throw new System.NotImplementedException();
    }

    public string refresh()
    {
        return "https://oauth.kujiale.com/oauth2/auth/token/refresh";
    }

    public string getName()
    {
        return DefaultAuthSourceEnum.KUJIALE.ToString();
    }
}