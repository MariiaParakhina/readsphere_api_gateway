using Microsoft.AspNetCore.Authentication.Certificate;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
    .AddCertificate(options =>
    {
        options.Events = new CertificateAuthenticationEvents
        {
            OnCertificateValidated = context =>
            {
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigin", builder =>
    {
        builder.WithOrigins("http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
var app = builder.Build();

app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.Headers.Append("Access-Control-Allow-Origin", (string)context.Request.Headers["Origin"]);
        context.Response.Headers.Append("Access-Control-Allow-Headers", "Origin, X-Requested-With, Content-Type, Accept, Authorization");
        context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
        context.Response.StatusCode = 200;
        return;
    }

    await next();
});
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/auth/login") ||
        context.Request.Path.StartsWithSegments("/api/Account/register") ||
        context.Request.Path.StartsWithSegments("/"))
    {
        await next();
        return;
    }

    var jwtHandler = new JwtSecurityTokenHandler();
    var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
    
    if (string.IsNullOrEmpty(token))
    {
        string authorizationHeader = context.Request.Headers["Authorization"].ToString();

        byte[] byteArray = Encoding.UTF8.GetBytes(authorizationHeader);

        Stream stream = new MemoryStream(byteArray);
        context.Response.StatusCode = 401; // Unauthorized
        context.Response.Body = stream;
        return;
    }

    try
    {
        var jwtToken = jwtHandler.ReadJwtToken(token);
        
        var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "nameid");
        var userId = userIdClaim?.Value;
        context.Request.Headers.Append("X-User-Id", userId); 
         
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;  
        await context.Response.WriteAsync(ex.Message);
        return;
    }

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowSpecificOrigin");
app.UseAuthentication();
app.UseAuthorization();
app.MapReverseProxy();

app.Run();