var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

/// <summary>Entry point; exposed so integration tests can host the API with <c>WebApplicationFactory</c>.</summary>
public partial class Program;
