namespace UserService.API.Common;

/// <summary>A public response plus the diagnostic classification; neither leaks into the JSON body.</summary>
public readonly record struct ErrorHandlingResult(ApiResponse Response, bool IsUnexpected = false);
