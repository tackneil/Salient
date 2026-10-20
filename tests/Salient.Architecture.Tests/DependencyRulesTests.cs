using System.Xml.Linq;

namespace Salient.Architecture.Tests;

/// <summary>
/// Enforces the project dependency rules from ADR-0002 by inspecting the declared references in each project file.
/// </summary>
public sealed class DependencyRulesTests
{
    private const string AnyProject = "*";

    private static readonly Dictionary<string, string[]> AllowedProjectReferences = new()
    {
        ["Salient.Core"] = [],
        ["Salient.Features"] = ["Salient.Core"],
        ["Salient.Features.Sift"] = ["Salient.Core", "Salient.Features"],
        ["Salient.Matching"] = ["Salient.Core", "Salient.Features"],
        ["Salient.Evaluation"] = ["Salient.Core", "Salient.Features", "Salient.Matching"],
        ["Salient.Reference.OpenCv"] = ["Salient.Core", "Salient.Features"],
        ["Salient.Api"] = [AnyProject],
    };

    public static TheoryData<string> SourceProjects
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in AllowedProjectReferences.Keys)
            {
                data.Add(name);
            }

            return data;
        }
    }

    [Fact]
    public void Every_source_project_has_a_dependency_rule()
    {
        var projects = Directory.GetFiles(Path.Combine(RepositoryRoot, "src"), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension);

        Assert.All(projects, name => Assert.True(
            AllowedProjectReferences.ContainsKey(name!),
            $"{name} has no dependency rule. Add it to {nameof(AllowedProjectReferences)} and to ADR-0002."));
    }

    [Theory]
    [MemberData(nameof(SourceProjects))]
    public void Project_references_only_allowed_projects(string project)
    {
        var allowed = AllowedProjectReferences[project];
        if (allowed.Contains(AnyProject))
        {
            return;
        }

        var referenced = Load(project)
            .Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/')));

        Assert.All(referenced, reference => Assert.Contains(reference, allowed));
    }

    [Theory]
    [MemberData(nameof(SourceProjects))]
    public void Only_the_reference_project_depends_on_OpenCvSharp(string project)
    {
        if (project == "Salient.Reference.OpenCv")
        {
            return;
        }

        Assert.DoesNotContain(PackageReferences(project), p => p.StartsWith("OpenCvSharp", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(SourceProjects))]
    public void Only_the_api_project_depends_on_AspNetCore(string project)
    {
        if (project == "Salient.Api")
        {
            return;
        }

        var document = Load(project);
        Assert.Equal("Microsoft.NET.Sdk", document.Root!.Attribute("Sdk")?.Value);
        Assert.DoesNotContain(PackageReferences(project), p => p.StartsWith("Microsoft.AspNetCore", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            document.Descendants("FrameworkReference"),
            e => e.Attribute("Include")?.Value == "Microsoft.AspNetCore.App");
    }

    private static IEnumerable<string> PackageReferences(string project) =>
        Load(project).Descendants("PackageReference").Select(e => e.Attribute("Include")!.Value);

    private static XDocument Load(string project) =>
        XDocument.Load(Path.Combine(RepositoryRoot, "src", project, project + ".csproj"));

    private static string RepositoryRoot { get; } = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Salient.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root (Salient.slnx).");
    }
}
