using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System.Net;
using System.Security.Cryptography;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Media;

namespace UserService.Infrastructure.Media;

public sealed class CloudinaryMediaProvider(Cloudinary cloudinary, MediaHttpClient transport) : IMediaProvider
{
    public async Task<ProviderImage?> FindAsync(MediaAsset asset, CancellationToken ct)
    {
        try
        {
            var resource = await cloudinary.GetResourceAsync(new GetResourceParams(asset.PublicId)
                { ResourceType = ResourceType.Image, Type = "authenticated" }, ct);
            if (resource.StatusCode == HttpStatusCode.NotFound) return null;
            if (resource.Error is not null || resource.PublicId != asset.PublicId || resource.Type != "authenticated"
                || resource.ResourceType != ResourceType.Image || resource.Bytes != asset.ByteCount
                || string.IsNullOrWhiteSpace(resource.AssetId)) throw new MediaProviderException();
            var version = resource.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (asset.ProviderAssetId is not null && (asset.ProviderAssetId != resource.AssetId || asset.ProviderVersion != version))
                throw new MediaProviderException();
            var bytes = await DownloadAsync(asset.PublicId, version, asset.ByteCount, ct);
            var mime = resource.Format switch { "jpg" or "jpeg" => "image/jpeg", "png" => "image/png", "webp" => "image/webp", _ => "" };
            return new(resource.PublicId, resource.AssetId, version, mime, bytes.LongLength,
                Convert.ToHexString(SHA256.HashData(bytes)), resource.Width, resource.Height);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { throw new MediaProviderException(); }
    }

    public async Task<ProviderImage> UploadAsync(MediaAsset asset, byte[] content, CancellationToken ct)
    {
        try
        {
            using var stream = new MemoryStream(content, false);
            var result = await cloudinary.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription("image", stream), PublicId = asset.PublicId,
                Type = "authenticated", Overwrite = false, UniqueFilename = false, UseFilename = false
            }, ct);
            if (result.Error is not null) throw new MediaProviderException();
            // Reconcile through the exact authenticated locator and compare bytes,
            // even after upload success. No URL supplied by the provider is followed.
            return await FindAsync(asset, ct) ?? throw new MediaProviderException();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { throw new MediaProviderException(); }
    }

    public async Task<Stream> OpenAsync(MediaAsset asset, CancellationToken ct)
    {
        try
        {
            var image = await FindAsync(asset, ct) ?? throw new MediaProviderException();
            if (image.Sha256 != asset.Sha256 || image.MimeType != asset.MimeType) throw new MediaProviderException();
            var bytes = await DownloadAsync(asset.PublicId, asset.ProviderVersion!, asset.ByteCount, ct);
            if (Convert.ToHexString(SHA256.HashData(bytes)) != asset.Sha256) throw new MediaProviderException();
            return new MemoryStream(bytes, false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { throw new MediaProviderException(); }
    }

    private async Task<byte[]> DownloadAsync(string publicId, string version, long expectedBytes, CancellationToken ct)
    {
        // Signed delivery URL exists only inside Infrastructure; never returned,
        // logged or persisted. No transformations or arbitrary remote URLs.
        var url = cloudinary.Api.UrlImgUp.Secure(true).Type("authenticated").Version(version).Signed(true).BuildUrl(publicId);
        using var response = await transport.Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > UploadImageValidator.MaximumBytes)
            throw new MediaProviderException();
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) != 0)
        {
            if (output.Length + read > expectedBytes || output.Length + read > UploadImageValidator.MaximumBytes)
                throw new MediaProviderException();
            await output.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        if (output.Length != expectedBytes) throw new MediaProviderException();
        return output.ToArray();
    }
}

// Avoid URL logging handlers for server-only signed delivery requests.
public sealed class MediaHttpClient : IDisposable
{
    public HttpClient Client { get; } = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
    public void Dispose() => Client.Dispose();
}
