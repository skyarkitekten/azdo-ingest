using Azure.Core;
using Azure.Identity;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.OAuth;
using System.Text.Json;
using Xunit;

namespace Nexus.Ingest.Backlog.Azdo.Test;

/// <summary>
/// Integration tests for AzdoService
/// These tests require real Azure DevOps environment and are marked with custom trait
/// </summary>
[Trait("Category", "Integration")]
public class AzdoServiceIntegrationTests : IDisposable
{
    private readonly string organizationUrl;
    private readonly string projectName;
    private readonly AzdoService? service;

    public AzdoServiceIntegrationTests()
    {
        // Load test configuration from environment variables or testsettings.json
        organizationUrl = Environment.GetEnvironmentVariable("AZDO_ORGANIZATION_URL") ?? GetConfigurationValue("TestConfiguration:OrganizationUrl");
        projectName = Environment.GetEnvironmentVariable("AZDO_PROJECT_NAME") ?? "TestProject";

        // Skip integration tests if not properly configured
        if (string.IsNullOrEmpty(organizationUrl) || organizationUrl.Contains("your-test-org"))
        {
            return;
        }

        try
        {
            // Create service with default Azure credentials using OAuth token
            service = CreateServiceAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // If credential creation fails, tests will be skipped
            service = null;
        }
    }

    private async Task<AzdoService> CreateServiceAsync()
    {
        var credential = new DefaultAzureCredential();
        var tokenRequest = new TokenRequestContext(new[] { "https://app.vssps.visualstudio.com/.default" });
        var tokenResponse = await credential.GetTokenAsync(tokenRequest);
        var credentials = new VssOAuthAccessTokenCredential(tokenResponse.Token);

        return new AzdoService(organizationUrl, credentials);
    }

    private string GetConfigurationValue(string key)
    {
        try
        {
            if (File.Exists("testsettings.json"))
            {
                var json = File.ReadAllText("testsettings.json");
                var config = JsonSerializer.Deserialize<JsonElement>(json);

                var parts = key.Split(':');
                var current = config;

                foreach (var part in parts)
                {
                    if (current.TryGetProperty(part, out var next))
                    {
                        current = next;
                    }
                    else
                    {
                        return string.Empty;
                    }
                }

                return current.GetString() ?? string.Empty;
            }
        }
        catch
        {
            // If configuration loading fails, return empty string
        }

        return string.Empty;
    }

    [Fact]
    public async Task GetNewBugsAsync_WithRealAzureDevOpsConnection_ReturnsWorkItems()
    {
        // Skip if service not initialized (missing configuration)
        if (service == null)
        {
            Skip.If(true, "Integration tests require Azure DevOps configuration");
            return;
        }

        // Act
        var workItems = await service.GetNewBugsAsync(projectName);

        // Assert
        Assert.NotNull(workItems);
        // Note: We don't assert specific count as it depends on actual data
        // The important thing is that the call succeeds without throwing
    }

    [Fact]
    public async Task GetNewBugsAsync_WithInvalidProject_ThrowsException()
    {
        // Skip if service not initialized (missing configuration)
        if (service == null)
        {
            Skip.If(true, "Integration tests require Azure DevOps configuration");
            return;
        }

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => service.GetNewBugsAsync("NonExistentProject"));
    }

    [Fact]
    public async Task GetNewBugsAsync_WithValidProject_CreatesQueryIfNotExists()
    {
        // Skip if service not initialized (missing configuration)
        if (service == null)
        {
            Skip.If(true, "Integration tests require Azure DevOps configuration");
            return;
        }

        // This test verifies that the service can create a query if it doesn't exist
        // We can't easily clean up queries, so this test focuses on the success path

        // Act
        var workItems = await service.GetNewBugsAsync(projectName);

        // Assert
        Assert.NotNull(workItems);
        // The query should now exist - subsequent calls should find it

        // Second call should use existing query
        var workItems2 = await service.GetNewBugsAsync(projectName);
        Assert.NotNull(workItems2);
    }

    [Fact]
    public void Service_CanBeDisposed_WithoutException()
    {
        // Skip if service not initialized (missing configuration)
        if (service == null)
        {
            Skip.If(true, "Integration tests require Azure DevOps configuration");
            return;
        }

        // Act & Assert
        // Should not throw
        service.Dispose();
    }

    public void Dispose()
    {
        service?.Dispose();
    }
}

/// <summary>
/// Helper class for conditional test skipping
/// </summary>
public static class Skip
{
    public static void If(bool condition, string reason)
    {
        if (condition)
        {
            throw new SkipException(reason);
        }
    }
}

/// <summary>
/// Exception thrown to skip a test
/// </summary>
public class SkipException : Exception
{
    public SkipException(string reason) : base(reason)
    {
    }
}
