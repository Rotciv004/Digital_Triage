using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Domain.Entities;
using DigitalTriage.Infrastructure.Persistence;
using DigitalTriage.Presentation.Common.Helpers;
using DigitalTriage.Presentation.Controllers;
using DigitalTriage.Tests.Helpers;
using DigitalTriage.Tests.TestFixtures;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace DigitalTriage.Tests.Controllers;

/// <summary>
/// Unit tests for OutlookController.
/// Uses in-memory database to avoid creating garbage in the real database.
/// </summary>
public class OutlookControllerTests : IDisposable
{
    private readonly MedicalTriageDbContext _dbContext;
    private readonly Mock<IFamilyMedicService> _familyMedicServiceMock;
    private readonly Mock<IAuthHelper> _authHelperMock;
    private readonly Mock<ILogger<OutlookController>> _loggerMock;
    private readonly IConfiguration _configuration;
    private readonly OutlookController _controller;
    private readonly HttpContext _httpContext;

    public OutlookControllerTests()
    {
        // Create in-memory database for testing
        var fixture = new DatabaseFixture();
        _dbContext = fixture.CreateDbContext();

        // Setup mocks
        _familyMedicServiceMock = new Mock<IFamilyMedicService>();
        _authHelperMock = new Mock<IAuthHelper>();
        _loggerMock = new Mock<ILogger<OutlookController>>();

        // Setup configuration with default test values
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "MicrosoftGraph:ClientId", "test-client-id" },
            { "MicrosoftGraph:ClientSecret", "test-client-secret" },
            { "MicrosoftGraph:TenantId", "common" }
        });
        _configuration = configBuilder.Build();

        // Create controller
        _controller = new OutlookController(
            _familyMedicServiceMock.Object,
            _authHelperMock.Object,
            _loggerMock.Object,
            _configuration);

        // Setup HttpContext
        _httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContext
        };
    }

    [Fact]
    public void Connect_WithAuthenticatedUser_ReturnsChallenge()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, patient.Id.ToString()),
            new(ClaimTypes.Email, patient.Email)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _httpContext.User = principal;

        _authHelperMock.Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(patient.Id);

        // Act
        var result = _controller.Connect();

        // Assert
        Assert.IsType<ChallengeResult>(result);
    }

    [Fact]
    public void Connect_WithMissingConfiguration_RedirectsWithError()
    {
        // Arrange - Create controller with empty configuration
        var emptyConfigBuilder = new ConfigurationBuilder();
        emptyConfigBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "MicrosoftGraph:ClientId", "" },
            { "MicrosoftGraph:ClientSecret", "" }
        });
        var emptyConfig = emptyConfigBuilder.Build();

        var controllerWithEmptyConfig = new OutlookController(
            _familyMedicServiceMock.Object,
            _authHelperMock.Object,
            _loggerMock.Object,
            emptyConfig);
        controllerWithEmptyConfig.ControllerContext = new ControllerContext
        {
            HttpContext = _httpContext
        };

        // Act
        var result = controllerWithEmptyConfig.Connect();

        // Assert
        Assert.IsType<RedirectResult>(result);
        var redirectResult = (RedirectResult)result;
        Assert.Contains("outlookError=not_configured", redirectResult.Url);
    }

    [Fact]
    public async Task Callback_WithValidTokens_StoresTokenAndRedirects()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, patient.Id.ToString()),
            new(ClaimTypes.Email, patient.Email)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _httpContext.User = principal;

        _authHelperMock.Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(patient.Id);

        // Mock authentication result with tokens
        var authProperties = new AuthenticationProperties();
        var tokens = new List<AuthenticationToken>
        {
            new() { Name = "refresh_token", Value = "test_refresh_token_123" },
            new() { Name = "expires_at", Value = DateTimeOffset.UtcNow.AddDays(90).ToString("O") }
        };
        authProperties.StoreTokens(tokens);

        var authResult = AuthenticateResult.Success(
            new AuthenticationTicket(principal, authProperties, "Microsoft"));

        var authServiceMock = new Mock<IAuthenticationService>();
        authServiceMock.Setup(x => x.AuthenticateAsync(_httpContext, "Microsoft"))
            .ReturnsAsync(authResult);

        _httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(authServiceMock.Object)
            .BuildServiceProvider();

        _familyMedicServiceMock.Setup(x => x.StoreOutlookTokenAsync(
            patient.Id,
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Callback();

        // Assert
        Assert.IsType<RedirectResult>(result);
        var redirectResult = (RedirectResult)result;
        Assert.Contains("outlookConnected=true", redirectResult.Url);
        _familyMedicServiceMock.Verify(x => x.StoreOutlookTokenAsync(
            patient.Id,
            "test_refresh_token_123",
            It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task Callback_WithNoRefreshToken_RedirectsWithError()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, patient.Id.ToString()),
            new(ClaimTypes.Email, patient.Email)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _httpContext.User = principal;

        _authHelperMock.Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(patient.Id);

        // Mock authentication result without refresh token
        var authProperties = new AuthenticationProperties();
        var authResult = AuthenticateResult.Success(
            new AuthenticationTicket(principal, authProperties, "Microsoft"));

        var authServiceMock = new Mock<IAuthenticationService>();
        authServiceMock.Setup(x => x.AuthenticateAsync(_httpContext, "Microsoft"))
            .ReturnsAsync(authResult);

        _httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(authServiceMock.Object)
            .BuildServiceProvider();

        // Act
        var result = await _controller.Callback();

        // Assert
        Assert.IsType<RedirectResult>(result);
        var redirectResult = (RedirectResult)result;
        Assert.Contains("outlookError=no_token", redirectResult.Url);
        _familyMedicServiceMock.Verify(x => x.StoreOutlookTokenAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task Callback_WithFailedAuthentication_RedirectsWithError()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, patient.Id.ToString()),
            new(ClaimTypes.Email, patient.Email)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _httpContext.User = principal;

        _authHelperMock.Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(patient.Id);

        // Mock failed authentication
        var authResult = AuthenticateResult.Fail("Authentication failed");

        var authServiceMock = new Mock<IAuthenticationService>();
        authServiceMock.Setup(x => x.AuthenticateAsync(_httpContext, "Microsoft"))
            .ReturnsAsync(authResult);

        _httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(authServiceMock.Object)
            .BuildServiceProvider();

        // Act
        var result = await _controller.Callback();

        // Assert
        Assert.IsType<RedirectResult>(result);
        var redirectResult = (RedirectResult)result;
        Assert.Contains("outlookError=auth_failed", redirectResult.Url);
    }

    [Fact]
    public async Task Callback_WithTokenStorageFailure_RedirectsWithError()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, patient.Id.ToString()),
            new(ClaimTypes.Email, patient.Email)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _httpContext.User = principal;

        _authHelperMock.Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(patient.Id);

        var authProperties = new AuthenticationProperties();
        var tokens = new List<AuthenticationToken>
        {
            new() { Name = "refresh_token", Value = "test_refresh_token_123" }
        };
        authProperties.StoreTokens(tokens);

        var authResult = AuthenticateResult.Success(
            new AuthenticationTicket(principal, authProperties, "Microsoft"));

        var authServiceMock = new Mock<IAuthenticationService>();
        authServiceMock.Setup(x => x.AuthenticateAsync(_httpContext, "Microsoft"))
            .ReturnsAsync(authResult);

        _httpContext.RequestServices = new ServiceCollection()
            .AddSingleton(authServiceMock.Object)
            .BuildServiceProvider();

        _familyMedicServiceMock.Setup(x => x.StoreOutlookTokenAsync(
            patient.Id,
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(false); // Simulate storage failure

        // Act
        var result = await _controller.Callback();

        // Assert
        Assert.IsType<RedirectResult>(result);
        var redirectResult = (RedirectResult)result;
        Assert.Contains("outlookError=storage_failed", redirectResult.Url);
    }

    [Fact]
    public async Task Disconnect_WithAuthenticatedUser_RemovesToken()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, patient.Id.ToString()),
            new(ClaimTypes.Email, patient.Email)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _httpContext.User = principal;

        _authHelperMock.Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(patient.Id);

        _familyMedicServiceMock.Setup(x => x.StoreOutlookTokenAsync(
            patient.Id,
            string.Empty,
            It.IsAny<DateTimeOffset>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Disconnect();

        // Assert
        Assert.IsType<OkObjectResult>(result);
        var okResult = (OkObjectResult)result;
        var response = okResult.Value;
        Assert.NotNull(response);
        
        _familyMedicServiceMock.Verify(x => x.StoreOutlookTokenAsync(
            patient.Id,
            string.Empty,
            It.IsAny<DateTimeOffset>()), Times.Once);
    }

    [Fact]
    public async Task Disconnect_WithUnauthenticatedUser_ReturnsUnauthorized()
    {
        // Arrange
        _authHelperMock.Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns((int?)null);

        // Act
        var result = await _controller.Disconnect();

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
        _familyMedicServiceMock.Verify(x => x.StoreOutlookTokenAsync(
            It.IsAny<int>(),
            It.IsAny<string>(),
            It.IsAny<DateTimeOffset>()), Times.Never);
    }

    [Fact]
    public async Task Disconnect_WithStorageFailure_ReturnsError()
    {
        // Arrange
        var patient = MockDataBuilder.CreatePatient();
        _dbContext.Patients.Add(patient);
        await _dbContext.SaveChangesAsync();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, patient.Id.ToString()),
            new(ClaimTypes.Email, patient.Email)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        _httpContext.User = principal;

        _authHelperMock.Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(patient.Id);

        _familyMedicServiceMock.Setup(x => x.StoreOutlookTokenAsync(
            patient.Id,
            string.Empty,
            It.IsAny<DateTimeOffset>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.Disconnect();

        // Assert
        Assert.IsType<ObjectResult>(result);
        var objectResult = (ObjectResult)result;
        Assert.Equal(500, objectResult.StatusCode);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}

