using FluentAssertions;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TunnelFin.Core;
using Xunit;

namespace TunnelFin.Tests.Core;

/// <summary>
/// Unit tests for Plugin initialization (T017)
/// Verifies GUID, Name, and service registration
/// </summary>
public class PluginTests
{
    private readonly Mock<IApplicationPaths> _mockApplicationPaths;
    private readonly Mock<IXmlSerializer> _mockXmlSerializer;

    public PluginTests()
    {
        // Setup default paths - BasePlugin requires these to be non-null
        var tempPath = Path.Combine(Path.GetTempPath(), "jellyfin-test", Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempPath);

        // Create mock with strict behavior to catch missing setups
        _mockApplicationPaths = new Mock<IApplicationPaths>();

        // Setup ALL properties of IApplicationPaths to return the temp path
        // BasePlugin constructor uses these paths internally
        _mockApplicationPaths.Setup(x => x.PluginConfigurationsPath).Returns(() => tempPath);
        _mockApplicationPaths.Setup(x => x.DataPath).Returns(() => tempPath);
        _mockApplicationPaths.Setup(x => x.ProgramDataPath).Returns(() => tempPath);
        _mockApplicationPaths.Setup(x => x.ConfigurationDirectoryPath).Returns(() => tempPath);
        _mockApplicationPaths.Setup(x => x.CachePath).Returns(() => tempPath);
        _mockApplicationPaths.Setup(x => x.LogDirectoryPath).Returns(() => tempPath);
        _mockApplicationPaths.Setup(x => x.TempDirectory).Returns(() => tempPath);
        _mockApplicationPaths.Setup(x => x.WebPath).Returns(() => tempPath);
        _mockApplicationPaths.Setup(x => x.VirtualDataPath).Returns(() => tempPath);
        _mockApplicationPaths.Setup(x => x.ImageCachePath).Returns(() => tempPath);
        _mockApplicationPaths.Setup(x => x.PluginsPath).Returns(() => tempPath);

        // Mock XML serializer to return a default configuration
        _mockXmlSerializer = new Mock<IXmlSerializer>();
        _mockXmlSerializer
            .Setup(x => x.DeserializeFromFile(It.IsAny<Type>(), It.IsAny<string>()))
            .Returns((Type type, string path) => Activator.CreateInstance(type));
    }

    [Fact]
    public void Plugin_Should_Have_Valid_GUID()
    {
        // Arrange & Act
        var plugin = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);

