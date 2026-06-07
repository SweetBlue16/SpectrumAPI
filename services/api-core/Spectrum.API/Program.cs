using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Spectrum.API.Configuration;
using Spectrum.API.Data;
using Spectrum.API.Grpc.Drops;
using Spectrum.API.Grpc.Social;
using Spectrum.API.Middlewares;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Auth;
using Spectrum.API.Services.Admin;
using Spectrum.API.Services.Analytics;
using Spectrum.API.Services.Drops;
using Spectrum.API.Services.Email;
using Spectrum.API.Services.External;
using Spectrum.API.Services.Home;
using Spectrum.API.Services.Profile;
using Spectrum.API.Services.Reports;
using Spectrum.API.Services.Reviews;
using Spectrum.API.Services.Search;
using Spectrum.API.Services.Seeding;
using Spectrum.API.Services.Votes;
using Spectrum.API.Services.Storage;
using Spectrum.API.Services.Clips;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("\n[SPECTRUM API] Starting server configuration...");
Console.ResetColor();

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    AddDevelopmentEnvFileConfiguration(builder.Configuration, builder.Environment.ContentRootPath);
    AddDerivedDevelopmentConnectionStrings(builder.Configuration);
}

builder.Configuration.AddEnvironmentVariables();

Console.WriteLine("[SPECTRUM API] Configuring exceptions and controllers...");
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

builder.Services.AddOptions<SmtpOptions>()
    .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
    .Configure(options =>
    {
        options.Host = Environment.GetEnvironmentVariable("SMTP_HOST") ?? options.Host;
        options.Username = Environment.GetEnvironmentVariable("SMTP_USERNAME") ?? options.Username;
        options.Password = Environment.GetEnvironmentVariable("SMTP_PASSWORD") ?? options.Password;
        options.FromEmail = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL") ?? options.FromEmail;
        options.FromName = Environment.GetEnvironmentVariable("SMTP_FROM_NAME") ?? options.FromName;

        if (int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var smtpPort))
        {
            options.Port = smtpPort;
        }

        if (bool.TryParse(Environment.GetEnvironmentVariable("SMTP_USE_TLS"), out var useTls))
        {
            options.UseTls = useTls;
        }
    });
builder.Services.AddOptions<VerificationCodeOptions>()
    .Bind(builder.Configuration.GetSection(VerificationCodeOptions.SectionName));
builder.Services.AddOptions<DemoSeedOptions>()
    .Bind(builder.Configuration.GetSection(DemoSeedOptions.SectionName))
    .Configure(options =>
    {
        var mongoHost = builder.Configuration["DB_MONGO_HOST"] ?? "localhost";
        var mongoPort = builder.Configuration["DB_MONGO_PORT"] ?? "27017";
        var mongoUser = builder.Configuration["DB_MONGO_USER"];
        var mongoPassword = builder.Configuration["DB_MONGO_PASSWORD"];

        options.SocialDatabaseName = builder.Configuration["DB_MONGO_SOCIAL_NAME"] ?? options.SocialDatabaseName;
        options.DropsDatabaseName = builder.Configuration["DB_MONGO_DROPS_NAME"] ?? options.DropsDatabaseName;
        options.DemoAdminPassword = builder.Configuration["DEMO_SEED_ADMIN_PASSWORD"] ?? options.DemoAdminPassword;
        options.DemoPassword = builder.Configuration["DEMO_SEED_PASSWORD"] ?? options.DemoPassword;

        options.SocialMongoConnectionString = BuildMongoConnectionString(
            mongoHost,
            mongoPort,
            options.SocialDatabaseName,
            mongoUser,
            mongoPassword,
            options.SocialMongoConnectionString);
        options.DropsMongoConnectionString = BuildMongoConnectionString(
            mongoHost,
            mongoPort,
            options.DropsDatabaseName,
            mongoUser,
            mongoPassword,
            options.DropsMongoConnectionString);
    });

Console.WriteLine("[SPECTRUM API] Configuring CORS policy (AllowFrontend)...");
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        }
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("SensitiveAuth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

Console.WriteLine("[SPECTRUM API] Configuring PostgreSQL database context...");
builder.Services.AddDbContext<SpectrumDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

Console.WriteLine("[SPECTRUM API] Registering repositories and services...");
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAdminDetailRepository, AdminDetailRepository>();
builder.Services.AddScoped<IReviewRepository, ReviewRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminProfileService, AdminProfileService>();
builder.Services.AddScoped<IAdminNotificationService, AdminNotificationService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<ICommentAnalyticsService, CommentAnalyticsService>();
builder.Services.AddScoped<IHomeDashboardService, HomeDashboardService>();
builder.Services.AddScoped<IGlobalSearchService, GlobalSearchService>();
builder.Services.AddScoped<IDemoSeedService, DemoSeedService>();
builder.Services.AddScoped<IVerificationCodeService, VerificationCodeService>();
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<IReviewCommentService, ReviewCommentService>();
builder.Services.AddScoped<IVoteService, VoteServiceClient>();
builder.Services.AddScoped<IReportService, ReportsService>();
builder.Services.AddScoped<IUserModerationService, UserModerationService>();
builder.Services.AddScoped<IDropsService, DropsService>();
builder.Services.AddScoped<IRewardDeliveryService, EmailRewardDeliveryService>();
builder.Services.AddScoped<IImageStorageService, ImageStorageService>();
builder.Services.AddScoped<IVideoStorageService, VideoStorageService>();
builder.Services.AddScoped<IGameClipService, GameClipService>();
builder.Services.AddScoped<IGameClipRepository, GameClipRepository>(); 

Console.WriteLine("[SPECTRUM API] Registering Game Catalog Services (Memory Cache & Sync)...");
builder.Services.AddSingleton<IGameRepository, GameRepository>();
builder.Services.AddHttpClient<IRawgSyncService, RawgSyncService>();
builder.Services.AddScoped<IGameService, GameService>();

Console.WriteLine("[SPECTRUM API] Configuring JWT Authentication...");
var jwtSecret = builder.Configuration["JwtSettings:Secret"]
    ?? throw new InvalidOperationException("JwtSettings:Secret is not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

Console.WriteLine("[SPECTRUM API] Generating Swagger documentation...");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Spectrum API", Version = "v1" });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter the JWT token prefixed with ‘Bearer ’. Example: Bearer abcdef12345"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new List<string>()
        }
    });
});

