using Microsoft.AspNetCore.Authentication.Certificate;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.WebHost.ConfigureKestrel((context, options) =>
{
    var certPath = "./cert/certificate.pfx";
    options.ListenAnyIP(64212, listenOptions =>
    {
        listenOptions.UseHttps(certPath, "pass");
    });
});

// Configure certificate authentication
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
builder.Services.AddCors((options) =>
{
    options.AddDefaultPolicy(
        corsPolicyBuilder =>
        {
            corsPolicyBuilder.WithOrigins("*")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});
var app = builder.Build();
app.Use(async (context, next) =>
{
    // Skip for paths that don't require authentication
    if (context.Request.Path.StartsWithSegments("/api/auth/login") ||
        context.Request.Path.StartsWithSegments("/api/Account/register"))
    {
        await next();
        return;
    }

    var jwtHandler = new JwtSecurityTokenHandler();
    var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

    // Check if token is present
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
        // Validate the token
        var jwtToken = jwtHandler.ReadJwtToken(token);
        
        var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "nameid");
        var userId = userIdClaim?.Value;
        context.Request.Headers.Append("X-User-Id", userId); 
         
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 401;  
        await context.Response.WriteAsync(ex.Message);
        return;
    }

    await next();
});
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();  
// app.UseHsts(); 
app.MapReverseProxy();

app.Run();