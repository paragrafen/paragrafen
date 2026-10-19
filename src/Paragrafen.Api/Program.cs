using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using Paragrafen.Api.Endpoints;
using Paragrafen.Api.ExceptionHandlers;
using Paragrafen.Api.Extensions;
using Paragrafen.Core;
using Paragrafen.Data;

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
