using Application;
using FluentValidation;

namespace Application.UnitTests;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_ShouldCallNext_WhenNoValidatorsExist()
    {
        var behavior = new ValidationBehavior<TestRequest, string>(Array.Empty<IValidator<TestRequest>>());
        var nextCalled = false;

        var result = await behavior.Handle(new TestRequest("ok"), cancellationToken =>
        {
            nextCalled = true;
            return Task.FromResult("done");
        }, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal("done", result);
    }

    [Fact]
    public async Task Handle_ShouldCallNext_WhenValidationSucceeds()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([new TestRequestValidator()]);
        var nextCalled = false;

        var result = await behavior.Handle(new TestRequest("value"), cancellationToken =>
        {
            nextCalled = true;
            return Task.FromResult("done");
        }, CancellationToken.None);

        Assert.True(nextCalled);
        Assert.Equal("done", result);
    }

    [Fact]
    public async Task Handle_ShouldThrowCombinedValidationErrors_WhenValidationFails()
    {
        var behavior = new ValidationBehavior<TestRequest, string>([new TestRequestValidator(), new TestRequestLengthValidator()]);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(new TestRequest(""), _ => Task.FromResult("done"), CancellationToken.None));

        Assert.True(exception.Errors.Count() >= 2);
        Assert.All(exception.Errors, error => Assert.Equal("Value", error.PropertyName));
    }

    private sealed record TestRequest(string Value) : MediatR.IRequest<string>;

    private sealed class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator()
        {
            RuleFor(request => request.Value).NotEmpty();
        }
    }

    private sealed class TestRequestLengthValidator : AbstractValidator<TestRequest>
    {
        public TestRequestLengthValidator()
        {
            RuleFor(request => request.Value).MinimumLength(2);
        }
    }
}
