namespace UserService.Application.Common.Models.Exceptions;

/// <summary>The database aborted or refused a guarded operation before commit.</summary>
public sealed class ProfileAccessConflictException(Exception innerException)
    : Exception("Profile access changed or is busy. Retry the request.", innerException);
