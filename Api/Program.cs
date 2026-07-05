using Api;
using Application;
using Infrastructure;
using Infrastructure.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddWebServices();
builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.Host.UseSerilog();

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

var app = builder.Build();

app.UseErrorHandlingMiddleware();

if (app.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
        app.Map("/", () => Results.Redirect("/swagger"));
    });
}

app.UseSerilogRequestLogging();

app.UseHttpsRedirection();

app.UseCors(Api.DependencyInjection.FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
