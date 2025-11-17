using DigitalTriage.Presentation.Common.Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using DigitalTriage.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddInfrastructure(builder.Configuration);

var authBuilder = builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });

// Only add Microsoft Account authentication if ClientId and ClientSecret are configured
var clientId = builder.Configuration["MicrosoftGraph:ClientId"];
var clientSecret = builder.Configuration["MicrosoftGraph:ClientSecret"];

if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
{
    authBuilder.AddMicrosoftAccount(options =>
    {
        options.ClientId = clientId;
        options.ClientSecret = clientSecret;
        options.CallbackPath = "/signin-microsoft";
        options.SaveTokens = true;
        options.Scope.Add("https://graph.microsoft.com/Mail.Send");
        options.Scope.Add("https://graph.microsoft.com/User.Read");
        options.Scope.Add("offline_access");
        
        // Set redirect URI after authentication
        options.Events.OnTicketReceived = async context =>
        {
            // The tokens are saved automatically when SaveTokens = true
            // We'll handle token storage in the controller after redirect
            await Task.CompletedTask;
        };
    });
    
    // Set Microsoft as the default challenge scheme only if configured
    builder.Services.Configure<AuthenticationOptions>(options =>
    {
        options.DefaultChallengeScheme = "Microsoft";
    });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DoctorOnly", policy => policy.RequireRole("Doctor"));
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__RequestVerificationToken";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddHttpClient();
builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var client = factory.CreateClient();
    client.BaseAddress = new Uri(builder.Configuration["BaseUrl"] ?? "https://localhost:7266");
    return client;
});

builder.Services.AddScoped<IAuthHelper, AuthHelper>();
builder.Services.AddScoped<AntiforgeryHelper>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapRazorComponents<DigitalTriage.Presentation.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
