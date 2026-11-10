using System.Security.Cryptography;
using System.Buffers.Binary;
using SkiaSharp;
using UserService.Application.Media;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.Infrastructure.Media;

public sealed class SkiaImageInspector : IImageInspector
{
    public ImageFacts Inspect(byte[] content, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (content.Length is <= 0 or > UploadImageValidator.MaximumBytes) throw Invalid();
        RejectAnimation(content);
        using var input = new MemoryStream(content, false);
        using var codec = SKCodec.Create(input);
        if (codec is null || codec.FrameCount > 1 || codec.Info.Width <= 0 || codec.Info.Height <= 0
            || codec.Info.Width > 8192 || codec.Info.Height > 8192 || (long)codec.Info.Width * codec.Info.Height > 25_000_000)
            throw Invalid();
        var mime = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => "image/jpeg", SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Webp => "image/webp", _ => throw Invalid()
        };
        // Size checks precede allocation. Decode every pixel, not just metadata.
        using var bitmap = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success) throw Invalid();
        ct.ThrowIfCancellationRequested();
        return new(mime, codec.Info.Width, codec.Info.Height, content.LongLength, Convert.ToHexString(SHA256.HashData(content)));
    }

    private static void RejectAnimation(ReadOnlySpan<byte> content)
    {
        // Some native PNG decoders expose only APNG's default frame. Inspect
        // container control chunks as well, so successful still decoding cannot
        // silently admit animated bytes that a browser would play.
        if (content.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
        {
            var offset = 8;
            while (offset <= content.Length - 12)
            {
                var length = BinaryPrimitives.ReadUInt32BigEndian(content.Slice(offset, 4));
                if (length > content.Length - offset - 12) throw Invalid();
                var type = content.Slice(offset + 4, 4);
                if (type.SequenceEqual("acTL"u8) || type.SequenceEqual("fcTL"u8) || type.SequenceEqual("fdAT"u8)) throw Invalid();
                offset += checked((int)length + 12);
                if (type.SequenceEqual("IEND"u8)) break;
            }
        }
        else if (content.Length >= 12 && content[..4].SequenceEqual("RIFF"u8) && content.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            var offset = 12;
            while (offset <= content.Length - 8)
            {
                var length = BinaryPrimitives.ReadUInt32LittleEndian(content.Slice(offset + 4, 4));
                if (length > content.Length - offset - 8) throw Invalid();
                var type = content.Slice(offset, 4);
                if (type.SequenceEqual("ANIM"u8) || type.SequenceEqual("ANMF"u8)
                    || type.SequenceEqual("VP8X"u8) && length > 0 && (content[offset + 8] & 2) != 0) throw Invalid();
                offset += checked((int)length + 8 + (int)(length & 1));
            }
        }
    }
    private static AuthException Invalid() => new("MEDIA_INVALID_IMAGE", "Image content is invalid or exceeds image limits.", 400);
}
