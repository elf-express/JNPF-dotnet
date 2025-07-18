﻿using JNPF.Extras.CollectiveOAuth.Enums;

namespace JNPF.Extras.CollectiveOAuth.Config;

/// <summary>
/// 微软.
/// </summary>
public class MicrosoftAuthSource : IAuthSource
{
    public string authorize()
    {
        return "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
    }

    public string accessToken()
    {
        return "https://login.microsoftonline.com/common/oauth2/v2.0/token";
    }

    public string userInfo()
    {
        return "https://graph.microsoft.com/v1.0/me";
    }

    public string revoke()
    {
        throw new System.NotImplementedException();
    }

    public string refresh()
    {
        return "https://login.microsoftonline.com/common/oauth2/v2.0/token";
    }

    public string getName()
    {
        return DefaultAuthSourceEnum.MICROSOFT.ToString();
    }
}