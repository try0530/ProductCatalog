using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using ProductCatalog.Background;
using ProductCatalog.Data;
using ProductCatalog.Middleware;
using ProductCatalog.Services;

var builder = WebApplication.CreateBuilder(args);


// Controllers
builder.Services.AddControllers();

builder.Services.AddScoped<OrderService>();

builder.Services.AddEndpointsApiExplorer();

// Swagger / OpenAPI
const string apiKeySchemeName = "ApiKey";

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "Product Catalog API",
            Version = "v1"
        }
    );

    options.AddSecurityDefinition(
        apiKeySchemeName,
        new OpenApiSecurityScheme
        {
            Name = "X-API-Key",
            Description = "Enter the API key. Default development key: dev-secret",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey
        }
    );

    options.AddSecurityRequirement(
        document =>
            new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(apiKeySchemeName, document)] = []
            }
    );
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=productcatalog.db;Default Timeout=30;Foreign Keys=True";

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

// Application Services
builder.Services.AddSingleton<IWebhookQueue, WebhookQueue>();

builder.Services.AddHostedService<WebhookBackgroundService>();

builder.Services.AddHttpClient("WebhookClient", client => { client.Timeout = TimeSpan.FromSeconds(5); });

// Application
var app = builder.Build();

// Swagger
app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "Product Catalog API v1"
    );
});


// API Key MiddleWware
app.UseMiddleware<ApiKeyMiddleware>();


// Controller
app.MapControllers();

// Database Initialization
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await DatabaseSeeder.SeedAsync(dbContext);
}

// Run on port
app.Run();