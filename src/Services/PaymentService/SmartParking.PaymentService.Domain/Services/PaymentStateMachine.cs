using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Domain.Services;

public static class PaymentStateMachine
{
    public const int MaxRetryLimit = 3; // 1 initial + 3 retries = 4 max attempts

    public static bool CanTransition(PaymentStatus current, PaymentStatus next)
    {
        if (current == next) return true;

        return current switch
        {
            PaymentStatus.PENDING => next is PaymentStatus.SUCCESS or PaymentStatus.FAILED or PaymentStatus.CANCELLED or PaymentStatus.UNKNOWN,
            PaymentStatus.UNKNOWN => next is PaymentStatus.SUCCESS or PaymentStatus.FAILED,
            PaymentStatus.FAILED => next is PaymentStatus.PENDING, // for retry
            PaymentStatus.SUCCESS => false, // Immutable! Never transition away from SUCCESS
            PaymentStatus.CANCELLED => false, // Terminal
            _ => false
        };
    }

    public static void AssertTransition(PaymentStatus current, PaymentStatus next)
    {
        if (!CanTransition(current, next))
        {
            throw new InvalidOperationException($"Invalid payment state transition from {current} to {next}.");
        }
    }

    public static bool IsRetryAllowed(int retryCount)
    {
        return retryCount < MaxRetryLimit;
    }

    public static void AssertRetryAllowed(int retryCount)
    {
        if (!IsRetryAllowed(retryCount))
        {
            throw new InvalidOperationException($"Payment retry limit exceeded. Maximum {MaxRetryLimit} retries allowed.");
        }
    }
}
