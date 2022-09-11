﻿using System.Collections.Generic;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace common.Handlers
{
    [ExcludeFromCodeCoverage]
    public class AuthorizationValidator : IAuthorizationValidator
    {
        private readonly ILogger<AuthorizationValidator> _log;
        private const string ScopeType = @"http://schemas.microsoft.com/identity/claims/scope";

        public AuthorizationValidator(ILogger<AuthorizationValidator> log)
        {
            _log = log;
        }

        public async Task<Tuple<bool, string>> ValidateTokenAsync(AuthenticationHeaderValue authenticationHeader)
        {
            if (authenticationHeader == null) return new Tuple<bool, string>(false, string.Empty);
            if (string.IsNullOrEmpty(authenticationHeader.Parameter)) throw new ArgumentNullException(nameof(authenticationHeader));

            var issuer = $"https://sts.windows.net/{Environment.GetEnvironmentVariable("CallingAppTenantId")}/";
            var audience = Environment.GetEnvironmentVariable("CallingAppValidAudience");
            var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(issuer + "/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever());

            
            var discoveryDocument = await configurationManager.GetConfigurationAsync(default);
            var signingKeys = discoveryDocument.SigningKeys;

            var validationParameters = new TokenValidationParameters
            {
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(2),
            };

            try
            {
                var tokenValidator = new JwtSecurityTokenHandler();
                var claimsPrincipal =
                    tokenValidator.ValidateToken(authenticationHeader.Parameter, validationParameters, out _);

                var requiredScopes = Environment.GetEnvironmentVariable("CallingAppValidScopes")
                    ?.Replace(" ", string.Empty).Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries).ToList();
                var requiredRoles = Environment.GetEnvironmentVariable("CallingAppValidRoles")
                    ?.Split(new[] {","}, StringSplitOptions.RemoveEmptyEntries).ToList();

                return IsValid(claimsPrincipal, requiredScopes, requiredRoles) ? new Tuple<bool, string>(true, authenticationHeader.Parameter) : new Tuple<bool, string>(false, string.Empty);
            }
            catch (SecurityTokenValidationException securityException)
            {
                _log.LogError(securityException, "A security exception was caught");
                return new Tuple<bool, string>(false, string.Empty);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "An unexpected error was caught");
                return new Tuple<bool, string>(false, string.Empty);
            }
        }

        private static bool IsValid(ClaimsPrincipal claimsPrincipal, List<string> requiredScopes = null, List<string> requiredRoles = null)
        {
            if (claimsPrincipal == null)
            {
                return false;
            }

            requiredScopes = requiredScopes?.ToList() ?? new List<string>();
            requiredRoles = requiredRoles?.ToList() ?? new List<string>();

            if (!requiredScopes.Any() && !requiredRoles.Any())
            {
                return true;
            }

            var hasAccessToRoles = !requiredRoles.Any() || requiredRoles.All(claimsPrincipal.IsInRole);
            
            var scopeClaim = claimsPrincipal.HasClaim(x => x.Type == ScopeType)
                ? claimsPrincipal.Claims.First(x => x.Type == ScopeType).Value
                : string.Empty;

            var tokenScopes = scopeClaim.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var hasAccessToScopes = !requiredScopes.Any() || requiredScopes.All(x => tokenScopes.Any(y => string.Equals(x, y, StringComparison.OrdinalIgnoreCase)));

            return hasAccessToRoles && hasAccessToScopes;
        }
    }
}

