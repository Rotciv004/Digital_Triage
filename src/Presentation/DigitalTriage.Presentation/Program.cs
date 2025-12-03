using DigitalTriage.Presentation.Common.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using DigitalTriage.Infrastructure;
using DigitalTriage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

// Microsoft Account authentication (if configured)
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
    });
    
    builder.Services.Configure<Microsoft.AspNetCore.Authentication.AuthenticationOptions>(options =>
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

// ? AUTO-APPLY MIGRATIONS ON STARTUP
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<MedicalTriageDbContext>();
        var logger = services.GetRequiredService<ILogger<Program>>();
        
        logger.LogInformation("Applying database migrations...");
        context.Database.Migrate(); // Apply all pending migrations
        logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
        // Don't throw - let app start even if DB is not available
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // ? This serves wwwroot files
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.MapRazorComponents<DigitalTriage.Presentation.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();
