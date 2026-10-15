using System.Text.Json;
using System.Text.Json.Serialization;
using Paragrafen.Api.Endpoints;
using Paragrafen.Api.ExceptionHandlers;
using Azure.AI.OpenAI;
using Azure.Identity;
using Paragrafen.Core.Abstractions;
using Paragrafen.Core.Services;
using Paragrafen.Data.Ai;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
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
builder.Services.AddScoped<IAskService, AskService>();

var app = builder.Build();

app.UseCors();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Paragrafen API"));
}

app.MapHealthEndpoints();
app.MapAskEndpoints();

app.Run();
