# Testing Strategy for AzdoSecurityProvider

## Overview

This document outlines the comprehensive testing strategy for the `AzdoSecurityProvider` class, which provides various authentication methods for connecting to Azure DevOps.

## Test Structure

### Unit Tests (`AzdoSecurityProviderTests.cs`)

**Purpose**: Test the logic, parameter validation, and error handling without external dependencies.

**Test Categories**:

1. **Parameter Validation Tests**
   - Tests all public methods with null, empty, and whitespace parameters
   - Verifies appropriate exceptions are thrown
   - Uses `[Theory]` with `[InlineData]` for comprehensive coverage

2. **Input Validation Tests**
   - Tests URL format validation
   - Certificate null reference validation
   - String parameter boundary conditions

3. **Return Value Tests**
   - Verifies correct `VssConnection` objects are returned
   - Validates connection URI is set correctly
   - Confirms correct credential types are used

**Key Features**:

- No external network calls
- Fast execution (< 100ms per test)
- High code coverage
- Deterministic results

### Integration Tests (`AzdoSecurityProviderIntegrationTests.cs`)

**Purpose**: Test actual authentication flows with external Azure services.

**Test Categories**:

1. **Managed Identity Tests**
   - System-assigned managed identity authentication
   - User-assigned managed identity authentication
   - Azure environment dependency validation

2. **Service Principal Tests**
   - Certificate-based authentication
   - Client secret authentication
   - Token acquisition and validation

3. **Access Token Tests**
   - Valid token authentication
   - Invalid token error handling
   - Token expiration scenarios

4. **Performance Tests**
   - Authentication timing validation
   - Concurrent connection testing
   - Resource cleanup verification

**Key Features**:

- Skipped by default (requires configuration)
- Real Azure DevOps API calls
- Environment-dependent
- Comprehensive error scenario testing

## Test Configuration

### Environment Setup

**For Unit Tests** (no setup required):

```bash
dotnet test --filter "Category!=Integration"
```

**For Integration Tests** (requires Azure environment):

1. **Azure Managed Identity Environment**:
   - Deploy to Azure App Service, Function App, or VM
   - Enable system-assigned or user-assigned managed identity
   - Grant Azure DevOps permissions to the managed identity

2. **Service Principal Setup**:
   - Create service principal in Azure AD
   - Configure certificate or client secret
   - Grant appropriate Azure DevOps permissions

3. **Test Configuration**:

   ```json
   {
     "TestConfiguration": {
       "OrganizationUrl": "https://dev.azure.com/your-test-org",
       "ServicePrincipal": {
         "ClientId": "guid",
         "TenantId": "guid", 
         "ClientSecret": "from-keyvault"
       }
     }
   }
   ```

### Running Tests

**Unit Tests Only**:

```bash
dotnet test --filter "FullyQualifiedName~AzdoSecurityProviderTests"
```

**Integration Tests** (requires environment setup):

```bash
# Remove Skip attributes from integration tests first
dotnet test --filter "FullyQualifiedName~AzdoSecurityProviderIntegrationTests"
```

**All Tests**:

```bash
dotnet test
```

## Test Authentication Methods

### 1. Managed Identity Authentication

**Unit Test Coverage**:

- ✅ Parameter validation
- ✅ URL format validation
- ✅ Exception handling structure

**Integration Test Coverage**:

- ✅ System-assigned managed identity (Azure environment)
- ✅ User-assigned managed identity (Azure environment)
- ✅ Token acquisition and validation
- ✅ API call verification

**Test Scenarios**:

```csharp
// Unit test example
[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public async Task CreateManagedIdentityConnectionAsync_WithInvalidUrl_ThrowsArgumentException(string invalidUrl)

// Integration test example  
[Fact(Skip = "Requires Azure environment")]
public async Task CreateManagedIdentityConnectionAsync_InAzureEnvironment_ReturnsValidConnection()
```

### 2. Service Principal Authentication

**Unit Test Coverage**:

- ✅ Parameter validation for all required fields
- ✅ Certificate null reference validation
- ✅ Client secret validation
- ✅ URL and GUID format validation

**Integration Test Coverage**:

- ✅ Certificate-based authentication flow
- ✅ Client secret authentication flow
- ✅ MSAL token acquisition
- ✅ Azure DevOps API connectivity

**Test Scenarios**:

