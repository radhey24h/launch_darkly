using FluentAssertions;
using Gateway.Clients;
using Gateway.Models;
using Gateway.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Gateway.Tests;

/// <summary>
/// Unit tests for BackendRouter.
///
/// Testing strategy:
///   - Mock ILaunchDarklyService to control which variation is returned.
///   - Mock IDotNetBackendClient and IPythonBackendClient to avoid network calls.
///   - Assert that the correct backend client is called for each variation.
///
/// This is the most important test in the project:
///   It verifies that the routing logic correctly dispatches to the
///   right backend based on the LaunchDarkly variation.
///
/// By mocking ILaunchDarklyService, these tests run in milliseconds
/// without any network connection to LaunchDarkly or the backends.
/// </summary>
public class BackendRouterTests
{
    // ---------------------------------------------------------------
    // Shared test fixtures (Mocks)
    // ---------------------------------------------------------------
    private readonly Mock<ILaunchDarklyService> _mockLdService;
    private readonly Mock<IDotNetBackendClient> _mockDotNetClient;
    private readonly Mock<IPythonBackendClient> _mockPythonClient;
    private readonly BackendRouter _router;

    public BackendRouterTests()
    {
        _mockLdService = new Mock<ILaunchDarklyService>(MockBehavior.Strict);
        _mockDotNetClient = new Mock<IDotNetBackendClient>(MockBehavior.Strict);
        _mockPythonClient = new Mock<IPythonBackendClient>(MockBehavior.Strict);

        // Use NullLogger — we don't care about log output in unit tests.
        var logger = NullLogger<BackendRouter>.Instance;

        _router = new BackendRouter(
            _mockLdService.Object,
            _mockDotNetClient.Object,
            _mockPythonClient.Object,
            logger);
    }

    // ---------------------------------------------------------------
    // Test: "dotnet" variation → calls .NET backend
    // ---------------------------------------------------------------

