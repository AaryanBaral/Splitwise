using Splitwise_Back.Configurations;
using Splitwise_Back.Data;
using Splitwise_Back.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAppServices(builder.Configuration);
builder.Services.AddRepositories(builder.Configuration);

builder.Services.ConfigureCloudinary();


var app = builder.Build();
await app.Services.InitializeDbAsync();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
