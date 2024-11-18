using Microsoft.AspNetCore.Diagnostics;
using Splitwise_Back.Configurations;
using Splitwise_Back.Data;
using Splitwise_Back.Extensions;


var builder = WebApplication.CreateBuilder(args);




builder.Services.AddAppServices(builder.Configuration);
builder.Services.AddRepositories(builder.Configuration);

builder.Services.ConfigureCloudinary();


var app = builder.Build();
app.MapGet("/", () => "Hello World!");
await app.Services.InitializeDbAsync();







app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseExceptionHandler(handler =>
{
    handler.Run(async context =>
    {
        var exceptionHandler = context.RequestServices.GetRequiredService<IExceptionHandler>();
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;

        if (exception != null)
        {
            await exceptionHandler.TryHandleAsync(context, exception, context.RequestAborted);
        }
    });
});
app.MapControllers();

app.Run();
