namespace Domain.Exceptions;

public sealed class EntityInUseException<TEntity> : Exception
{
    public EntityInUseException(string name)
        : base(
            $"The {typeof(TEntity).Name.ToLowerInvariant()} '{name}' cannot be deleted because it is used by one or more books.")
    {
    }
}