```csharp
// Certificate validation
[Fact]
public async Task CreateServicePrincipalConnectionAsync_WithNullCertificate_ThrowsArgumentNullException()

// Real authentication (integration)
[Fact(Skip = "Requires valid service principal")]
public async Task CreateServicePrincipalConnectionAsync_WithValidCertificate_ReturnsValidConnection()
```

### 3. Access Token Authentication

**Unit Test Coverage**:

- ✅ Token parameter validation
- ✅ Organization URL validation
- ✅ Credential object creation

**Integration Test Coverage**:

- ✅ Valid token authentication
- ✅ Invalid token error handling
- ✅ Token expiration scenarios

### 4. Username/Password Authentication

**Unit Test Coverage**:

- ✅ All parameter validation
- ✅ Credential object creation
- ✅ Connection object validation

**Integration Test Coverage**:

- ✅ Basic authentication flow (if enabled in Azure DevOps)
- ✅ Multi-factor authentication scenarios

## Performance Testing

**Objectives**:

- Authentication completes within 10 seconds
- Concurrent connections work properly
- No memory leaks during repeated authentication

**Test Implementation**:

```csharp
[Fact]
public async Task Authentication_PerformanceTest_CompletesWithinReasonableTime()
{
    var stopwatch = Stopwatch.StartNew();
    // ... authentication code ...
    Assert.True(stopwatch.ElapsedMilliseconds < 10000);
}
```

## Security Testing

**Focus Areas**:

1. **Credential Handling**:
   - No credentials stored in memory longer than necessary
   - Proper disposal of sensitive objects
   - No credential logging

2. **Error Information**:
   - Exceptions don't leak sensitive information
   - Authentication failures are properly logged
   - Appropriate error messages for debugging

3. **Token Management**:
   - Token refresh handling
   - Expired token detection
   - Proper token scope validation

## Continuous Integration

**Pipeline Configuration**:

```yaml
# Unit tests run on every commit
- task: DotNetCoreCLI@2
  displayName: 'Run Unit Tests'
  inputs:
    command: 'test'
    arguments: '--filter "Category!=Integration" --collect:"XPlat Code Coverage"'

# Integration tests run on scheduled builds with proper environment
- task: DotNetCoreCLI@2
  displayName: 'Run Integration Tests'
  condition: and(succeeded(), eq(variables['Build.Reason'], 'Schedule'))
  inputs:
    command: 'test'
    arguments: '--filter "Category=Integration"'
  env:
    AZURE_CLIENT_ID: $(TestServicePrincipalId)
    AZURE_TENANT_ID: $(TestTenantId)
    AZURE_CLIENT_SECRET: $(TestClientSecret)
```

## Test Data Management

**Unit Tests**:

- Use constants for test data
- Generate test certificates dynamically
- No real credentials required

**Integration Tests**:

- Store credentials in Azure Key Vault
- Use Azure DevOps service connections
- Separate test Azure DevOps organization
- Environment-specific configuration

## Coverage Goals

**Code Coverage Targets**:

- Unit tests: >95% line coverage
- Integration tests: 100% happy path coverage
- Combined: >90% branch coverage

**Scenarios Coverage**:

- ✅ All authentication methods
- ✅ All error conditions
- ✅ Performance edge cases
- ✅ Concurrent usage patterns
- ✅ Security scenarios

## Maintenance

**Regular Tasks**:

1. **Certificate Renewal**: Update test certificates before expiration
2. **Token Refresh**: Rotate test access tokens
3. **Dependency Updates**: Keep Azure SDK packages current
4. **Environment Validation**: Verify test Azure DevOps organization access

**Monitoring**:

- Test execution times
- Integration test success rates
- Azure service availability impact on tests

## Troubleshooting

**Common Issues**:

1. **Integration Tests Failing**:
   - Verify Azure environment setup
   - Check managed identity permissions
   - Validate service principal configuration
   - Confirm Azure DevOps organization access

2. **Performance Tests Slow**:
   - Check network connectivity
   - Verify Azure region proximity
   - Monitor Azure service health

3. **Authentication Errors**:
   - Validate tenant IDs and client IDs
   - Check certificate validity
   - Verify Azure DevOps permissions

**Debug Commands**:

```bash
# Run specific test with detailed output
dotnet test --filter "MethodName" --verbosity detailed

# Run tests with diagnostic logging
dotnet test --logger "console;verbosity=diagnostic"
```

This comprehensive testing strategy ensures the `AzdoSecurityProvider` class is robust, secure, and reliable across all supported authentication scenarios.