Console.WriteLine("[SPECTRUM API] Configuring gRPC clients for services...");
builder.Services.AddGrpcClient<DropService.DropServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcSettings:DropsServiceUrl"]!);
});

builder.Services.AddGrpcClient<VoteService.VoteServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcSettings:SocialServiceUrl"]!);
});

builder.Services.AddGrpcClient<CommentService.CommentServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcSettings:SocialServiceUrl"]!);
});

builder.Services.AddGrpcClient<ReportService.ReportServiceClient>(options =>
{
    options.Address = new Uri(builder.Configuration["GrpcSettings:SocialServiceUrl"]!);
});

Console.WriteLine("[SPECTRUM API] Building the application...");
var app = builder.Build();

Console.WriteLine("[SPECTRUM API] Setting up the HTTP request pipeline...");
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

Console.WriteLine("[SPECTRUM API] Initializing in-memory game catalog...\n");

var repository = app.Services.GetRequiredService<IGameRepository>();
var totalGames = repository.GetAll().Count();

if (totalGames > 0)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"\n[SPECTRUM API] Success: {totalGames} high-quality games loaded into RAM.");
    Console.ResetColor();
}
else
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("[SPECTRUM API] Warning: Snapshot not found or empty. Run sync via AdminGamesController.");
    Console.ResetColor();
}

app.UseExceptionHandler();

app.UseCors("AllowFrontend");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("[SPECTRUM API] Boot sequence complete. Server is now listening to requests!\n");
Console.ResetColor();

await app.RunAsync();

static void AddDevelopmentEnvFileConfiguration(ConfigurationManager configuration, string contentRootPath)
{
    var envFilePath = FindInfraEnvFile(contentRootPath);
    if (envFilePath is null)
    {
        return;
    }

    var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    foreach (var line in File.ReadLines(envFilePath))
    {
        var trimmedLine = line.Trim();
        if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
        {
            continue;
        }

        var separatorIndex = trimmedLine.IndexOf('=');
        if (separatorIndex <= 0)
        {
            continue;
        }

        var key = trimmedLine[..separatorIndex].Trim();
        var value = TrimConfigurationValue(trimmedLine[(separatorIndex + 1)..].Trim());
        values[key] = value;

        if (key.Contains("__", StringComparison.Ordinal))
        {
            values[key.Replace("__", ":", StringComparison.Ordinal)] = value;
        }
    }

    if (values.Count > 0)
    {
        configuration.AddInMemoryCollection(values);
    }
}

static void AddDerivedDevelopmentConnectionStrings(ConfigurationManager configuration)
{
    if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
    {
        return;
    }

    var postgresUser = configuration["DB_POSTGRES_USER"];
    var postgresPassword = configuration["DB_POSTGRES_PASSWORD"];
    var postgresDatabase = configuration["DB_POSTGRES_NAME"];
    if (string.IsNullOrWhiteSpace(postgresUser) ||
        string.IsNullOrWhiteSpace(postgresPassword) ||
        string.IsNullOrWhiteSpace(postgresDatabase))
    {
        return;
    }

    var builder = new Npgsql.NpgsqlConnectionStringBuilder
    {
        Host = configuration["DB_POSTGRES_HOST"] ?? "localhost",
        Port = int.TryParse(configuration["DB_POSTGRES_PORT"], out var port) ? port : 5432,
        Database = postgresDatabase,
        Username = postgresUser,
        Password = postgresPassword
    };

    configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:DefaultConnection"] = builder.ConnectionString
    });
}

static string BuildMongoConnectionString(
    string host,
    string port,
    string databaseName,
    string? username,
    string? password,
    string fallbackConnectionString)
{
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
    {
        return string.IsNullOrWhiteSpace(fallbackConnectionString)
            ? $"mongodb://{host}:{port}/{databaseName}"
            : fallbackConnectionString;
    }

    var encodedUsername = Uri.EscapeDataString(username);
    var encodedPassword = Uri.EscapeDataString(password);
    return $"mongodb://{encodedUsername}:{encodedPassword}@{host}:{port}/{databaseName}?authSource=admin";
}

static string? FindInfraEnvFile(string contentRootPath)
{
    var directory = new DirectoryInfo(contentRootPath);
    while (directory is not null)
    {
        var candidate = Path.Combine(directory.FullName, "infra", ".env");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    return null;
}

static string TrimConfigurationValue(string value)
{
    if (value.Length >= 2 &&
        ((value.StartsWith('"') && value.EndsWith('"')) ||
         (value.StartsWith('\'') && value.EndsWith('\''))))
    {
        return value[1..^1];
    }

    return value;
}
