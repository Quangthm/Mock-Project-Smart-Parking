namespace SmartParking.OperationService.API;

public static class Program
{
    public static void Main(string[] args) => OperationHost.Build(args).Run();

    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
        => OperationHost.Build(args, configure);
}
