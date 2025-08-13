using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.OAuth;
using Microsoft.VisualStudio.Services.WebApi;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Nexus.Ingest.Backlog.Azdo;

public static class AzdoSecurityProvider
{
    /// <summary>
    /// Creates a connection using Managed Identity.
    /// Used for Azure-hosted apps. No credentials to manage - Azure handles everything automatically
    /// </summary>
    /// <param name="organizationUrl"></param>
    /// <returns cref="VssConnection">A connection to Azure DevOps.</returns>
    /// <exception cref="AuthenticationException"></exception>
    /// <exception cref="ArgumentException"></exception>
    public static async Task<VssConnection> CreateManagedIdentityConnectionAsync(string organizationUrl)
    {
        if (string.IsNullOrWhiteSpace(organizationUrl))
        {
            throw new ArgumentException("Organization URL cannot be null or empty.", nameof(organizationUrl));
        }

        try
        {
            var credential = new ManagedIdentityCredential();
            var tokenRequest = new TokenRequestContext(new[] { "https://app.vssps.visualstudio.com/.default" });
            var tokenResponse = await credential.GetTokenAsync(tokenRequest);

            var credentials = new VssOAuthAccessTokenCredential(tokenResponse.Token);
            return new VssConnection(new Uri(organizationUrl), credentials);
        }
        catch (Exception ex)
        {
            throw new AuthenticationException($"Failed to authenticate with managed identity: {ex.Message}", ex);
        }
    }

    public static async Task<VssConnection> CreateManagedIdentityConnectionAsync(
        string organizationUrl,
        string managedIdentityClientId)
    {
        if (string.IsNullOrWhiteSpace(organizationUrl))
            throw new ArgumentException("Organization URL cannot be null or empty.", nameof(organizationUrl));
        if (string.IsNullOrWhiteSpace(managedIdentityClientId))
            throw new ArgumentException("Managed identity client ID cannot be null or empty.", nameof(managedIdentityClientId));

        try
        {
            var credential = new ManagedIdentityCredential(managedIdentityClientId);
            var tokenRequest = new TokenRequestContext(new[] { "https://app.vssps.visualstudio.com/.default" });
            var tokenResponse = await credential.GetTokenAsync(tokenRequest);

            var credentials = new VssOAuthAccessTokenCredential(tokenResponse.Token);
            return new VssConnection(new Uri(organizationUrl), credentials);
        }
        catch (Exception ex)
        {
            throw new AuthenticationException($"Failed to authenticate with managed identity: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Authenticate using Microsoft Entra ID credentials
    /// Recommended for interactive applications and modern authentication scenarios
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    public static VssConnection CreateEntraConnection(string organizationUrl, string accessToken)
    {
        if (string.IsNullOrWhiteSpace(organizationUrl))
            throw new ArgumentException("Organization URL cannot be null or empty.", nameof(organizationUrl));
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ArgumentException("Access token cannot be null or empty.", nameof(accessToken));

        // Use Microsoft Entra access token for authentication
        var credentials = new VssOAuthAccessTokenCredential(accessToken);
        return new VssConnection(new Uri(organizationUrl), credentials);
    }

    /// <summary>
    /// For username/password scenarios (less secure, avoid when possible)
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    public static VssConnection CreateEntraUsernameConnection(string organizationUrl, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(organizationUrl))
            throw new ArgumentException("Organization URL cannot be null or empty.", nameof(organizationUrl));
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty.", nameof(username));
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        var credentials = new VssAadCredential(username, password);
        return new VssConnection(new Uri(organizationUrl), credentials);
    }

    /// <summary>
    /// Authenticate using service principal with certificate (most secure)
    /// Recommended for production automation scenarios
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="AuthenticationException"></exception>
    public static async Task<VssConnection> CreateServicePrincipalConnectionAsync(
        string organizationUrl,
        string clientId,
        string tenantId,
        X509Certificate2 certificate)
    {
        if (string.IsNullOrWhiteSpace(organizationUrl))
            throw new ArgumentException("Organization URL cannot be null or empty.", nameof(organizationUrl));
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID cannot be null or empty.", nameof(tenantId));
        if (certificate is null)
            throw new ArgumentNullException(nameof(certificate));

        try
        {
            // Create confidential client application with certificate
            var app = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithCertificate(certificate)
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
                .Build();

            // Acquire token for Azure DevOps
            var result = await app
                .AcquireTokenForClient(new[] { "https://app.vssps.visualstudio.com/.default" })
                .ExecuteAsync();

            // Create connection with acquired token
            var credentials = new VssOAuthAccessTokenCredential(result.AccessToken);
            return new VssConnection(new Uri(organizationUrl), credentials);
        }
        catch (Exception ex)
        {
            throw new AuthenticationException($"Failed to authenticate service principal: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Service principal with client secret (less secure than certificate)
    /// </summary>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="AuthenticationException"></exception>
    public static async Task<VssConnection> CreateServicePrincipalSecretConnectionAsync(
        string organizationUrl,
        string clientId,
        string tenantId,
        string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(organizationUrl))
            throw new ArgumentException("Organization URL cannot be null or empty.", nameof(organizationUrl));
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentException("Client ID cannot be null or empty.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID cannot be null or empty.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentException("Client secret cannot be null or empty.", nameof(clientSecret));

        try
        {
            var app = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithClientSecret(clientSecret)
                .WithAuthority(new Uri($"https://login.microsoftonline.com/{tenantId}"))
                .Build();

            var result = await app
                .AcquireTokenForClient(new[] { "https://app.vssps.visualstudio.com/.default" })
                .ExecuteAsync();

            var credentials = new VssOAuthAccessTokenCredential(result.AccessToken);
            return new VssConnection(new Uri(organizationUrl), credentials);
        }
        catch (Exception ex)
        {
            throw new AuthenticationException($"Failed to authenticate service principal with secret: {ex.Message}", ex);
        }
    }
}