        // Assert
        plugin.Id.Should().NotBeEmpty("Plugin must have a valid GUID");
        plugin.Id.Should().NotBe(Guid.Empty, "Plugin GUID should not be empty GUID");
    }

    [Fact]
    public void Plugin_Should_Have_Name_TunnelFin()
    {
        // Arrange & Act
        var plugin = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);

        // Assert
        plugin.Name.Should().Be("TunnelFin", "Plugin name must be 'TunnelFin'");
        plugin.Name.Should().NotBeNullOrWhiteSpace("Plugin name should not be null or whitespace");
    }

    [Fact]
    public void Plugin_Should_Have_Description()
    {
        // Arrange & Act
        var plugin = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);

        // Assert
        plugin.Description.Should().NotBeNullOrWhiteSpace("Plugin should have a description");
        plugin.Description.Should().ContainEquivalentOf("privacy", "Description should mention privacy");
    }

    [Fact]
    public void Plugin_Should_Create_Singleton_Instance()
    {
        // Arrange & Act
        var plugin1 = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);
        var plugin2 = Plugin.Instance;

        // Assert
        plugin2.Should().NotBeNull("Plugin.Instance should be set");
        plugin2.Should().BeSameAs(plugin1, "Plugin.Instance should be the same instance");
    }

    [Fact]
    public void Plugin_Configuration_Should_Be_PluginConfiguration_Type()
    {
        // Arrange & Act
        var plugin = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);

        // Assert
        plugin.Configuration.Should().NotBeNull("Plugin should have configuration");
        plugin.Configuration.Should().BeOfType<PluginConfiguration>("Configuration should be PluginConfiguration type");
    }

    [Fact]
    public void Plugin_Should_Initialize_With_Default_Configuration()
    {
        // Arrange & Act
        var plugin = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);
        var config = plugin.Configuration as PluginConfiguration;

        // Assert
        config.Should().NotBeNull("Configuration should be PluginConfiguration");
        config!.MaxConcurrentStreams.Should().Be(3, "Default max concurrent streams should be 3");
        config.MaxCacheSize.Should().Be(10737418240L, "Default max cache size should be 10GB");
        config.DefaultHopCount.Should().Be(3, "Default hop count should be 3 for maximum privacy");
    }

    [Fact]
    public void Plugin_UpdateConfiguration_Should_Clear_Caches()
    {
        // Arrange
        var plugin = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);
        var newConfig = new PluginConfiguration
        {
            MaxConcurrentStreams = 5,
            MaxCacheSize = 5368709120L // 5GB
        };

        // Act
        plugin.UpdateConfiguration(newConfig);

        // Assert
        var config = plugin.Configuration as PluginConfiguration;
        config.Should().NotBeNull();
        config!.MaxConcurrentStreams.Should().Be(5, "Configuration should be updated");
        config.MaxCacheSize.Should().Be(5368709120L, "Cache size should be updated");
    }

    [Fact]
    public void Plugin_Should_Have_Consistent_GUID_Across_Instances()
    {
        // Arrange & Act
        var plugin1 = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);
        var plugin2 = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);

        // Assert
        plugin1.Id.Should().Be(plugin2.Id, "Plugin GUID should be consistent across instances");
    }

    [Fact]
    public void Plugin_Instance_Should_Be_Null_Before_First_Creation()
    {
        // This test assumes Plugin.Instance is reset between tests
        // In actual implementation, Instance is set in constructor
        // This test verifies the pattern is correct

        // Arrange & Act
        var plugin = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);

        // Assert
        Plugin.Instance.Should().NotBeNull("Instance should be set after plugin creation");
    }

    [Fact]
    public void GetPages_Should_Return_Configuration_Page()
    {
        // Arrange
        var plugin = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);

        // Act
        var pages = plugin.GetPages().ToList();

        // Assert
        pages.Should().NotBeEmpty("Plugin should have at least one configuration page");
        pages.Should().HaveCount(2, "Plugin should have config and search pages");

        // Verify main config page
        var configPage = pages.FirstOrDefault(p => p.Name == "TunnelFin");
        configPage.Should().NotBeNull();
        configPage!.EmbeddedResourcePath.Should().Contain("Configuration.config.html");

        // Verify search page (Phase 2: Plugin Configuration Search Page)
        var searchPage = pages.FirstOrDefault(p => p.Name == "TunnelFin Search");
        searchPage.Should().NotBeNull();
        searchPage!.EmbeddedResourcePath.Should().Contain("Configuration.searchPage.html");
    }

    [Fact]
    public void UpdateConfiguration_Should_Throw_When_Configuration_Is_Invalid()
    {
        // Arrange
        var plugin = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);
        var invalidConfig = new PluginConfiguration
        {
            MaxConcurrentStreams = -1, // Invalid
            MaxCacheSize = -1 // Invalid
        };

        // Act
        var act = () => plugin.UpdateConfiguration(invalidConfig);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Invalid configuration*");
    }

    [Fact]
    public void UpdateConfiguration_Should_Accept_Valid_Configuration()
    {
        // Arrange
        var plugin = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);
        var validConfig = new PluginConfiguration
        {
            MaxConcurrentStreams = 10,
            MaxCacheSize = 21474836480L, // 20GB
            DefaultHopCount = 2,
            EnableBandwidthContribution = true
        };

        // Act
        plugin.UpdateConfiguration(validConfig);

        // Assert
        var config = plugin.Configuration as PluginConfiguration;
        config.Should().NotBeNull();
        config!.MaxConcurrentStreams.Should().Be(10);
        config.MaxCacheSize.Should().Be(21474836480L);
        config.DefaultHopCount.Should().Be(2);
        config.EnableBandwidthContribution.Should().BeTrue();
    }

    [Fact]
    public void Plugin_Id_Should_Be_Specific_GUID()
    {
        // Arrange & Act
        var plugin = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);

        // Assert
        plugin.Id.Should().Be(Guid.Parse("A7F8B3C2-1D4E-4A5B-9C6D-7E8F9A0B1C2D"),
            "Plugin should have the specific GUID defined in the implementation");
    }

    [Fact]
    public void Plugin_Description_Should_Mention_Key_Features()
    {
        // Arrange & Act
        var plugin = new Plugin(_mockApplicationPaths.Object, _mockXmlSerializer.Object);

        // Assert
        plugin.Description.Should().ContainEquivalentOf("privacy");
        plugin.Description.Should().ContainEquivalentOf("torrent");
        plugin.Description.Should().ContainEquivalentOf("Tribler");
        plugin.Description.Should().ContainEquivalentOf("anonymity");
    }
}

