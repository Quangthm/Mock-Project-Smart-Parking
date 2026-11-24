using SmartParking.IoTService.Application.Assistant.Knowledge;

namespace SmartParking.IoTService.Infrastructure.Assistant.Knowledge;

// Vietnamese source and optional reviewed English content share one publication hash.
internal sealed record DemoArticle(string Id, string Version, string Title, string Content, string[] SearchTerms)
{
    public string? EnglishTitle { get; init; }
    public string? EnglishContent { get; init; }
    public GroundedSource Source => new(Id, Version, Title, Content[..Math.Min(Content.Length, 1000)]);
}
