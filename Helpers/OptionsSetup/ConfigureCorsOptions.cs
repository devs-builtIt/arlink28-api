using Arlink28.Api.Helpers.Settings;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Arlink28.Api.Helpers.OptionsSetup;

public class ConfigureCorsOptions(IOptions<AppSettings> appSettings) : IConfigureOptions<CorsOptions>
{
    private readonly AppSettings _appSettings = appSettings.Value;

    public void Configure(CorsOptions options)
    {
        options.AddPolicy("CorsPolicy", policy =>
        {
            policy
                .WithOrigins(_appSettings.FrontendBaseUrl, _appSettings.WebUrl)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    }
}
