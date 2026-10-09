using Paragrafen.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Probably smart to move this logic into its own class later
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [])
            .AllowAnyHeader()
            .AllowAnyMethod()));

var app = builder.Build();

app.UseCors();

app.MapGet("/", () => "Hello World!");

app.MapHealthEndpoints();

app.Run();
