using System.Text.RegularExpressions;

namespace SmartParking.IoTService.Application.Assistant.Guest;

// Whole-question grammars: matching a topic word alone must not discard a private
// reference, an unsupported qualification, or a second instruction/question.
internal static partial class PublicQuestionPatterns
{
    internal static string? Match(string message)
    {
        if (message.Length > 320 || !Characters().IsMatch(message.Trim())) return null;
        var question = PublicFaqRetrieval.Normalize(message);
        if (VehicleNavigation().IsMatch(question)) return "vehicle-navigation";
        if (VehicleSupport().IsMatch(question)) return "vehicle-support";
        if (ShortBookingSteps().IsMatch(question) || BookingSteps().IsMatch(question)) return "booking-start";
        return null;
    }

    internal static string VehicleNavigationReply(bool english) => english
        ? "Sign in with your Driver account, open the account menu and select My Vehicles to view your registered vehicles. This is navigation guidance; I have not read your vehicle records."
        : "Đăng nhập bằng tài khoản Driver, mở menu tài khoản và chọn My Vehicles để xem các xe đã đăng ký. Đây là hướng dẫn mở màn hình; mình chưa tra cứu danh sách xe của bạn.";

    [GeneratedRegex(@"\A[\p{L}\p{M} ?!.,-]+\z", RegexOptions.CultureInvariant)]
    private static partial Regex Characters();

    private const string VehicleTypes = @"(?:vehicle types|types? of vehicles|kinds? of vehicles|vehicles)";
    private const string Vehicles = @"(?:cars?|motorcycles?|motorbikes?)(?: (?:and|or) (?:cars?|motorcycles?|motorbikes?))?";
    private const string BookingObject = @"(?:(?:a |the )?(?:parking )?(?:space|spot)|parking)";
    private const string BookingSite = @"(?: (?:on|with|in|through) smartpark)?";

    [GeneratedRegex(@"\A(?:(?:toi|minh|tui) )?(?:muon )?(?:dat cho|dat xe|dat truoc|book cho)" +
        @"(?: (?:gui xe|do xe|dau xe))? (?:(?:lam )?sao|lam rang|kieu gi|the nao|nhu (?:the )?nao|ntn)" +
        @"(?: (?:a|vay|ta|nhi|hen|he|rua))?\z", RegexOptions.CultureInvariant)]
    private static partial Regex ShortBookingSteps();

    [GeneratedRegex(@"\A(?:please )?(?:(?:what|which) " + VehicleTypes + @" (?:does|can) smartpark (?:support|cover|accept)|does smartpark (?:support|accept) " + Vehicles + @"|can " + Vehicles + @" park at smartpark|(?:what|which) " + VehicleTypes + @" (?:are supported by|can park at) smartpark|smartpark (?:ho tro|phuc vu|chap nhan) (?:nhung |cac )?loai xe nao)(?: please)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex VehicleSupport();

    [GeneratedRegex(@"\A(?:(?:(?:hay |vui long )?(?:huong dan|chi) (?:cho )?(?:toi |minh )?(?:cac buoc |tung buoc )?|(?:toi |minh )?muon biet cach |(?:cach|cac buoc|quy trinh) )(?:dat cho|dat xe|dat truoc)(?: (?:tren|qua|voi) smartpark)?|(?:dat cho|dat xe) nhu (?:the )?nao|(?:please )?(?:(?:show|tell|explain)(?: me)? (?:the )?(?:steps|process)(?: for| to)?|what are the steps to) (?:booking|book|reserve|reserving)(?: " + BookingObject + @")?" + BookingSite + @"|(?:please )?(?:how (?:do|can) i|how to) (?:book|reserve)(?: " + BookingObject + @")?" + BookingSite + @"(?: step by step)?|(?:please )?(?:explain|show me) (?:how to book|booking)" + BookingSite + @" step by step|(?:please )?guide me through (?:the )?booking process" + BookingSite + @")(?: please)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex BookingSteps();

    [GeneratedRegex(@"\A(?:(?:cho )?(?:(?:toi|minh|tui) (?:co the )?)?(?:xem|coi|quan ly) (?:danh sach |nhung |cac )?xe(?: (?:toi|minh|tui))?(?: (?:da )?(?:dang ky|luu))?(?: cua (?:toi|minh|tui))? o (?:dau|mo|muc nao|muc mo)|xe (?:da dang ky|da luu)(?: cua (?:toi|minh))? xem o dau|(?:where (?:can|do) i|how (?:can|do) i) (?:view|see|find|manage) (?:my (?:(?:registered|saved) )?vehicles|the vehicles i (?:have )?registered))(?: please)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex VehicleNavigation();
}
