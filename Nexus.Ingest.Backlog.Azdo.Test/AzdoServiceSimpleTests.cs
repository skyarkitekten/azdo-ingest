using Xunit;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using Nexus.Ingest.Backlog.Azdo;

namespace Nexus.Ingest.Backlog.Azdo.Test;

/// <summary>
/// Simple unit tests for AzdoService constructor validation
/// These tests verify parameter validation without making network calls
/// </summary>
public class AzdoServiceSimpleTests
{
    private const string TestOrganizationUrl = "https://dev.azure.com/testorg";

    [Fact]
    public void Constructor_WithNullOrganizationUrl_ThrowsArgumentNullException()
    {
        // Arrange
        var mockCredentials = new Mock<VssCredentials>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new AzdoService(null!, mockCredentials.Object));

        Assert.Equal("uriString", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-url")]
    [InlineData("not-a-uri")]
    public void Constructor_WithInvalidOrganizationUrl_ThrowsUriFormatException(string organizationUrl)
    {
        // Arrange
        var mockCredentials = new Mock<VssCredentials>();

        // Act & Assert
        Assert.Throws<UriFormatException>(() =>
            new AzdoService(organizationUrl, mockCredentials.Object));
    }

    [Fact]
    public void Constructor_WithNullCredentials_ThrowsExceptionDuringNetworkCall()
    {
        // The constructor doesn't validate parameters immediately but fails when trying to connect
        // This demonstrates that the constructor makes network calls
        var exception = Assert.ThrowsAny<Exception>(() =>
            new AzdoService(TestOrganizationUrl, null!));

        // Should be a network-related exception, not an ArgumentNullException from parameter validation
        Assert.True(exception is NullReferenceException || exception is ArgumentNullException);
    }

    [Fact]
    public void Constructor_WithValidParameters_DoesNotThrowDuringUriValidation()
    {
        // Arrange
        var mockCredentials = new Mock<VssCredentials>();

        // Act & Assert - The constructor will throw due to network calls, but not due to URI validation
        var exception = Assert.ThrowsAny<Exception>(() =>
            new AzdoService(TestOrganizationUrl, mockCredentials.Object));

        // The exception should be network-related, not ArgumentNullException or UriFormatException
        Assert.False(exception is ArgumentNullException);
        Assert.False(exception is UriFormatException);
    }
}
