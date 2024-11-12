using Splitwise_Back.Configurations;
using Splitwise_Back.Data;
using Splitwise_Back.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppServices(builder.Configuration);
builder.Services.AddRepositories(builder.Configuration);

builder.Services.ConfigureCloudinary();


var app = builder.Build();
await app.Services.InitializeDbAsync();


app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseExceptionHandler();
app.MapControllers();

app.Run();
