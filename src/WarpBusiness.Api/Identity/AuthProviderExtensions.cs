using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using Microsoft.IdentityModel.Tokens;

namespace WarpBusiness.Api.Identity;

public static class AuthProviderExtensions
{
    /// <summary>
    /// Registers authentication based on the configured AuthProvider:ActiveProvider value.
    /// Call this AFTER AddIdentity in Program.cs — it replaces the existing AddAuthentication call.
    /// </summary>
    public static IServiceCollection AddWarpAuthentication(
        this IServiceCollection services,
        IConfiguration config)
    {
        var options = config
            .GetSection(AuthProviderOptions.SectionName)
            .Get<AuthProviderOptions>() ?? new AuthProviderOptions();

        services.Configure<AuthProviderOptions>(
            config.GetSection(AuthProviderOptions.SectionName));

        return options.ActiveProvider switch
        {
            AuthProviderType.Keycloak => services.AddKeycloakAuth(config, options.Keycloak),
            AuthProviderType.Microsoft => services.AddMicrosoftAuth(config, options.Microsoft),
            _ => services.AddLocalJwtAuth(config),
        };
    }

    private static IServiceCollection AddLocalJwtAuth(
        this IServiceCollection services,
        IConfiguration config)
    {
        var jwtKey = config["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured");

        services.AddAuthentication(opt =>
        {
            opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(opt =>
        {
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = config["Jwt:Issuer"],
                ValidAudience = config["Jwt:Audience"],
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

        return services;
    }

    private static IServiceCollection AddKeycloakAuth(
        this IServiceCollection services,
        IConfiguration config,
        KeycloakOptions keycloak)
    {
        if (string.IsNullOrEmpty(keycloak.Authority))
            throw new InvalidOperationException(
                "AuthProvider:Keycloak:Authority is required when ActiveProvider is Keycloak");

        services.AddAuthentication(opt =>
        {
            opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(opt =>
        {
            opt.Authority = keycloak.Authority;
            opt.Audience = keycloak.Audience;
            opt.RequireHttpsMetadata = true;
            opt.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = !string.IsNullOrEmpty(keycloak.Audience),
                ValidateLifetime = true,
                NameClaimType = keycloak.NameClaim,
                RoleClaimType = ClaimTypes.Role,
            };
            opt.Events = new JwtBearerEvents
            {
                OnTokenValidated = async ctx =>
                {
                    // Map Keycloak identity to ApplicationUser on first login
                    var mapper = ctx.HttpContext.RequestServices
                        .GetRequiredService<IExternalIdentityMapper>();
                    await mapper.EnsureUserAsync(ctx.Principal!, AuthProviderType.Keycloak);
                }
            };
        });

        return services;
    }

    private static IServiceCollection AddMicrosoftAuth(
        this IServiceCollection services,
        IConfiguration config,
        MicrosoftOptions ms)
    {
        if (string.IsNullOrEmpty(ms.TenantId) || string.IsNullOrEmpty(ms.ClientId))
            throw new InvalidOperationException(
                "AuthProvider:Microsoft:TenantId and ClientId are required when ActiveProvider is Microsoft");

        // Microsoft.Identity.Web wires up Azure AD / Entra ID bearer token validation
        services.AddMicrosoftIdentityWebApiAuthentication(config, "AuthProvider:Microsoft");

        // Also handle claim mapping on token validation
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, opt =>
        {
            var inner = opt.Events?.OnTokenValidated;
            opt.Events ??= new JwtBearerEvents();
            opt.Events.OnTokenValidated = async ctx =>
            {
                if (inner != null) await inner(ctx);
                var mapper = ctx.HttpContext.RequestServices
                    .GetRequiredService<IExternalIdentityMapper>();
                await mapper.EnsureUserAsync(ctx.Principal!, AuthProviderType.Microsoft);
            };
        });

        return services;
    }
}
