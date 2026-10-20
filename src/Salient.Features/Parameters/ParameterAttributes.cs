namespace Salient.Features.Parameters;

/// <summary>
/// The increment a UI control should use for a numeric parameter.
/// Combine with <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/> for the bounds.
/// </summary>
/// <param name="step">Positive increment.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class StepAttribute(double step) : Attribute
{
    /// <summary>Positive increment.</summary>
    public double Step { get; } = step > 0 ? step : throw new ArgumentOutOfRangeException(nameof(step));
}

/// <summary>Marks a parameter that the UI hides under an "Advanced" section by default.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AdvancedAttribute : Attribute;
