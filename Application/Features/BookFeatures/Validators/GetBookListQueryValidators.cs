namespace Application.Features.BookFeatures.Validators;

using Queries.GetBook;

public sealed class GetAllBooksQueryValidator : AbstractValidator<GetAllBooksQuery>
{
    public GetAllBooksQueryValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, 500);
    }
}

public sealed class GetAllAdminBooksQueryValidator : AbstractValidator<GetAllAdminBooksQuery>
{
    public GetAllAdminBooksQueryValidator()
    {
        RuleFor(query => query.Skip).GreaterThanOrEqualTo(0);
        RuleFor(query => query.Take).InclusiveBetween(1, 500);
    }
}
