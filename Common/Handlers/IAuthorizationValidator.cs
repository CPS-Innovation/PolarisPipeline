﻿using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace common.Handlers
{
    public interface IAuthorizationValidator
    {
        Task<Tuple<bool, string>> ValidateTokenAsync(AuthenticationHeaderValue authenticationHeader, Guid correlationId, string validAudience = "");
    }
}

