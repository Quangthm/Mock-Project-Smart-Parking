using CloudinaryDotNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Media;
using SmartParking.UserService.Domain.Entities;

namespace UserService.Infrastructure.Media;

public static class MediaRegistration
{
    public static IServiceCollection AddUserMedia(this IServiceCollection services, IConfiguration config)
    {
        var enabled = config.GetValue<bool>("MediaStorage:Enabled");
        var names = new[] { "CloudName", "ApiKey", "ApiSecret" };
        var values = names.Select(n => config["MediaStorage:Cloudinary:" + n]?.Trim()).ToArray();
        if (enabled)
        {
            var missing = names.Where((_, i) => string.IsNullOrWhiteSpace(values[i])).ToArray();
            if (missing.Length != 0) throw new InvalidOperationException("Configure " + string.Join(", ", missing.Select(n => "MediaStorage:Cloudinary:" + n)) + ".");
        }
        services.AddSingleton(new MediaSettings(enabled, values[0] ?? ""));
        services.AddSingleton<IImageInspector, SkiaImageInspector>();
        services.AddScoped<MediaUseCases>();
        if (enabled)
        {
            services.AddSingleton(new Cloudinary(new CloudinaryDotNet.Account(values[0], values[1], values[2])));
            services.AddSingleton<MediaHttpClient>();
            services.AddScoped<IMediaProvider, CloudinaryMediaProvider>();
        }
        else services.AddSingleton<IMediaProvider, DisabledMediaProvider>();
        return services;
    }

    private sealed class DisabledMediaProvider : IMediaProvider
    {
        public Task<ProviderImage?> FindAsync(MediaAsset asset, CancellationToken ct) => throw new MediaProviderException();
        public Task<ProviderImage> UploadAsync(MediaAsset asset, byte[] content, CancellationToken ct) => throw new MediaProviderException();
        public Task<Stream> OpenAsync(MediaAsset asset, CancellationToken ct) => throw new MediaProviderException();
    }
}
