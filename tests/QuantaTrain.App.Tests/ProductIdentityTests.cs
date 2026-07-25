namespace QuantaTrain.App.Tests;

public sealed class ProductIdentityTests
{
    [Fact]
    public void PublishedApplicationUsesQuantaTrayIdentity()
    {
        var assembly = typeof(LocalizationService).Assembly;

        Assert.Equal("QuantaTray", assembly.GetName().Name);
        Assert.Equal(
            "QuantaTray",
            assembly.GetCustomAttributesData()
                .Single(attribute =>
                    attribute.AttributeType == typeof(System.Reflection.AssemblyProductAttribute))
                .ConstructorArguments[0]
                .Value);
    }
}
