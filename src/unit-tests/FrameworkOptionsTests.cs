using Sherlock.MCP.Runtime.Configuration;
using Xunit;

namespace Sherlock.MCP.Tests;

public class FrameworkOptionsTests
{
    [Fact]
    public void Constructor_WithNoArguments_DefaultsToModernStack()
    {
        // Arrange & Act
        var options = new FrameworkOptions();

        // Assert
        Assert.Equal(new[] { "net10.0", "net11.0" }, options.TargetFrameworks);
    }

    [Fact]
    public void Constructor_WithNull_DefaultsToModernStack()
    {
        // Arrange & Act
        var options = new FrameworkOptions(null);

        // Assert
        Assert.Equal(new[] { "net10.0", "net11.0" }, options.TargetFrameworks);
    }

    [Fact]
    public void Constructor_WithEmptyArray_DefaultsToModernStack()
    {
        // Arrange & Act
        var options = new FrameworkOptions([]);

        // Assert
        Assert.Equal(new[] { "net10.0", "net11.0" }, options.TargetFrameworks);
    }

    [Fact]
    public void Constructor_WithCustomFrameworks_UsesProvidedValues()
    {
        // Arrange
        var frameworks = new[] { "net6.0", "net7.0", "net8.0" };

        // Act
        var options = new FrameworkOptions(frameworks);

        // Assert
        Assert.Equal(frameworks, options.TargetFrameworks);
    }

    [Fact]
    public void IsFrameworkSupported_WithSupportedFramework_ReturnsTrue()
    {
        // Arrange
        var options = new FrameworkOptions(new[] { "net10.0", "net11.0" });

        // Act & Assert
        Assert.True(options.IsFrameworkSupported("net10.0"));
        Assert.True(options.IsFrameworkSupported("net11.0"));
    }

    [Fact]
    public void IsFrameworkSupported_WithUnsupportedFramework_ReturnsFalse()
    {
        // Arrange
        var options = new FrameworkOptions(new[] { "net10.0", "net11.0" });

        // Act & Assert
        Assert.False(options.IsFrameworkSupported("net8.0"));
        Assert.False(options.IsFrameworkSupported("net9.0"));
    }

    [Fact]
    public void IsFrameworkSupported_CaseInsensitive()
    {
        // Arrange
        var options = new FrameworkOptions(new[] { "net10.0" });

        // Act & Assert
        Assert.True(options.IsFrameworkSupported("NET10.0"));
        Assert.True(options.IsFrameworkSupported("Net10.0"));
        Assert.True(options.IsFrameworkSupported("nEt10.0"));
    }

    [Fact]
    public void SupportedFrameworksDisplay_ReturnsCommaSeparatedList()
    {
        // Arrange
        var options = new FrameworkOptions(new[] { "net8.0", "net10.0", "net11.0" });

        // Act
        var display = options.SupportedFrameworksDisplay;

        // Assert
        Assert.Equal("net8.0, net10.0, net11.0", display);
    }

    [Fact]
    public void LegacyAnalysisScenario_SupportsMultipleFrameworkVersions()
    {
        // Arrange - simulate legacy analysis workflow
        var legacyFrameworks = new[] { "net6.0", "net7.0", "net8.0", "net9.0", "net10.0", "net11.0" };

        // Act
        var options = new FrameworkOptions(legacyFrameworks);

        // Assert
        Assert.True(options.IsFrameworkSupported("net6.0"));
        Assert.True(options.IsFrameworkSupported("net7.0"));
        Assert.True(options.IsFrameworkSupported("net8.0"));
        Assert.True(options.IsFrameworkSupported("net9.0"));
        Assert.True(options.IsFrameworkSupported("net10.0"));
        Assert.True(options.IsFrameworkSupported("net11.0"));
        Assert.Equal("net6.0, net7.0, net8.0, net9.0, net10.0, net11.0", options.SupportedFrameworksDisplay);
    }
}
