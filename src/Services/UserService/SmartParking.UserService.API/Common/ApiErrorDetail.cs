namespace UserService.API.Common;

/// <summary>One rejected field and the reason it was rejected.</summary>
public readonly record struct ApiErrorDetail(string Field, string Message);
