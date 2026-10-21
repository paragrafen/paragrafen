using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Paragrafen.Api.Endpoints;
using Paragrafen.Api.ExceptionHandlers;
using Azure.AI.OpenAI;
using Azure.Identity;
using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Prompts;
using Paragrafen.Core.Models;
using Paragrafen.Core.Services;
using Paragrafen.Data;
using Paragrafen.Api.Extensions;
using Paragrafen.Core;

var builder = WebApplication.CreateBuilder(args);

// The build runs Program.cs to generate contracts/openapi.json, without secrets.
// These placeholders only satisfy startup validation; nothing connects or signs with them.
if (Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider")
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Paragrafen"] = "Host=localhost;Database=openapi;Username=openapi;Password=openapi",
        ["Jwt:IssuerSigningKey"] = new string('x', 32),
    });
}


builder.Services.AddData(builder.Configuration);
builder.Services.AddCore();
builder.Services.AddJwtAuth(builder.Configuration, builder.Environment);

builder.Services.AddApiDocumentation();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();


builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

// Probably smart to move this logic into its own class later
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()));

builder.Services.AddSingleton(_ =>
{
    var endpoint = builder.Configuration["Foundry:Endpoint"]
                   ?? throw new InvalidOperationException("Foundry:Endpoint is not configured.");
    var chatDeployment = builder.Configuration["Foundry:ChatClient"]
                     ?? throw new InvalidOperationException("Foundry:ChatClient is not configured.");
    var client = new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential());
    return client.GetChatClient(chatDeployment);
});

builder.Services.AddSingleton<IAiModelService, AiModelService>();
builder.Services.AddSingleton(AskPrompt.Load("v0"));

builder.Services.AddSingleton(_ => ParagrafenDataSource.Create(
    builder.Configuration.GetConnectionString("Paragrafen")
        ?? throw new InvalidOperationException("ConnectionStrings:Paragrafen is not configured."),
    new DefaultAzureCredential()));

builder.Services.AddSingleton(_ =>
{
    var deployment = builder.Configuration["Foundry:ChatClient"]
                     ?? throw new InvalidOperationException("Foundry:ChatClient is not configured.");
    return builder.Configuration.GetSection($"Foundry:Prices:{deployment}").Get<ModelPrice>()
           ?? throw new InvalidOperationException($"Foundry:Prices:{deployment} is not configured.");
});

builder.Services.AddSingleton<ICallLog, CallLog>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IAskService, AskService>();
builder.Services.AddCorsPolicy(builder.Configuration);

var app = builder.Build();

app.UseCors();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapApiDocumentation();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapHealthEndpoints();
app.MapAskEndpoints();

app.Run();

public partial class Program { } // needed for integration tests
