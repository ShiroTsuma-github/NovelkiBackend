namespace Domain.Exceptions;

public class EntityAlreadyExistsException<TEntity, TKey> : Exception
{
    public EntityAlreadyExistsException(string name, TKey existingId)
        : base($"A {typeof(TEntity).Name} with name '{name}' already exists with ID: {existingId}.")
    {
        Name = name;
        ExistingId = existingId;
    }

    public string Name { get; }
    public TKey ExistingId { get; }
}
