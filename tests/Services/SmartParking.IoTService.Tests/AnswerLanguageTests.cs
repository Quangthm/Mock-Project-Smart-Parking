using SmartParking.IoTService.Application.Assistant.Guest;

namespace SmartParking.IoTService.Tests;

public sealed class AnswerLanguageTests
{
    [Theory]
    [InlineData("SmartPark là gì?", "en", "vi")]
    [InlineData("SmartPark la gi?", "en", "vi")]
    [InlineData("What is SmartPark?", "vi", "en")]
    [InlineData("Show my vehicles", "vi", "en")]
    [InlineData("Lich dat cua toi", "en", "vi")]
    [InlineData("refund guidance", "vi", "en")]
    [InlineData("assistant limitations", "vi", "en")]
    [InlineData("gioi han tro ly", "en", "vi")]
    [InlineData("Find parking near Hồ Chí Minh", "vi", "en")]
    [InlineData("SmartPark", "en", "en")]
    [InlineData("SmartPark", "vi", "vi")]
    public void DetectsPerMessageWithFallback(string text, string fallback, string expected) =>
        Assert.Equal(expected, AnswerLanguage.Detect(text, fallback));
}
