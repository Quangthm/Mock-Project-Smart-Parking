using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Services;
using Xunit;

namespace SmartParking.PaymentService.Tests.Domain;

public class PaymentStateMachineTests
{
    [Theory]
    [InlineData(PaymentStatus.PENDING, PaymentStatus.SUCCESS, true)]
    [InlineData(PaymentStatus.PENDING, PaymentStatus.FAILED, true)]
    [InlineData(PaymentStatus.PENDING, PaymentStatus.CANCELLED, true)]
    [InlineData(PaymentStatus.PENDING, PaymentStatus.UNKNOWN, true)]
    [InlineData(PaymentStatus.UNKNOWN, PaymentStatus.SUCCESS, true)]
    [InlineData(PaymentStatus.UNKNOWN, PaymentStatus.FAILED, true)]
    [InlineData(PaymentStatus.FAILED, PaymentStatus.PENDING, true)] // retry
    [InlineData(PaymentStatus.SUCCESS, PaymentStatus.FAILED, false)] // Immutability
    [InlineData(PaymentStatus.SUCCESS, PaymentStatus.CANCELLED, false)]
    [InlineData(PaymentStatus.SUCCESS, PaymentStatus.PENDING, false)]
    [InlineData(PaymentStatus.CANCELLED, PaymentStatus.SUCCESS, false)]
    [InlineData(PaymentStatus.CANCELLED, PaymentStatus.PENDING, false)]
    public void CanTransition_ValidatesAllowedTransitionsCorrectly(PaymentStatus current, PaymentStatus next, bool expected)
    {
        var result = PaymentStateMachine.CanTransition(current, next);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void AssertTransition_ThrowsInvalidOperationException_WhenTransitionDisallowed()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PaymentStateMachine.AssertTransition(PaymentStatus.SUCCESS, PaymentStatus.FAILED));
    }

    [Fact]
    public void AssertTransition_Succeeds_WhenTransitionAllowed()
    {
        var exception = Record.Exception(() =>
            PaymentStateMachine.AssertTransition(PaymentStatus.PENDING, PaymentStatus.SUCCESS));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(4, false)]
    public void IsRetryAllowed_RespectsMaxRetryLimit(int retryCount, bool expected)
    {
        var result = PaymentStateMachine.IsRetryAllowed(retryCount);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void PaymentTransaction_MarkSuccess_SetsStatusAndDetails()
    {
        var tx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            Amount = 25000,
            Status = PaymentStatus.PENDING
        };

        var now = DateTimeOffset.UtcNow;
        tx.MarkSuccess("GW-123456", now);

        Assert.Equal(PaymentStatus.SUCCESS, tx.Status);
        Assert.Equal("GW-123456", tx.GatewayTransactionId);
        Assert.Equal(now, tx.PaidAt);
    }

    [Fact]
    public void PaymentTransaction_MarkFailed_ThrowsIfAlreadySuccess()
    {
        var tx = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            Amount = 25000,
            Status = PaymentStatus.SUCCESS
        };

        Assert.Throws<InvalidOperationException>(() => tx.MarkFailed());
    }

    [Fact]
    public void PaymentOrder_MarkPaid_UpdatesStatusAndTimestamp()
    {
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            Amount = 25000,
            FinalAmount = 25000,
            Status = PaymentOrderStatus.PENDING
        };

        var now = DateTimeOffset.UtcNow;
        order.MarkPaid(now);

        Assert.Equal(PaymentOrderStatus.PAID, order.Status);
        Assert.Equal(now, order.UpdatedAt);
    }
}
