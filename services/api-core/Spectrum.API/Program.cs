using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Spectrum.API.Data;
using Spectrum.API.Grpc.Drops;
using Spectrum.API.Grpc.Social;
using Spectrum.API.Middlewares;
using Spectrum.API.Repositories;
using Spectrum.API.Services.Auth;
using Spectrum.API.Services.External;
using System.Reflection;
using System.Text;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("\n[SPECTRUM API] Starting server configuration...");
Console.ResetColor();

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("[SPECTRUM API] Configuring exceptions and controllers...");
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

Console.WriteLine("[SPECTRUM API] Configuring CORS policy (AllowFrontend)...");
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

Console.WriteLine("[SPECTRUM API] Configuring PostgreSQL database context...");
builder.Services.AddDbContext<SpectrumDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

Console.WriteLine("[SPECTRUM API] Registering repositories and services...");
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IAdminDetailRepository, AdminDetailRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();

Console.WriteLine("[SPECTRUM API] Configuring external HTTP client for RAWG API...");
builder.Services.AddHttpClient<IGameService, GameService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["RawgApi:BaseUrl"]);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

Console.WriteLine("[SPECTRUM API] Configuring JWT Authentication...");
var jwtSecret = builder.Configuration["JwtSettings:Secret"];
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
    options.Address = new Uri("http://localhost:9090");
});

builder.Services.AddGrpcClient<VoteService.VoteServiceClient>(options =>
{
    options.Address = new Uri("http://localhost:9091");
});

builder.Services.AddGrpcClient<CommentService.CommentServiceClient>(options =>
{
    options.Address = new Uri("http://localhost:9091");
});

builder.Services.AddGrpcClient<ReportService.ReportServiceClient>(options =>
{
    options.Address = new Uri("http://localhost:9091");
});

Console.WriteLine("[SPECTRUM API] Building the application...");
var app = builder.Build();

Console.WriteLine("[SPECTRUM API] Setting up the HTTP request pipeline...");
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();

// app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("[SPECTRUM API] Boot sequence complete. Server is now listening to requests!\n");
Console.ResetColor();

app.Run();