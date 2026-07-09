using Luff.Server.Features;
using Shouldly;
using Xunit;

namespace Luff.Server.Tests.Volumes;

public sealed class VolumeValidatorTests
{
    [Theory]
    [InlineData("/srv/data", "/data")]
    [InlineData("/home/user/app", "/app")]
    [InlineData("/var/lib/myapp", "/state")]
    [InlineData("appdata", "/var/lib/app")]
    [InlineData("cache_1.2", "/cache")]
    public void Should_Accept_Valid_Mounts(string source, string target)
    {
        // When, Then
        VolumeValidator.Validate(source, target).ShouldBeNull();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/etc")]
    [InlineData("/etc/passwd")]
    [InlineData("/var/run/docker.sock")]
    [InlineData("/var/lib/docker/volumes")]
    [InlineData("/proc/1")]
    [InlineData("/data/../etc")]
    public void Should_Reject_Denied_Bind_Sources(string source)
    {
        // When, Then
        VolumeValidator.Validate(source, "/mnt").ShouldNotBeNull();
    }

    [Theory]
    [InlineData("relative/target")]
    [InlineData("/target/../escape")]
    public void Should_Reject_Invalid_Targets(string target)
    {
        // When, Then
        VolumeValidator.Validate("/srv/data", target).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("bad name")]
    [InlineData("-bad")]
    [InlineData("")]
    public void Should_Reject_Invalid_Volume_Names(string source)
    {
        // When, Then
        VolumeValidator.Validate(source, "/mnt").ShouldNotBeNull();
    }
}
