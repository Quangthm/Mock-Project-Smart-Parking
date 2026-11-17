using MediatR;
using SmartParking.IoTService.Application.Assistant.Knowledge;
using SmartParking.IoTService.Application.Assistant.Management;

namespace SmartParking.IoTService.Application.Assistant.Guest;

public sealed record PublicArticle(string Id, string TitleVi, string TitleEn, string Vi, string En, string[] Sources,
    string? SourceLocale = null, IReadOnlyList<string>? SearchTerms = null);
public sealed record PublicKnowledge(string Version, string Fingerprint, IReadOnlyList<PublicArticle> Articles);
public interface IPublicKnowledge
{
    Task<PublicKnowledge?> ReadAsync(CancellationToken ct);
}
public interface IGuestAnswerFactory
{
    IManagedAnswerProvider Create(PublicKnowledge knowledge, PublicArticle article, string locale);
}
public sealed record GuestOptions(bool ReleaseEnabled = false, int TimeoutMilliseconds = 20000);
public sealed record GuestStatus(bool Enabled, bool GuestFaq, string[] Locales, string ContractVersion = "0.4.1");
public sealed record GuestSource(string ArticleId, string Version, string Locale, string Title, string Excerpt, string Status = "active");
public sealed record GuestAction(string Type, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] Guid? ReservationId = null);
public sealed record GuestReply(Guid RequestId, string Mode, string Outcome, string Content,
    GuestSource[] Sources, object[] Cards, GuestAction[] Actions)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ReplyLocale { get; init; }
}
public sealed record GuestMessage(Guid RequestId, string? Message, string? Locale) : IRequest<GuestReply>
{
    public override string ToString() => "[GUEST MESSAGE REDACTED]";
}
public sealed record GetGuestStatus : IRequest<GuestStatus>;
public sealed class GuestFailure(int status, string code) : Exception
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
