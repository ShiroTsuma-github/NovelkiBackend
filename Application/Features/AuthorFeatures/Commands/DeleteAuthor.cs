namespace Application.Features.AuthorFeatures.Commands;

public sealed record DeleteAuthorCommand(Guid Id) : IRequest;

public sealed class DeleteAuthorCommandHandler(IAuthorRepository authorRepository, IUser user)
    : IRequestHandler<DeleteAuthorCommand>
{
    public async Task Handle(DeleteAuthorCommand request, CancellationToken cancellationToken)
    {
        var author = await authorRepository.GetByIdAsync(request.Id, cancellationToken)
                     ?? throw new EntityNotFoundException<Author, Guid>(request.Id);
        if (author.CreatedBy != user.RequiredId &&
            !user.Roles.Contains(AuthorizationRoles.Admin, StringComparer.OrdinalIgnoreCase))
        {
            throw new EntityNotFoundException<Author, Guid>(request.Id);
        }

        if (author.Books.Count > 0)
        {
            throw new EntityInUseException<Author>(author.PrimaryName);
        }

        await authorRepository.DeleteAsync(author, cancellationToken);
    }
}
