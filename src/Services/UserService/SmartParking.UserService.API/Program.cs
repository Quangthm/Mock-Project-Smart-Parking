namespace UserService.API;

public static class Program
{
    public static async Task Main(string[] args) => await (await UserHost.BuildAsync(args)).RunAsync();
}
