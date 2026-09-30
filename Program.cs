using System.Diagnostics;
using System.Threading.RateLimiting;
using Arlink28.Api.Data;
using Arlink28.Api.Data.Entities;
using Arlink28.Api.Data.Seed;
using Arlink28.Api.Features.Enquiries.Controllers;
using Arlink28.Api.Features.Shared.Interfaces;
using Arlink28.Api.Features.Shared.Services.Interfaces;
using Arlink28.Api.Features.Shared.Services;
using Arlink28.Api.Helpers;
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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
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
    builder.Services.Configure<MediaStorageSettings>(builder.Configuration.GetSection(MediaStorageSettings.Section));
    builder.Services.Configure<EnquirySettings>(builder.Configuration.GetSection(EnquirySettings.Section));

    builder.Services.AddHostedService<Arlink28.Api.Features.Enquiries.Services.EnquiryNotificationWorker>();

    builder.Services.AddScoped<IPasswordHasher<Staff>, PasswordHasher<Staff>>();
    builder.Services.AddScoped<DataContextInitializer>();
    builder.Services.AddScoped<CatalogueSeeder>();

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

    // Every error is RFC 9457 Problem Details (ADR 0005): controller errors (ApiProblem),
    // validation failures, exceptions (ExceptionHandlingMiddleware) and empty-bodied
    // 401/403/404 (UseStatusCodePages). Each carries a stable `code` and the `traceId`.
    builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ctx =>
    {
        var problem = ctx.ProblemDetails;
        problem.Extensions.TryAdd("traceId", Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier);
        var status = problem.Status ?? ctx.HttpContext.Response.StatusCode;
        problem.Extensions.TryAdd("code",
            problem is HttpValidationProblemDetails ? ErrorCodes.ValidationFailed : ErrorCodes.ForStatus(status));
    });

    // The public enquiry form is open to anyone, so it is limited per address. A rejected request
    // gets the same Problem Details shape as every other error (code RATE_LIMITED).
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, ct) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
            await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(
                new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too Many Requests",
                        Detail = "Too many enquiries from this address. Please try again in a few minutes.",
                    },
                });
        };
        options.AddPolicy(EnquiriesController.RateLimitPolicy, http =>
            RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(10),
                    QueueLimit = 0,
                }));
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    // Generate schemas from the Newtonsoft settings above (enum names, not integers),
    // not from System.Text.Json, which this app doesn't use for MVC.
    builder.Services.AddSwaggerGenNewtonsoftSupport();
    builder.Services.AddCors();

    var app = builder.Build();

    // `dotnet run -- seed-catalogue [--today=YYYY-MM-DD] [--allow-production]`
    // loads the poster catalogue (Data/Seed) and exits without starting the server.
    if (args.Length > 0 && args[0] == "seed-catalogue")
    {
        if (app.Environment.IsProduction() && !args.Contains("--allow-production"))
        {
            Console.Error.WriteLine("Refusing to seed the catalogue in Production without --allow-production.");
            Environment.ExitCode = 1;
            return;
        }

        var todayArg = args.FirstOrDefault(a => a.StartsWith("--today="));
        var today = todayArg is null
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : DateOnly.Parse(todayArg["--today=".Length..]);

        using var seedScope = app.Services.CreateScope();
        var results = await seedScope.ServiceProvider.GetRequiredService<CatalogueSeeder>().SeedAsync(today);
        foreach (var r in results)
            Console.WriteLine($"{r.Action,-8} {r.Status,-10} {r.Slug}{(r.DataIssue is null ? "" : "  (draft: " + r.DataIssue + ")")}");
        Console.WriteLine($"Seeded {results.Count} packages (from-prices as of {today:yyyy-MM-dd}).");
        return;
    }

    // `dotnet run -- seed-photos [--dry-run] [--undo] [--count=4] [--allow-production]`
    // downloads test photos into media storage and adds them to packages that have none
    // (see TestPhotoSeeder). --undo removes exactly what it added. Exits without starting the server.
    if (args.Length > 0 && args[0] == "seed-photos")
    {
        if (app.Environment.IsProduction() && !args.Contains("--allow-production"))
        {
            Console.Error.WriteLine("Refusing to add test photos in Production without --allow-production.");
            Environment.ExitCode = 1;
            return;
        }

        var dryRun = args.Contains("--dry-run");
        var countArg = args.FirstOrDefault(a => a.StartsWith("--count="));
        var count = countArg is null ? 4 : Math.Clamp(int.Parse(countArg["--count=".Length..]), 1, 6);

        using var photoScope = app.Services.CreateScope();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (arlink28 test photo seeder)");
        var seeder = new TestPhotoSeeder(
            photoScope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            photoScope.ServiceProvider.GetRequiredService<IMediaStorage>(),
            http);

        var lines = args.Contains("--undo") ? await seeder.UndoAsync(dryRun) : await seeder.SeedAsync(count, dryRun);
        foreach (var line in lines) Console.WriteLine(line);
        Console.WriteLine(dryRun ? "Dry run: nothing was changed." : "Done.");
        return;
    }

    using (var scope = app.Services.CreateScope())
    {
        var initialiser = scope.ServiceProvider.GetRequiredService<DataContextInitializer>();
        await initialiser.SeedSuperAdminAsync();
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseStatusCodePages();

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

    // Uploaded photos (MediaStorage:RootPath) are public, like the catalogue that shows them.
    var mediaSettings = app.Services.GetRequiredService<IOptions<MediaStorageSettings>>().Value;
    var mediaRoot = Path.GetFullPath(mediaSettings.RootPath, app.Environment.ContentRootPath);
    Directory.CreateDirectory(mediaRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(mediaRoot),
        RequestPath = mediaSettings.PublicPath,
        OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "public,max-age=31536000,immutable",
    });

    app.UseRouting();
    app.UseRateLimiter();
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
