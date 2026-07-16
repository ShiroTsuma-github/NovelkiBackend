namespace Domain.Exceptions;

public class EntityNotFoundException<TEntity, TKey> : Exception
{
    public EntityNotFoundException(TKey id)
        : base($"A {typeof(TEntity).Name} with ID '{id}' was not found.")
    {
        Id = id;
    }

    public TKey Id { get; }
}
