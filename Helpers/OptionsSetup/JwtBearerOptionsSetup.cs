using System.Text;
using Arlink28.Api.Helpers.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Arlink28.Api.Helpers.OptionsSetup;

public class JwtBearerOptionsSetup(
    IOptions<AppSettings> appSettings,
    IHostEnvironment env) : IConfigureNamedOptions<JwtBearerOptions>
{
    private readonly AppSettings _appSettings = appSettings.Value;

    public void Configure(JwtBearerOptions options)
    {
        options.RequireHttpsMetadata = !env.IsDevelopment(); // A.8.5, A.8.24
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _appSettings.ValidIssuer,
            ValidAudience = _appSettings.ValidAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_appSettings.Secret)),
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero
        };
    }

    public void Configure(string? name, JwtBearerOptions options) => Configure(options);
}
