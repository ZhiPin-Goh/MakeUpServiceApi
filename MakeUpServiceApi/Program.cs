using MakeUpServiceApi.AgentTools;
using MakeUpServiceApi.BackgroundServices;
using MakeUpServiceApi.DbSeeder;
using MakeUpServiceApi.GlobalExceptionHandler;
using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Interface_Services;
using MakeUpServiceApi.InterfaceServices;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // enum class serialize as string
        // 这是一个全局设置，所有的 enum 都会被序列化为字符串
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register the global exception handler
// 定义一个全局异常处理器，并将其注册到依赖注入容器中
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Register interfaces and services
// 将接口和服务注册到依赖注入容器中
builder.Services.AddTransient<ITokenService, TokenService>();
builder.Services.AddTransient<IPhotoService, PhotoService>();
builder.Services.AddTransient<INotificationService, NotificationService>();
builder.Services.AddTransient<ITravelFeeService, TravelFeeService>();
builder.Services.AddTransient<IEmailService, EmailService>();
builder.Services.AddTransient<IGoogleCalendarService, GoogleCalendarService>();

// Configure Kestrel and IIS to allow large file uploads (up to 1 GB) (This is image set up)
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodyBufferSize = 1073741824; // 1 GB
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1073741824L; // 1 GB
});

// Configure JWT authentication
// 配置 JWT 认证
builder.Services.AddHttpContextAccessor();

// Configure MemoryCache
// 配置内存缓存
builder.Services.AddMemoryCache();

// Use https redirection
// 定义一个中间件，用于将 HTTP 请求重定向到 HTTPS
builder.Services.AddHttpClient();

builder.Services.AddSwaggerGen(x =>
{
    x.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MakeUpServiceApi",
        Version = "v1",
    });
    x.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    x.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
          {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                },
            },
             new string[] {}
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(option =>
{
    option.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
    };
    option.Events = new JwtBearerEvents
    {
        OnForbidden = context =>
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            var errorResponse = new
            {
                success = false,
                error = "RoleError",
                message = "You do not have permission to access this resource."
            };
            return context.Response.WriteAsJsonAsync(errorResponse);
        },
        OnChallenge = context =>
        {
            context.HandleResponse();
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            var errorResponse = new
            {
                success = false,
                error = "TokenExpired",
                message = "Your token has expired. Please log in again to obtain a new token."
            };
            return context.Response.WriteAsJsonAsync(errorResponse);
        }
    };
});

// Rag ai tool
builder.Services.AddScoped<AgentTools>();

// 429 security rate limiting
// Use the built-in rate limiting middleware to limit the number of requests from a client within a specified time window
builder.Services.AddRateLimiter(options =>
{
    // 1 minute window, 100 requests per IP address
    options.AddPolicy("GlobalPolicy", httpContext =>
         RateLimitPartition.GetFixedWindowLimiter(
             partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
             factory: _ => new FixedWindowRateLimiterOptions
             {
                 PermitLimit = 100,
                 Window = TimeSpan.FromMinutes(1),
                 QueueLimit = 2,
                 QueueProcessingOrder = QueueProcessingOrder.OldestFirst
             }
     ));
    // 1 minute window, 5 requests per IP address for login endpoint
    options.AddPolicy("StrictPolicy", httpContext =>
         RateLimitPartition.GetFixedWindowLimiter(
             partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
             factory: _ => new FixedWindowRateLimiterOptions
             {
                 PermitLimit = 5,
                 Window = TimeSpan.FromMinutes(1)
             }
    ));
    // response when the rate limit is exceeded
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            success = false,
            error = "RateLimitExceeded",
            message = "You have exceeded the allowed number of requests. Please try again later."
        }, token);
    };
});

builder.Services.AddHostedService<IdempotencyClearService>();
builder.Services.AddHostedService<TokenClearService>();
builder.Services.AddHostedService<AuditLogClearService>();
builder.Services.AddHostedService<ReminbersBookingService>();
builder.Services.AddHostedService<TripRemindersService>();
builder.Services.AddHostedService<ClearBookingService>();
builder.Services.AddHostedService<ReminberCompleteService>();
builder.Services.AddHostedService<ClearNotifyService>();

// Configure Entity Framework Core with SQL Server
// 配置 Entity Framework Core 使用 SQL Server 数据库 
builder.Services.AddDbContext<AppDbContext>
    (option => option
  .UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Internal Server Error (500) will be handled by the global exception handler
app.UseExceptionHandler();

// Jwt Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.MapControllers().RequireRateLimiting("GlobalPolicy"); // Apply the global rate limiting policy to all controllers

// run code to seed the database with initial data (e.g., admin users) when the application starts
// 启动时运行代码以使用初始数据（例如管理员用户）填充数据库
using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;
try
{
    await DbSeeder.SeedAdminsAsync(services);
}
catch (Exception ex)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred while seeding the database.");
}


app.Run();
