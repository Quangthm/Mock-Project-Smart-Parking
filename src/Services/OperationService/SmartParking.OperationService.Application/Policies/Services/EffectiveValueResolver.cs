using SmartParking.OperationService.Domain.Policies;

namespace SmartParking.OperationService.Application.Policies.Services;

public static class EffectiveValueResolver
{
    public static T Resolve<T>(T? configuredValue, T adminDefault) =>
        configuredValue ?? adminDefault;

    public static void ValidateBounds(string paramName, int? value, int min, int max)
    {
        if (value.HasValue && (value.Value < min || value.Value > max))
        {
            throw new PolicyOutOfBoundsException(
                paramName,
                $"{paramName} must be between {min} and {max}."
            );
        }
    }

    public static void ValidateTimeLogic(int? minMinutes, int? maxHours)
    {
        if (minMinutes.HasValue && maxHours.HasValue)
        {
            if (minMinutes.Value > maxHours.Value * 60)
            {
                throw new InvalidPolicyDurationException(
                    "minReservationDurationMinutes cannot exceed maxReservationDurationHours."
                );
            }
        }
    }
}
