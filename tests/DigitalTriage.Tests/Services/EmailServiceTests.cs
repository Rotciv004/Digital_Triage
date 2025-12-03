using DigitalTriage.Application.Contracts.Services;
using DigitalTriage.Infrastructure.Services;
using DigitalTriage.Tests.TestFixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace DigitalTriage.Tests.Services;

public class EmailServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;

    public EmailServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>());
        _configuration = configBuilder.Build();

        var logger = TestLoggerFactory.Create<EmailService>();
        _emailService = new EmailService(
            _httpClientFactoryMock.Object,
            logger,
            _configuration);
    }

    [Fact]
    public async Task SendFamilyMedicRequestNotificationAsync_WithValidData_ReturnsTrue()
    {
        // Arrange
        var doctorEmail = "doctor@test.com";
        var patientName = "John Doe";
        var patientEmail = "patient@test.com";

        // Act
        var result = await _emailService.SendFamilyMedicRequestNotificationAsync(
            doctorEmail,
            patientName,
            patientEmail);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SendProblemToFamilyMedicAsync_WithValidData_SendsEmail()
    {
        // Arrange
        var patientEmail = "patient@test.com";
        var doctorEmail = "doctor@test.com";
        var accessToken = "test_access_token";
        var issueTitle = "Test Issue";
        var issueDescription = "Test Description";
        var problemType = "General";
        var emergencyGrade = 3;

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        // Act
        var result = await _emailService.SendProblemToFamilyMedicAsync(
            patientEmail,
            doctorEmail,
            accessToken,
            issueTitle,
            issueDescription,
            problemType,
            emergencyGrade);

        // Assert
        Assert.True(result);
        
        mockHttpMessageHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString().Contains("graph.microsoft.com") &&
                req.Headers.Authorization!.Scheme == "Bearer" &&
                req.Headers.Authorization.Parameter == accessToken),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendProblemToFamilyMedicAsync_WithHttpError_ReturnsFalse()
    {
        // Arrange
        var patientEmail = "patient@test.com";
        var doctorEmail = "doctor@test.com";
        var accessToken = "test_access_token";
        var issueTitle = "Test Issue";
        var issueDescription = "Test Description";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("Error", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        // Act
        var result = await _emailService.SendProblemToFamilyMedicAsync(
            patientEmail,
            doctorEmail,
            accessToken,
            issueTitle,
            issueDescription);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendProblemToFamilyMedicAsync_WithException_ReturnsFalse()
    {
        // Arrange
        var patientEmail = "patient@test.com";
        var doctorEmail = "doctor@test.com";
        var accessToken = "test_access_token";
        var issueTitle = "Test Issue";
        var issueDescription = "Test Description";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        // Act
        var result = await _emailService.SendProblemToFamilyMedicAsync(
            patientEmail,
            doctorEmail,
            accessToken,
            issueTitle,
            issueDescription);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task SendProblemToFamilyMedicAsync_WithNullOptionalParameters_StillSendsEmail()
    {
        // Arrange
        var patientEmail = "patient@test.com";
        var doctorEmail = "doctor@test.com";
        var accessToken = "test_access_token";
        var issueTitle = "Test Issue";
        var issueDescription = "Test Description";

        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        // Act
        var result = await _emailService.SendProblemToFamilyMedicAsync(
            patientEmail,
            doctorEmail,
            accessToken,
            issueTitle,
            issueDescription,
            problemType: null,
            emergencyGrade: null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task SendProblemToFamilyMedicAsync_ValidatesEmailContent()
    {
        // Arrange
        var patientEmail = "patient@test.com";
        var doctorEmail = "doctor@test.com";
        var accessToken = "test_access_token";
        var issueTitle = "Test Issue";
        var issueDescription = "Test Description with <script>alert('xss')</script>";

        var capturedRequest = new HttpRequestMessage();
        var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
        mockHttpMessageHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
            {
                capturedRequest = req;
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var httpClient = new HttpClient(mockHttpMessageHandler.Object);
        _httpClientFactoryMock.Setup(x => x.CreateClient(It.IsAny<string>()))
            .Returns(httpClient);

        // Act
        var result = await _emailService.SendProblemToFamilyMedicAsync(
            patientEmail,
            doctorEmail,
            accessToken,
            issueTitle,
            issueDescription);

        // Assert
        Assert.True(result);
        
        var content = await capturedRequest.Content!.ReadAsStringAsync();
        var emailData = JsonSerializer.Deserialize<JsonElement>(content);
        
        // Verify HTML is escaped (script tag should be escaped)
        var bodyContent = emailData.GetProperty("message").GetProperty("body").GetProperty("content").GetString();
        Assert.NotNull(bodyContent);
        Assert.Contains("&lt;script&gt;", bodyContent); // HTML should be escaped
    }
}

