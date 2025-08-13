using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Nexus.Ingest.Backlog.Azdo.Test;

/// <summary>
/// Unit tests for AzdoSecurityProvider
/// These tests focus on logic, error handling, and parameter validation without making real network calls
/// </summary>
public class AzdoSecurityProviderTests
{
    private const string TestOrganizationUrl = "https://dev.azure.com/testorg";
    private const string TestClientId = "test-client-id";
    private const string TestTenantId = "test-tenant-id";
    private const string TestClientSecret = "test-client-secret";
    private const string TestAccessToken = "test-access-token";
    private const string TestUsername = "test@example.com";
    private const string TestPassword = "test-password";

    [Fact]
    public void CreateEntraConnection_WithValidParameters_ReturnsVssConnection()
    {
        // Act
        var connection = AzdoSecurityProvider.CreateEntraConnection(TestOrganizationUrl, TestAccessToken);

        // Assert
        Assert.NotNull(connection);
        Assert.Equal(new Uri(TestOrganizationUrl), connection.Uri);
        Assert.NotNull(connection.Credentials);
        // Note: VssConnection.Credentials returns base VssCredentials type, not the specific implementation
        Assert.IsAssignableFrom<VssCredentials>(connection.Credentials);
    }

    [Theory]
    [InlineData(null, TestAccessToken)]
    [InlineData("", TestAccessToken)]
    [InlineData("   ", TestAccessToken)]
    [InlineData(TestOrganizationUrl, null)]
    [InlineData(TestOrganizationUrl, "")]
    [InlineData(TestOrganizationUrl, "   ")]
    public void CreateEntraConnection_WithInvalidParameters_ThrowsArgumentException(string? organizationUrl, string? accessToken)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            AzdoSecurityProvider.CreateEntraConnection(organizationUrl!, accessToken!));
    }

    [Fact]
    public void CreateEntraConnection_WithInvalidUrl_ThrowsUriFormatException()
    {
        // Arrange
        const string invalidUrl = "not-a-valid-url";

        // Act & Assert
        Assert.Throws<UriFormatException>(() =>
            AzdoSecurityProvider.CreateEntraConnection(invalidUrl, TestAccessToken));
    }

    [Fact]
    public void CreateEntraUsernameConnection_WithValidParameters_ReturnsVssConnection()
    {
        // Act
        var connection = AzdoSecurityProvider.CreateEntraUsernameConnection(
            TestOrganizationUrl, TestUsername, TestPassword);

        // Assert
        Assert.NotNull(connection);
        Assert.Equal(new Uri(TestOrganizationUrl), connection.Uri);
        Assert.NotNull(connection.Credentials);
        // Note: VssConnection.Credentials returns base VssCredentials type, not the specific implementation
        Assert.IsAssignableFrom<VssCredentials>(connection.Credentials);
    }

    [Theory]
    [InlineData(null, TestUsername, TestPassword)]
    [InlineData("", TestUsername, TestPassword)]
    [InlineData(TestOrganizationUrl, null, TestPassword)]
    [InlineData(TestOrganizationUrl, "", TestPassword)]
    [InlineData(TestOrganizationUrl, TestUsername, null)]
    [InlineData(TestOrganizationUrl, TestUsername, "")]
    public void CreateEntraUsernameConnection_WithInvalidParameters_ThrowsArgumentException(
        string? organizationUrl, string? username, string? password)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() =>
            AzdoSecurityProvider.CreateEntraUsernameConnection(organizationUrl!, username!, password!));
    }

    [Fact]
    public async Task CreateManagedIdentityConnectionAsync_WithInvalidUrl_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            await AzdoSecurityProvider.CreateManagedIdentityConnectionAsync(""));
    }

    [Fact]
    public async Task CreateManagedIdentityConnectionAsync_WithClientId_WithInvalidParameters_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            await AzdoSecurityProvider.CreateManagedIdentityConnectionAsync("", TestClientId));
    }

    [Theory]
    [InlineData(null, TestClientId, TestTenantId, TestClientSecret)]
    [InlineData("", TestClientId, TestTenantId, TestClientSecret)]
    [InlineData(TestOrganizationUrl, null, TestTenantId, TestClientSecret)]
    [InlineData(TestOrganizationUrl, "", TestTenantId, TestClientSecret)]
    [InlineData(TestOrganizationUrl, TestClientId, null, TestClientSecret)]
    [InlineData(TestOrganizationUrl, TestClientId, "", TestClientSecret)]
    [InlineData(TestOrganizationUrl, TestClientId, TestTenantId, null)]
    [InlineData(TestOrganizationUrl, TestClientId, TestTenantId, "")]
    public async Task CreateServicePrincipalSecretConnectionAsync_WithInvalidParameters_ThrowsArgumentException(
        string? organizationUrl, string? clientId, string? tenantId, string? clientSecret)
    {
        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            await AzdoSecurityProvider.CreateServicePrincipalSecretConnectionAsync(
                organizationUrl!, clientId!, tenantId!, clientSecret!));
    }

    [Fact]
    public async Task CreateServicePrincipalConnectionAsync_WithNullCertificate_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await AzdoSecurityProvider.CreateServicePrincipalConnectionAsync(
                TestOrganizationUrl, TestClientId, TestTenantId, null!));
    }

    [Theory]
    [InlineData(null, TestClientId, TestTenantId)]
    [InlineData("", TestClientId, TestTenantId)]
    [InlineData(TestOrganizationUrl, null, TestTenantId)]
    [InlineData(TestOrganizationUrl, "", TestTenantId)]
    [InlineData(TestOrganizationUrl, TestClientId, null)]
    [InlineData(TestOrganizationUrl, TestClientId, "")]
    public async Task CreateServicePrincipalConnectionAsync_WithInvalidStringParameters_ThrowsArgumentException(
        string? organizationUrl, string? clientId, string? tenantId)
    {
        // Arrange
        using var certificate = CreateTestCertificate();

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(async () =>
            await AzdoSecurityProvider.CreateServicePrincipalConnectionAsync(
                organizationUrl!, clientId!, tenantId!, certificate));
    }

    /// <summary>
    /// Creates a test certificate for testing purposes
    /// Note: This creates a self-signed certificate that should only be used for testing
    /// </summary>
    private static X509Certificate2 CreateTestCertificate()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=Test Certificate", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(1));
        return certificate;
    }
}
