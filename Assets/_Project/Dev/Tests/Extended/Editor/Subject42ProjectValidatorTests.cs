#if UNITY_EDITOR
using NUnit.Framework;

public sealed class Subject42ProjectValidatorTests
{

    [Category("Extended")]
    [Test]
    public void ProductionContent_HasNoValidationErrors()
    {
        Subject42ValidationReport report =
            Subject42ProjectValidator.ValidateProject();

        Assert.That(
            report.ErrorCount,
            Is.Zero,
            report.FormatErrors());
    }
}
#endif
