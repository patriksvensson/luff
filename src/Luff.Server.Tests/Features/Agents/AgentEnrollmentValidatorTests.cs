using Luff.Server.Features;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Fleet;

public sealed class AgentEnrollmentValidatorTests
{
    [Fact]
    public void Should_Accept_Correct_Secret()
    {
        // Given
        var validator = new AgentEnrollmentValidator(
            Options.Create(
                new AgentEnrollmentOptions
                {
                    Secret = "correct-horse",
                }));

        // When
        var valid = validator.IsValid("correct-horse");

        // Then
        valid.ShouldBeTrue();
    }

    [Fact]
    public void Should_Reject_Incorrect_Secret()
    {
        // Given
        var validator = new AgentEnrollmentValidator(
            Options.Create(
                new AgentEnrollmentOptions
                {
                    Secret = "correct-horse",
                }));

        // When
        var valid = validator.IsValid("battery-staple");

        // Then
        valid.ShouldBeFalse();
    }

    [Fact]
    public void Should_Reject_Empty_Secret()
    {
        // Given
        var validator = new AgentEnrollmentValidator(
            Options.Create(
                new AgentEnrollmentOptions
                {
                    Secret = "correct-horse",
                }));

        // When
        var valid = validator.IsValid("");

        // Then
        valid.ShouldBeFalse();
    }

    [Fact]
    public void Should_Reject_All_Validation_Attempt_If_No_Secret_Is_Configured()
    {
        // Given
        var validator = new AgentEnrollmentValidator(
            Options.Create(
                new AgentEnrollmentOptions
                {
                    Secret = null,
                }));

        // When
        var valid = validator.IsValid("anything");

        // Then
        valid.ShouldBeFalse();
    }
}