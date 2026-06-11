using Microsoft.AspNetCore.Authentication.JwtBearer;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddCors(options  =>
{
    options.AddPolicy("Default", policy =>
    {
       policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod(); 
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Host.UseSerilog((context, config) => 
    config.ReadFrom.Configuration(context.Configuration));

var app = builder.Build();



// PHASE 2


app.UseSerilogRequestLogging();
app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapOpenApi();
app.MapScalarApiReference();
app.MapControllers();

//app.UseHttpsRedirection();

app.Run();

