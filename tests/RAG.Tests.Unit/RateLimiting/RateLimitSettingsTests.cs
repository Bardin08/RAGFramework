using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RAG.Core.Configuration;
using Shouldly;

namespace RAG.Tests.Unit.RateLimiting;

/// <summary>
/// Unit tests for RateLimitSettings configuration binding (AC: 6).
/// Tests that configuration loads values correctly.
/// </summary>
public class RateLimitSettingsTests
{
    [Fact]
    public void RateLimitSettings_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var settings = new RateLimitSettings();

        // Assert
        settings.Enabled.ShouldBeTrue();
        settings.EnableEndpointRateLimiting.ShouldBeTrue();
        settings.StackBlockedRequests.ShouldBeFalse();
        settings.HttpStatusCode.ShouldBe(429);
        settings.RealIpHeader.ShouldBe("X-Real-IP");
        settings.ClientIdHeader.ShouldBe("X-ClientId");
    }

    [Fact]
    public void RateLimitTiers_DefaultValues_MatchAcceptanceCriteria()
    {
        // Arrange & Act
        var tiers = new RateLimitTiers();

        // Assert - AC 2: Default 100/min, Authenticated 200/min, Admin 500/min
        tiers.Anonymous.ShouldBe(100);
        tiers.Authenticated.ShouldBe(200);
        tiers.Admin.ShouldBe(500);
    }

    [Fact]
    public void RateLimitSettings_IpWhitelist_ContainsLocalhostByDefault()
    {
        // Arrange & Act
        var settings = new RateLimitSettings();

        // Assert
        settings.IpWhitelist.ShouldContain("127.0.0.1");
        settings.IpWhitelist.ShouldContain("::1");
    }

    [Fact]
    public void EndpointRateLimitRule_DefaultPeriod_IsOneMinute()
    {
        // Arrange & Act
        var rule = new EndpointRateLimitRule();

        // Assert
        rule.Period.ShouldBe("1m");
        rule.Endpoint.ShouldBe("*");
    }

    [Fact]
    public void RateLimitSettings_BindsFromConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"RateLimiting:Enabled", "true"},
                {"RateLimiting:HttpStatusCode", "429"},
                {"RateLimiting:Tiers:Anonymous", "50"},
                {"RateLimiting:Tiers:Authenticated", "150"},
                {"RateLimiting:Tiers:Admin", "300"},
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<RateLimitSettings>(configuration.GetSection("RateLimiting"));
        var provider = services.BuildServiceProvider();

        // Act
        var settings = provider.GetRequiredService<IOptions<RateLimitSettings>>().Value;

        // Assert
        settings.Enabled.ShouldBeTrue();
        settings.HttpStatusCode.ShouldBe(429);
        settings.Tiers.Anonymous.ShouldBe(50);
        settings.Tiers.Authenticated.ShouldBe(150);
        settings.Tiers.Admin.ShouldBe(300);
    }

    [Fact]
    public void RateLimitSettings_EndpointRules_BindFromConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"RateLimiting:EndpointRules:0:Endpoint", "post:/api/query"},
                {"RateLimiting:EndpointRules:0:Period", "1m"},
                {"RateLimiting:EndpointRules:0:Limit", "50"},
                {"RateLimiting:EndpointRules:1:Endpoint", "get:/healthz"},
                {"RateLimiting:EndpointRules:1:Period", "1m"},
                {"RateLimiting:EndpointRules:1:Limit", "1000"},
            })
            .Build();

        var services = new ServiceCollection();
        services.Configure<RateLimitSettings>(configuration.GetSection("RateLimiting"));
        var provider = services.BuildServiceProvider();

        // Act
        var settings = provider.GetRequiredService<IOptions<RateLimitSettings>>().Value;

        // Assert
        settings.EndpointRules.Count.ShouldBe(2);

        var queryRule = settings.EndpointRules.First(r => r.Endpoint.Contains("query"));
        queryRule.Limit.ShouldBe(50);
        queryRule.Period.ShouldBe("1m");

        var healthRule = settings.EndpointRules.First(r => r.Endpoint.Contains("healthz"));
        healthRule.Limit.ShouldBe(1000);
    }

    [Fact]
    public void RateLimitSettings_SectionName_IsCorrect()
    {
        // Arrange & Act & Assert
        RateLimitSettings.SectionName.ShouldBe("RateLimiting");
    }

    [Fact]
    public void GeneralRateLimitRule_CanBeInstantiated()
    {
        // Arrange & Act
        var rule = new GeneralRateLimitRule
        {
            Endpoint = "*",
            Period = "1h",
            Limit = 1000
        };

        // Assert
        rule.Endpoint.ShouldBe("*");
        rule.Period.ShouldBe("1h");
        rule.Limit.ShouldBe(1000);
    }
}
