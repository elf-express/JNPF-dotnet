﻿using JNPF.Extras.CollectiveOAuth.Enums;

namespace JNPF.Extras.CollectiveOAuth.Config;

/// <summary>
/// Linkin领英.
/// </summary>
public class LinkedInAuthSource : IAuthSource
{
    public string authorize()
    {
        return "https://www.linkedin.com/oauth/v2/authorization";
    }

    public string accessToken()
    {
        return "https://www.linkedin.com/oauth/v2/accessToken";
    }

    public string userInfo()
    {
        return "https://api.linkedin.com/v2/me";
    }

    public string revoke()
    {
        throw new System.NotImplementedException();
    }

    public string refresh()
    {
        return "https://www.linkedin.com/oauth/v2/accessToken";
    }

    public string getName()
    {
        return DefaultAuthSourceEnum.LINKEDIN.ToString();
    }
}