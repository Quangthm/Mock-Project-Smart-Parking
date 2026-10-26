using FluentValidation;
using UserService.Application.Common.Behaviors;

namespace SmartParking.UserService.Tests;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task ScopedAsyncValidatorsExecuteInOrderAndReceiveCancellation()
    {
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = new List<string>();
        using var cancellation = new CancellationTokenSource();
        var first = new InlineValidator<Probe>();
        first.RuleFor(value => value.Value).MustAsync(async (_, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            calls.Add("first-enter");
            firstEntered.SetResult();
            await releaseFirst.Task.WaitAsync(token);
            calls.Add("first-exit");
            return true;
        });
        var second = new InlineValidator<Probe>();
        second.RuleFor(value => value.Value).MustAsync((_, token) =>
        {
            Assert.Equal(cancellation.Token, token);
            calls.Add("second");
            return Task.FromResult(true);
        });
        var behavior = new ValidationBehavior<Probe, string>([first, second]);
        var operation = behavior.Handle(new("value"), token =>
        {
            Assert.Equal(cancellation.Token, token);
            calls.Add("handler");
            return Task.FromResult("ok");
        }, cancellation.Token);
        await firstEntered.Task;
        Assert.Equal(["first-enter"], calls);
        releaseFirst.SetResult();
        Assert.Equal("ok", await operation);
        Assert.Equal(["first-enter", "first-exit", "second", "handler"], calls);
    }

    [Fact]
    public async Task CancellationStopsPendingValidationWithoutInvokingHandler()
    {
        using var cancellation = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var validator = new InlineValidator<Probe>();
        validator.RuleFor(value => value.Value).MustAsync(async (_, token) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return true;
        });
        var behavior = new ValidationBehavior<Probe, string>([validator]);
        var handled = false;
        var operation = behavior.Handle(new("value"), _ => { handled = true; return Task.FromResult("unexpected"); }, cancellation.Token);
        await entered.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.False(handled);
    }

    [Fact]
    public async Task MultipleInvalidValidatorsReportEachFailureOnce()
    {
        var first = new InlineValidator<Probe>();
        first.RuleFor(value => value.Value).NotEmpty().WithMessage("First constraint.");
        var second = new InlineValidator<Probe>();
        second.RuleFor(value => value.Value).MinimumLength(2).WithMessage("Second constraint.");
        var behavior = new ValidationBehavior<Probe, string>([first, second]);
        var exception = await Assert.ThrowsAsync<ValidationException>(() => behavior.Handle(
            new(""), _ => Task.FromResult("must not execute"), CancellationToken.None));
        Assert.Equal(["First constraint.", "Second constraint."], exception.Errors.Select(error => error.ErrorMessage));
    }

    private sealed record Probe(string Value);
}
