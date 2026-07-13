#:sdk Cake.Sdk@6.2.0

////////////////////////////////////////////////////////////////
// Arguments

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var linting = HasArgument("lint");

////////////////////////////////////////////////////////////////
// Settings

var testSettings = new DotNetTestSettings
{
    Configuration = configuration,
    Verbosity = DotNetVerbosity.Minimal,
    NoLogo = true,
    NoRestore = true,
    NoBuild = true,
};

////////////////////////////////////////////////////////////////
// Tasks

Task("Clean")
    .Does(ctx =>
{
    ctx.CleanDirectory("./artifacts");
});

Task("Lint")
    .WithCriteria(() => linting)
    .Does(ctx =>
{
    ctx.DotNetFormatStyle("./src/Luff.slnx", new DotNetFormatSettings
    {
        VerifyNoChanges = true,
    });
});

Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Lint")
    .Does(ctx =>
{
    ctx.DotNetBuild("./src/Luff.slnx", new DotNetBuildSettings
    {
        Configuration = configuration,
        Verbosity = DotNetVerbosity.Minimal,
        NoLogo = true,
        NoIncremental = ctx.HasArgument("rebuild"),
        MSBuildSettings = new DotNetMSBuildSettings()
            .TreatAllWarningsAs(MSBuildTreatAllWarningsAs.Error)
    });
});

Task("Test")
    .IsDependentOn("Build")
    .Does(ctx =>
{
    foreach (var project in ctx.GetFiles("./src/*.Tests/*.Tests.csproj"))
    {
        ctx.DotNetTest(project.FullPath, testSettings);
    }
});

Task("Package")
    .IsDependentOn("Build")
    .Does(ctx =>
{
    var runtimes = new[]
    {
        "linux-x64",
        "linux-arm64",
    };

    var components = new[]
    {
        (Name: "server", Project: "./src/Luff.Server/Luff.Server.csproj"),
        (Name: "agent", Project: "./src/Luff.Agent/Luff.Agent.csproj"),
    };

    foreach (var rid in runtimes)
    {
        foreach (var component in components)
        {
            ctx.DotNetPublish(component.Project, new DotNetPublishSettings
            {
                Configuration = configuration,
                Runtime = rid,
                SelfContained = true,
                OutputDirectory = $"./artifacts/bin/{component.Name}/{rid}",
                Verbosity = DotNetVerbosity.Minimal,
                NoLogo = true,
                MSBuildSettings = new DotNetMSBuildSettings()
                    .WithProperty("PublishSingleFile", "true")
                    .WithProperty("IncludeNativeLibrariesForSelfExtract", "true")
                    .WithProperty("EnableCompressionInSingleFile", "true")
                    .WithProperty("OpenApiGenerateDocumentsOnBuild", "false"),
            });
        }
    }

    foreach (var component in components)
    {
        foreach (var rid in runtimes)
        {
            var tarball = $"./artifacts/luff-{component.Name}-{rid}.tar.gz";
            var directory = MakeAbsolute(Directory($"./artifacts/bin/{component.Name}/{rid}"));

            using (var stream = System.IO.File.Create(tarball))
            using (var gzip = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionLevel.Optimal))
            using (var writer = new System.Formats.Tar.TarWriter(gzip, System.Formats.Tar.TarEntryFormat.Pax))
            {
                foreach (var file in GetFiles($"{directory}/**/*"))
                {
                    var relative = MakeRelative(file, directory);
                    writer.WriteEntry(file.FullPath, relative.ToString());
                }
            }

            ctx.Information($"Created {tarball}");
        }
    }

    StartProcess(
        "dotnet",
        new ProcessSettings
        {
            Arguments = "minver --default-pre-release-identifiers preview.0",
            RedirectStandardOutput = true,
        },
        out var minverOutput);

    var version = string.Concat(minverOutput).Trim();
    if (string.IsNullOrWhiteSpace(version))
    {
        version = "latest";
    }

    var dockerTarball = "./artifacts/luff-server-docker.tar.gz";
    var dotenv = System.IO.File.ReadAllText("./eng/server/.env.example")
        .Replace("LUFF_VERSION=latest", $"LUFF_VERSION={version}");

    using (var stream = System.IO.File.Create(dockerTarball))
    using (var gzip = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionLevel.Optimal))
    using (var writer = new System.Formats.Tar.TarWriter(gzip, System.Formats.Tar.TarEntryFormat.Pax))
    {
        writer.WriteEntry(System.IO.Path.GetFullPath("./eng/server/compose.yaml"), "compose.yaml");
        writer.WriteEntry(System.IO.Path.GetFullPath("./eng/server/uninstall.sh"), "uninstall.sh");

        writer.WriteEntry(new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, ".env")
        {
            DataStream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(dotenv)),
            Mode = System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite
                | System.IO.UnixFileMode.GroupRead | System.IO.UnixFileMode.OtherRead,
        });
    }

    ctx.Information($"Created {dockerTarball}");

    var agentTarball = "./artifacts/luff-agent-docker.tar.gz";
    var agentDotenv = System.IO.File.ReadAllText("./eng/agent/.env.example")
        .Replace("LUFF_VERSION=latest", $"LUFF_VERSION={version}");

    using (var stream = System.IO.File.Create(agentTarball))
    using (var gzip = new System.IO.Compression.GZipStream(stream, System.IO.Compression.CompressionLevel.Optimal))
    using (var writer = new System.Formats.Tar.TarWriter(gzip, System.Formats.Tar.TarEntryFormat.Pax))
    {
        writer.WriteEntry(System.IO.Path.GetFullPath("./eng/agent/compose.yaml"), "compose.yaml");
        writer.WriteEntry(System.IO.Path.GetFullPath("./eng/agent/compose.frontdoor.yaml"), "compose.frontdoor.yaml");

        writer.WriteEntry(new System.Formats.Tar.PaxTarEntry(System.Formats.Tar.TarEntryType.RegularFile, ".env")
        {
            DataStream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(agentDotenv)),
            Mode = System.IO.UnixFileMode.UserRead | System.IO.UnixFileMode.UserWrite
                | System.IO.UnixFileMode.GroupRead | System.IO.UnixFileMode.OtherRead,
        });
    }

    ctx.Information($"Created {agentTarball}");
});

Task("Publish")
    .IsDependentOn("Test")
    .IsDependentOn("Package");

////////////////////////////////////////////////////////////////
// Targets

Task("Default")
    .IsDependentOn("Publish");

////////////////////////////////////////////////////////////////
// Execution

RunTarget(target);
