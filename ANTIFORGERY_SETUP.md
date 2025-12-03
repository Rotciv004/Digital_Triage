# Complete Antiforgery Token Setup Guide for .NET 8 Blazor Server

This comprehensive guide provides step-by-step instructions for implementing antiforgery token protection in your Blazor Server application.

## Table of Contents

1. [Overview](#overview)
2. [Step 1: Configure Antiforgery in Program.cs](#step-1-configure-antiforgery-in-programcs)
3. [Step 2: Add JavaScript Helper](#step-2-add-javascript-helper)
4. [Step 3: Create Antiforgery Token Endpoint](#step-3-create-antiforgery-token-endpoint)
5. [Step 4: Create Antiforgery Helper Class](#step-4-create-antiforgery-helper-class)
6. [Step 5: Using Antiforgery in Blazor Components](#step-5-using-antiforgery-in-blazor-components)
7. [Step 6: Secure API Controller Example](#step-6-secure-api-controller-example)
8. [Testing](#testing)

---

## Overview

Antiforgery tokens protect your application against Cross-Site Request Forgery (CSRF) attacks. In .NET 8 Blazor Server:

- **EditForm components** automatically handle antiforgery tokens when you use `<AntiforgeryToken />`
- **HTTP POST requests from JavaScript or HttpClient** require manual token handling

This guide covers both scenarios.

---

## Step 1: Configure Antiforgery in Program.cs

Update your `Program.cs` to configure antiforgery with a custom header name:

```csharp
// Configure Antiforgery with custom header
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN"; // Custom header name
    options.Cookie.Name = "__RequestVerificationToken";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() 
        ? CookieSecurePolicy.SameAsRequest 
        : CookieSecurePolicy.Always;
});

// Add HttpClient for API calls
builder.Services.AddHttpClient();
builder.Services.AddScoped<HttpClient>(serviceProvider =>
{
    var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();
    var client = factory.CreateClient();
    client.BaseAddress = new Uri(builder.Configuration["BaseUrl"] ?? "https://localhost:7266");
    return client;
});

// Antiforgery helper
builder.Services.AddScoped<AntiforgeryHelper>();

// ... existing code ...

// Map API Controllers (for antiforgery token endpoint)
app.MapControllers();
```

**Key Points:**
- `HeaderName = "X-CSRF-TOKEN"` sets the custom header for AJAX requests
- Cookie configuration ensures secure token storage
- `MapControllers()` is needed for the token endpoint

---

## Step 2: Add JavaScript Helper

Add the antiforgery JavaScript helper to `Components/App.razor`:

```html
<!-- Antiforgery Token Helper -->
<script src="js/antiforgery.js"></script>
```

The helper file `wwwroot/js/antiforgery.js` provides:
- `getAntiforgeryToken()` - Gets token from page/cookie
- `fetchAntiforgeryToken()` - Fetches token from server endpoint
- `postWithAntiforgery()` - Makes POST requests with token automatically

---

## Step 3: Create Antiforgery Token Endpoint

Create `Controllers/AntiforgeryController.cs`:

```csharp
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTriage.Presentation.Controllers
{
    [ApiController]
    [Route("antiforgery")]
    public class AntiforgeryController : ControllerBase
    {
        private readonly IAntiforgery _antiforgery;

        public AntiforgeryController(IAntiforgery antiforgery)
        {
            _antiforgery = antiforgery;
        }

        [HttpGet("token")]
        public IActionResult GetToken()
        {
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            return Ok(new { token = tokens.RequestToken });
        }
    }
}
```

This endpoint provides tokens to JavaScript clients.

---

## Step 4: Create Antiforgery Helper Class

Create `Helpers/AntiforgeryHelper.cs`:

```csharp
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.JSInterop;

namespace DigitalTriage.Presentation.Common.Helpers
{
    public class AntiforgeryHelper
    {
        private readonly IJSRuntime _jsRuntime;
        private readonly IAntiforgery _antiforgery;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AntiforgeryHelper(
            IJSRuntime jsRuntime,
            IAntiforgery antiforgery,
            IHttpContextAccessor httpContextAccessor)
        {
            _jsRuntime = jsRuntime;
            _antiforgery = antiforgery;
            _httpContextAccessor = httpContextAccessor;
        }

        public string GetToken()
        {
            var tokens = _antiforgery.GetAndStoreTokens(_httpContextAccessor.HttpContext!);
            return tokens.RequestToken!;
        }

        public async Task<string?> GetTokenFromJavaScriptAsync()
        {
            try
            {
                var token = await _jsRuntime.InvokeAsync<string>("BlazorAntiforgery.getToken");
                return token;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> FetchTokenAsync()
        {
            try
            {
                var token = await _jsRuntime.InvokeAsync<string>("BlazorAntiforgery.fetchToken");
                return token;
            }
            catch
            {
                return null;
            }
        }
    }
}
```

---

## Step 5: Using Antiforgery in Blazor Components

### Method 1: Using AntiforgeryHelper (Recommended)

```csharp
@page "/example"
@using System.Net.Http
@inject HttpClient Http
@inject AntiforgeryHelper AntiforgeryHelper

<button @onclick="UpdateData">Update</button>

@code {
    private async Task UpdateData()
    {
        var token = AntiforgeryHelper.GetToken();
        
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ExampleApi/update");
        request.Headers.Add("X-CSRF-TOKEN", token);
        request.Content = JsonContent.Create(new { Name = "Test", Value = "Data" });

        var response = await Http.SendAsync(request);
        // Handle response...
    }
}
```

### Method 2: Using JavaScript Interop

```csharp
@inject IJSRuntime JSRuntime

private async Task UpdateData()
{
    var token = await JSRuntime.InvokeAsync<string>("BlazorAntiforgery.fetchToken");
    
    Http.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
    Http.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);

    var response = await Http.PostAsJsonAsync("/api/ExampleApi/update", 
        new { Name = "Test", Value = "Data" });
}
```

### Method 3: Using EditForm (Automatic)

```razor
<EditForm Model="model" OnValidSubmit="HandleSubmit">
    <AntiforgeryToken />
    <DataAnnotationsValidator />
    <!-- form fields -->
    <button type="submit">Submit</button>
</EditForm>
```

The `<AntiforgeryToken />` component automatically handles tokens for EditForm submissions.

---

## Step 6: Secure API Controller Example

Create `Controllers/ExampleApiController.cs`:

```csharp
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalTriage.Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ExampleApiController : ControllerBase
    {
        [HttpPost("update")]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateData([FromBody] UpdateRequest request)
        {
            // Token validation is automatic with [ValidateAntiForgeryToken]
            return Ok(new { message = "Data updated successfully" });
        }

        [HttpPost("update-manual")]
        public async Task<IActionResult> UpdateDataManual([FromBody] UpdateRequest request)
        {
            // Manual validation
            var antiforgery = HttpContext.RequestServices.GetRequiredService<IAntiforgery>();
            try
            {
                await antiforgery.ValidateRequestAsync(HttpContext);
            }
            catch
            {
                return BadRequest(new { error = "Invalid antiforgery token" });
            }

            return Ok(new { message = "Data updated successfully" });
        }
    }
}
```

**Key Points:**
- `[ValidateAntiForgeryToken]` attribute automatically validates tokens
- Token must be in the `X-CSRF-TOKEN` header (as configured in Program.cs)
- Manual validation is also possible using `IAntiforgery.ValidateRequestAsync()`

---

## Testing

1. **Test EditForm submission:**
   - Forms with `<AntiforgeryToken />` should work automatically
   - No additional code needed

2. **Test HttpClient POST:**
   - Visit `/example-httpclient` to see working examples
   - Check browser console for any errors
   - Verify API endpoints receive the `X-CSRF-TOKEN` header

3. **Test API endpoint:**
   - Use Postman or curl to send POST requests
   - Include `X-CSRF-TOKEN` header with token value
   - Get token from `/antiforgery/token` endpoint

---

## Troubleshooting

### Error: "A valid antiforgery token was not provided"

**Solutions:**
1. Ensure `<AntiforgeryToken />` is in EditForm
2. Verify `X-CSRF-TOKEN` header is included in HttpClient requests
3. Check that token is obtained from `/antiforgery/token` endpoint
4. Ensure `app.UseAntiforgery()` is called in Program.cs

### Token not found in JavaScript

**Solutions:**
1. Verify `js/antiforgery.js` is loaded
2. Check browser console for errors
3. Ensure cookies are enabled
4. Verify CORS settings if calling from different domain

### Token validation fails

**Solutions:**
1. Ensure cookie and header token match (same request)
2. Check that `HeaderName` in Program.cs matches the header you're sending
3. Verify token isn't expired (tokens expire after some time)

---

## Summary

Antiforgery is configured with custom header `X-CSRF-TOKEN`
JavaScript helpers are available via `BlazorAntiforgery` object
AntiforgeryHelper class provides server-side token access
API controllers validate tokens with `[ValidateAntiForgeryToken]`
EditForms work automatically with `<AntiforgeryToken />`

Your application is now protected against CSRF attacks!
