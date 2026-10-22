using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SmartParking.UserService.Tests;

public sealed class LayerBoundaryTests
{
    private static string Root
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SmartParking.slnx")))
                directory = directory.Parent;
            return directory?.FullName ?? throw new InvalidOperationException("Repository root missing.");
        }
    }

    private static bool HasHttpDependency(string source) =>
        Regex.IsMatch(source, @"\b(System\.Net\.Http|HttpClient|IHttpClientFactory|HttpRequestMessage|HttpResponseMessage)\b");

    private static bool InjectsConcreteStore(string source) =>
        Regex.IsMatch(source, @"\b(AppDbContext|(?!I[A-Z])\w+Store)\s+\w+\s*[,)=]");

    [Theory]
    [InlineData("using System.Net.Http;")]
    [InlineData("private HttpClient client;")]
    [InlineData("public Worker(IHttpClientFactory factory) {}")]
    public void DirectHttpDependenciesAreRejected(string source) => Assert.True(HasHttpDependency(source));

    [Theory]
    [InlineData("(ReservationGateStore repository, CancellationToken ct)")]
    [InlineData("(AppDbContext context)")]
    [InlineData("(OperatorOperationsStore operations)")]
    public void ConcreteStoreParametersAreRejectedRegardlessOfName(string source) => Assert.True(InjectsConcreteStore(source));

    [Fact]
    public void CompositionAndApplicationPortsAreAllowed()
    {
        Assert.False(InjectsConcreteStore("services.AddScoped<IReservationGateStore, ReservationGateStore>();"));
        Assert.False(InjectsConcreteStore("(IReservationGateStore repository, CancellationToken ct)"));
    }

    [Fact]
    public void InnerProjectsDoNotReferenceAdaptersOrTransportPackages()
    {
        var projects = Directory.GetFiles(Path.Combine(Root, "src", "Services"), "*.csproj", SearchOption.AllDirectories);
        Assert.NotEmpty(projects);
        foreach (var path in projects)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var xml = XDocument.Load(path);
            var domain = name.EndsWith(".Domain");
            var application = name.EndsWith(".Application");
            if (!domain && !application) continue;
            foreach (var reference in xml.Descendants("ProjectReference"))
            {
                var target = reference.Attribute("Include")!.Value;
                Assert.True(!domain && target.EndsWith(".Domain.csproj"), $"{name} has forbidden reference {target}");
            }
            foreach (var reference in xml.Descendants().Where(x => x.Name.LocalName is "PackageReference" or "FrameworkReference"))
            {
                var target = reference.Attribute("Include")!.Value;
                Assert.False(Regex.IsMatch(target, "AspNetCore|EntityFramework|Npgsql|IdentityModel|MassTransit|RabbitMQ", RegexOptions.IgnoreCase), $"{name} depends on {target}");
            }
        }
    }

    [Fact]
    public void InnerSourceAndPublicPortsDoNotLeakDatabaseOrHttpDetails()
    {
        foreach (var path in Directory.GetFiles(Path.Combine(Root, "src", "Services"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                (p.Contains(".Application" + Path.DirectorySeparatorChar) || p.Contains(".Domain" + Path.DirectorySeparatorChar))))
        {
            var source = File.ReadAllText(path);
            Assert.False(Regex.IsMatch(source, @"\b(using|namespace)\s+[^;\r\n]*(EntityFramework|Npgsql|AspNetCore|IdentityModel|(?:UserService|FacilityService|ReservationService|IoTService)\.(?:Persistence|Infrastructure))"), path);
            Assert.False(HasHttpDependency(source), path);
            Assert.DoesNotContain("FromSql", source);
            Assert.DoesNotContain("ExecuteUpdateAsync", source);
            Assert.DoesNotContain("IQueryable<", source);
        }
    }

    [Fact]
    public void EndpointsDoNotExecuteSqlOrInjectConcreteStores()
    {
        foreach (var path in Directory.GetFiles(Path.Combine(Root, "src", "Services"), "*.cs", SearchOption.AllDirectories)
            .Where(p => p.Contains(".API" + Path.DirectorySeparatorChar) && !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)))
        {
            var source = File.ReadAllText(path);
            Assert.DoesNotContain("new NpgsqlCommand", source);
            Assert.DoesNotContain("ExecuteUpdateAsync", source);
            Assert.False(InjectsConcreteStore(source), path);
        }
    }

    [Fact]
    public void ServicesDoNotReferenceAnotherServicesImplementation()
    {
        foreach (var service in Directory.GetDirectories(Path.Combine(Root, "src", "Services")))
        foreach (var project in Directory.GetFiles(service, "*.csproj", SearchOption.AllDirectories))
        foreach (var reference in XDocument.Load(project).Descendants("ProjectReference"))
        {
            var target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(project)!, reference.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar)));
            Assert.StartsWith(service + Path.DirectorySeparatorChar, target, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void UserServiceHasNoAssistantOwnership()
    {
        var assembly = typeof(global::UserService.Application.Services.AuthSessionService).Assembly;
        Assert.DoesNotContain(assembly.GetTypes(), t => t.Name.Contains("Chatbot") || t.Namespace?.Contains("Assistant") == true);
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name?.Contains("IoTService") == true);
        Assert.DoesNotContain("Chatbot", File.ReadAllText(Path.Combine(Root, "src/Services/UserService/SmartParking.UserService.API/Program.cs")));
    }
}
