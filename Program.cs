using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Helpers.OptionsSetup;
using Arlink28.Api.Helpers.Settings;
using Arlink28.Api.Middleware;
using Asp.Versioning;
using FluentValidation;
using FluentValidation.AspNetCore;
using Grinderofl.FeatureFolders;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

try
{
    Log.Logger = new LoggerConfiguration()
        .WriteTo.Console()
        .CreateLogger();

    Log.Information("Starting Arlink28 API.");

    builder.Host.UseSerilog((context, loggerConfiguration) =>
    {
        loggerConfiguration.MinimumLevel.Warning();
        loggerConfiguration.WriteTo.Console();
        loggerConfiguration.ReadFrom.Configuration(context.Configuration);
    });

    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 2,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
            npgsqlOptions.CommandTimeout(30);
        });
    });

    builder.Services.Configure<AppSettings>(builder.Configuration.GetSection(AppSettings.Section));
    builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection(EmailSettings.Section));
    builder.Services.Configure<AdminBootstrapSettings>(builder.Configuration.GetSection(AdminBootstrapSettings.Section));

    builder.Services.AddScoped<IPasswordHasher<Staff>, PasswordHasher<Staff>>();
    builder.Services.AddScoped<DataContextInitializer>();

    // Scrutor: auto-register services by lifetime marker interface
    builder.Services.Scan(s =>
        s.FromAssemblyOf<ITransient>()
            .AddClasses(c => c.AssignableTo<ITransient>())
            .AsImplementedInterfaces()
            .WithTransientLifetime());
    builder.Services.Scan(s =>
        s.FromAssemblyOf<ISingleton>()
            .AddClasses(c => c.AssignableTo<ISingleton>())
            .AsImplementedInterfaces()
            .WithSingletonLifetime());
    builder.Services.Scan(s =>
        s.FromAssemblyOf<IScoped>()
            .AddClasses(c => c.AssignableTo<IScoped>())
            .AsImplementedInterfaces()
            .WithScopedLifetime());

    builder.Services.ConfigureOptions<JwtBearerOptionsSetup>();

    builder.Services.AddAuthentication(x =>
        {
            x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer();

    builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1);
            options.ReportApiVersions = true;
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ApiVersionReader = ApiVersionReader.Combine(
                new UrlSegmentApiVersionReader(),
                new HeaderApiVersionReader("X-Api-Version"));
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

    builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
    builder.Services.AddTransient<IConfigureOptions<CorsOptions>, ConfigureCorsOptions>();

    builder.Services.AddHealthChecks()
        .AddNpgSql(connectionString ?? string.Empty)
        .AddDbContextCheck<ApplicationDbContext>();

    builder.Services.AddFluentValidationAutoValidation();
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    builder.Services.AddControllers()
        .AddFeatureFolders()
        .AddNewtonsoftJson(options =>
        {
            options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            // Enums travel as names ("Operator"), matching the string fields in responses.
            // Integers are still accepted on input.
            options.SerializerSettings.Converters.Add(new StringEnumConverter());
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    // Generate schemas from the Newtonsoft settings above (enum names, not integers),
    // not from System.Text.Json, which this app doesn't use for MVC.
    builder.Services.AddSwaggerGenNewtonsoftSupport();
    builder.Services.AddCors();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var initialiser = scope.ServiceProvider.GetRequiredService<DataContextInitializer>();
        await initialiser.SeedSuperAdminAsync();
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        var descriptions = app.DescribeApiVersions();
        foreach (var versionName in descriptions.Select(x => x.GroupName))
            options.SwaggerEndpoint($"/swagger/{versionName}/swagger.json", versionName.ToUpperInvariant());
    });

    app.UseCors("CorsPolicy");

    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });

    app.UseRouting();
    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status418ImATeapot,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        },
        AllowCachingResponses = false,
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Server terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
