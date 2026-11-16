using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Pancras.Web.Tests;

public class HealthCheckTests : IClassFixture<PancrasWebFactory>
{
    private readonly PancrasWebFactory _factory;

    public HealthCheckTests(PancrasWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Live_IsAnonymous_AndReportsOnlyHealthy()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ready_WhenDependencyFails_Returns503_WithoutDetails()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    options.Registrations.Clear();
                    options.Registrations.Add(new HealthCheckRegistration(
                        "database", _ => new FailingCheck(), HealthStatus.Unhealthy, ["ready"]));
                })));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal("Unhealthy", body);
    }

    [Fact]
    public async Task Ready_WhenDependenciesPass_Returns200()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    options.Registrations.Clear();
                    options.Registrations.Add(new HealthCheckRegistration(
                        "database", _ => new PassingCheck(), HealthStatus.Unhealthy, ["ready"]));
                })));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    private sealed class FailingCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Unhealthy("secret connection detail"));
    }

    private sealed class PassingCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
            Task.FromResult(HealthCheckResult.Healthy());
    }
}
