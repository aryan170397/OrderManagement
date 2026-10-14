using OrderManagement.Api.Configuration;
using OrderManagement.Api.Extensions;
using OrderManagement.Application;
using OrderManagement.Infrastructure;

EnvFileLoader.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration);

var app = builder.Build();
app.UseApiPipeline();
app.Run();

public partial class Program;
