﻿using JNPF.Extras.CollectiveOAuth.Enums;

namespace JNPF.Extras.CollectiveOAuth.Config;

/// <summary>
/// Github.
/// </summary>
public class GithubAuthSource : IAuthSource
{
    public string authorize()
    {
        return "https://github.com/login/oauth/authorize";
    }

    public string accessToken()
    {
        return "https://github.com/login/oauth/access_token";
    }

    public string userInfo()
    {
        return "https://api.github.com/user";
    }

    public string revoke()
    {
        throw new System.NotImplementedException();
    }

    public string refresh()
    {
        throw new System.NotImplementedException();
    }

    public string getName()
    {
        return DefaultAuthSourceEnum.GITHUB.ToString();
    }
}