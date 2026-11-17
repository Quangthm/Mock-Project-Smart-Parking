using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Integrations;
using SmartParking.PaymentService.Infrastructure.Services;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Repositories;

return await RunAsync(args);

static async Task<int> RunAsync(string[] arguments)
{
    try
    {
        if (arguments.Length == 0 || arguments.Contains("--help"))
        {
            Console.WriteLine("Offline repair: --manifest <absolute-file-outside-repo> [--apply], or --inventory [--offset N] [--limit N].");
            Console.WriteLine("Requires ConnectionStrings__Payment and DataProtection__KeysDirectory with the existing Payment key ring. Dry-run is default.");
            return arguments.Length == 0 ? 2 : 0;
        }
        var inventory = false;
        var apply = false;
        string? manifest = null;
        var offset = 0;
        var limit = 100;
        for (var i = 0; i < arguments.Length; i++)
        {
            switch (arguments[i])
            {
                case "--inventory": inventory = true; break;
                case "--apply": apply = true; break;
                case "--manifest": manifest = arguments[++i]; break;
                case "--offset": offset = int.Parse(arguments[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                case "--limit": limit = int.Parse(arguments[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                default: throw new InvalidOperationException();
            }
        }
        if (inventory == (manifest is not null) || (inventory && apply)) throw new InvalidOperationException();
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Payment");
        var keysPath = Environment.GetEnvironmentVariable("DataProtection__KeysDirectory");
        if (string.IsNullOrWhiteSpace(connection) || string.IsNullOrWhiteSpace(keysPath) || !Path.IsPathRooted(keysPath) ||
            !Directory.Exists(keysPath) || !Directory.EnumerateFiles(keysPath, "key-*.xml").Any())
            throw new InvalidOperationException();
        RequireOutsideRepository(keysPath);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.None));
        services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(connection));
        services.AddDataProtection().SetApplicationName("SmartParking.PaymentService")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath)).DisableAutomaticKeyGeneration();
        services.AddScoped<ICheckoutBindingRepository, CheckoutBindingRepository>();
        services.AddScoped<ICheckoutSigningBindings, CheckoutSigningBindings>();
        services.AddScoped<IPaymentOrderRepository, PaymentOrderRepository>();
        services.AddScoped<IPaymentTransactionRepository, PaymentTransactionRepository>();
        services.AddScoped<ICheckoutBindingInventoryRepository, CheckoutBindingInventoryRepository>();
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssemblyContaining<ImportCheckoutBindingCommand>());
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var ct = cancellation.Token;
        var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        if (inventory)
        {
            var rows = await mediator.Send(new GetCheckoutBindingInventoryQuery(offset, limit), ct);
            Console.WriteLine(JsonSerializer.Serialize(new { offset, limit, rows }, json));
            return 0;
        }
        if (manifest is null || !Path.IsPathRooted(manifest)) throw new InvalidOperationException();
        RequireOutsideRepository(manifest);
        var info = new FileInfo(manifest);
        if (!info.Exists || info.Length > 1024 * 1024) throw new InvalidOperationException();
        var inputs = JsonSerializer.Deserialize<LegacyCheckoutBindingInput[]>(await File.ReadAllTextAsync(manifest, ct), json);
        if (inputs is null || inputs.Length is < 1 or > 1000 || inputs.Any(input => input is null) ||
            inputs.Select(input => input.OrderCode).Distinct().Count() != inputs.Length) throw new InvalidOperationException();

        // Preflight every entry before applying any mapping. A later concurrent conflict still
        // stops with a nonzero result; successful earlier rows are immutable and safe to replay.
        var results = new List<ImportCheckoutBindingResult>();
        foreach (var input in inputs) results.Add(await mediator.Send(new ImportCheckoutBindingCommand(input, false), ct));
        if (results.Any(result => result.State != "READY"))
        {
            Console.WriteLine(JsonSerializer.Serialize(results, json));
            return 2;
        }
        if (apply)
        {
            results.Clear();
            foreach (var input in inputs)
            {
                var result = await mediator.Send(new ImportCheckoutBindingCommand(input, true), ct);
                results.Add(result);
                if (result.State != "APPLIED") break;
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(results, json));
        return results.All(result => result.State == (apply ? "APPLIED" : "READY")) ? 0 : 2;
    }
    catch
    {
        Console.Error.WriteLine("Binding repair failed. Check approved schema, key-ring path, private manifest and database access. No credential details are emitted. Re-run dry-run before retrying apply.");
        return 1;
    }
}

static void RequireOutsideRepository(string path)
{
    var resolved = Path.GetFullPath(path);
    foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (!Directory.Exists(Path.Combine(directory.FullName, ".git")) && !File.Exists(Path.Combine(directory.FullName, ".git"))) continue;
            var relative = Path.GetRelativePath(directory.FullName, resolved);
            if (!Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException();
            break;
        }
    }
}