    /// <summary>
    /// When LaunchDarkly returns "dotnet", the router should call the
    /// .NET backend client and return its response.
    /// </summary>
    [Fact]
    [Trait("Category", "Routing")]
    public async Task WhenVariationIsDotNet_ShouldCallDotNetBackend()
    {
        // Arrange
        const string userId = "charlie"; // Hypothetical user in the 10% dotnet bucket

        // Mock LaunchDarkly to return "dotnet" variation for this user.
        _mockLdService
            .Setup(ld => ld.GetBackendVariation(userId))
            .Returns("dotnet");

        var expectedResponse = new OrderResponse
        {
            Backend = "dotnet",
            OrderId = 4242,
            Customer = "CHARLIE",
            Message = "Response from .NET Backend",
            Timestamp = DateTime.UtcNow.ToString("O"),
            UserId = userId
        };

        // Mock the .NET client to return the expected response.
        _mockDotNetClient
            .Setup(c => c.GetOrderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _router.RouteOrderRequestAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.SelectedVariation.Should().Be("dotnet");
        result.Data.Backend.Should().Be("dotnet");
        result.Data.OrderId.Should().Be(4242);
        result.ElapsedMs.Should().BeGreaterThanOrEqualTo(0);

        // Verify the .NET client was called exactly once.
        _mockDotNetClient.Verify(
            c => c.GetOrderAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once,
            "DotNetBackendClient should be called exactly once when variation is 'dotnet'");

        // Verify the Python client was NEVER called.
        _mockPythonClient.Verify(
            c => c.GetOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "PythonBackendClient should NOT be called when variation is 'dotnet'");
    }

    // ---------------------------------------------------------------
    // Test: "python" variation → calls Python backend
    // ---------------------------------------------------------------

    /// <summary>
    /// When LaunchDarkly returns "python", the router should call the
    /// Python backend client and return its response.
    ///
    /// This is the 90% case in the initial rollout.
    /// </summary>
    [Fact]
    [Trait("Category", "Routing")]
    public async Task WhenVariationIsPython_ShouldCallPythonBackend()
    {
        // Arrange
        const string userId = "alice"; // User in the 90% python bucket

        _mockLdService
            .Setup(ld => ld.GetBackendVariation(userId))
            .Returns("python");

        var expectedResponse = new OrderResponse
        {
            Backend = "python",
            OrderId = 1337,
            Customer = "ALICE",
            Message = "Response from Python Backend (Mock TIBCO)",
            Timestamp = DateTime.UtcNow.ToString("O"),
            UserId = userId
        };

        _mockPythonClient
            .Setup(c => c.GetOrderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _router.RouteOrderRequestAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.SelectedVariation.Should().Be("python");
        result.Data.Backend.Should().Be("python");
        result.Data.Customer.Should().Be("ALICE");

        // Verify the Python client was called exactly once.
        _mockPythonClient.Verify(
            c => c.GetOrderAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once,
            "PythonBackendClient should be called exactly once when variation is 'python'");

        // Verify the .NET client was NEVER called.
        _mockDotNetClient.Verify(
            c => c.GetOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "DotNetBackendClient should NOT be called when variation is 'python'");
    }

    // ---------------------------------------------------------------
    // Test: Unknown variation falls back to Python (defensive default)
    // ---------------------------------------------------------------

    /// <summary>
    /// If LaunchDarkly returns an unexpected variation string (e.g., "canary"),
    /// the router should default to the Python backend as a safe fallback.
    /// This prevents routing to unknown backends.
    /// </summary>
    // NOTE: "DOTNET" (uppercase) is NOT included here because the router uses
    // OrdinalIgnoreCase comparison, so "DOTNET" correctly routes to the .NET backend.
    // Case-insensitive matching is intentional — LaunchDarkly could theoretically
    // return variations in any casing depending on configuration.
    [Theory]
    [InlineData("unknown")]
    [InlineData("canary")]
    [InlineData("")]
    [Trait("Category", "Routing")]
    public async Task WhenVariationIsUnknown_ShouldFallBackToPythonBackend(string unknownVariation)
    {
        // Arrange
        const string userId = "testuser";

        _mockLdService
            .Setup(ld => ld.GetBackendVariation(userId))
            .Returns(unknownVariation);

        var pythonResponse = new OrderResponse
        {
            Backend = "python",
            OrderId = 999,
            Customer = "TESTUSER",
            Message = "Response from Python Backend (Mock TIBCO)",
            Timestamp = DateTime.UtcNow.ToString("O"),
            UserId = userId
        };

        _mockPythonClient
            .Setup(c => c.GetOrderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pythonResponse);

        // Act
        var result = await _router.RouteOrderRequestAsync(userId);

        // Assert — Python backend used as fallback
        result.Data.Backend.Should().Be("python");

        _mockPythonClient.Verify(
            c => c.GetOrderAsync(userId, It.IsAny<CancellationToken>()),
            Times.Once);

        _mockDotNetClient.Verify(
            c => c.GetOrderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Test: Elapsed time is measured
    // ---------------------------------------------------------------

    /// <summary>
    /// The response should include a non-negative elapsed time,
    /// giving callers visibility into routing + backend call latency.
    /// </summary>
    [Fact]
    [Trait("Category", "Performance")]
    public async Task ShouldMeasureElapsedTime()
    {
        // Arrange
        const string userId = "bob";

        _mockLdService
            .Setup(ld => ld.GetBackendVariation(userId))
            .Returns("python");

        _mockPythonClient
            .Setup(c => c.GetOrderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderResponse { Backend = "python", UserId = userId });

        // Act
        var result = await _router.RouteOrderRequestAsync(userId);

        // Assert
        result.ElapsedMs.Should().BeGreaterThanOrEqualTo(0,
            "ElapsedMs should always be non-negative");
    }

    // ---------------------------------------------------------------
    // Test: Sticky routing — same userId always gets same variation
    // ---------------------------------------------------------------

    /// <summary>
    /// Verifies that calling the router multiple times with the same userId
    /// always evaluates the flag with that userId (enabling sticky routing).
    ///
    /// The actual stickiness is guaranteed by LaunchDarkly's deterministic
    /// hashing — this test verifies our code passes the userId consistently.
    /// </summary>
    [Fact]
    [Trait("Category", "StickyRouting")]
    public async Task SameUserId_ShouldAlwaysPassSameKeyToLaunchDarkly()
    {
        // Arrange
        const string userId = "emma";

        _mockLdService
            .Setup(ld => ld.GetBackendVariation(userId))
            .Returns("python");

        _mockPythonClient
            .Setup(c => c.GetOrderAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderResponse { Backend = "python", UserId = userId });

        // Act — call 5 times with same userId
        for (var i = 0; i < 5; i++)
        {
            await _router.RouteOrderRequestAsync(userId);
        }

        // Assert — LaunchDarkly was always called with the same userId
        _mockLdService.Verify(
            ld => ld.GetBackendVariation(userId),
            Times.Exactly(5),
            "GetBackendVariation should be called with the exact userId each time");
    }

    // ---------------------------------------------------------------
    // Test: Cancellation propagates correctly
    // ---------------------------------------------------------------

    /// <summary>
    /// When the CancellationToken is already cancelled, the backend client
    /// call should be cancelled and the exception should propagate.
    /// </summary>
    [Fact]
    [Trait("Category", "Cancellation")]
    public async Task WhenCancelled_ShouldPropagateTaskCanceledException()
    {
        // Arrange
        const string userId = "david";

        _mockLdService
            .Setup(ld => ld.GetBackendVariation(userId))
            .Returns("dotnet");

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel the token

        _mockDotNetClient
            .Setup(c => c.GetOrderAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<TaskCanceledException>(
            () => _router.RouteOrderRequestAsync(userId, cts.Token));
    }
}
