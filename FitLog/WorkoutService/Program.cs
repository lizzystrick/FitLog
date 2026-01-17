using Microsoft.EntityFrameworkCore;
using WorkoutService.Data;
using WorkoutService.Logic;
using WorkoutService.Messaging;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<WorkoutDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("WorkoutDb")));
builder.Services.AddControllers();
builder.Services.AddSingleton<RabbitMqPublisher>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();

    // supports both appsettings ("RabbitMq:Host") and docker env ("RabbitMq__Host")
    var host = config["RabbitMq__Host"] ?? config["RabbitMq:Host"] ?? "localhost";

    return new RabbitMqPublisher(host);
});

builder.Services.AddSingleton<IEventPublisher>(sp =>
    sp.GetRequiredService<RabbitMqPublisher>());
var jwtSection = builder.Configuration.GetSection("Jwt");
var issuer = jwtSection["Issuer"];
var audience = jwtSection["Audience"];
var key = jwtSection["Key"];

builder.Services.AddHostedService<UserDeletedConsumer>();
builder.Services.AddHostedService<WorkoutUploadedQueueInitializer>();
builder.Services.AddScoped<WorkoutServiceLogic>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WorkoutService",
        Version = "v1"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Gebruik: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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
            Array.Empty<string>()
        }
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,

            ValidateAudience = true,
            ValidAudience = audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Swagger aan in Development

app.UseSwagger();
app.UseSwaggerUI();


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

//app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();


app.UseHttpMetrics(); // meet automatisch HTTP requests
app.MapMetrics();   
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();
public partial class Program { }
