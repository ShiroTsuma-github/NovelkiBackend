namespace Application.Common;

using System.Diagnostics.CodeAnalysis;

public static class Guard
{
    public static void ThrowIfFound<TEntity, TKey>
        (
        [NotNull]TEntity? entity,
        string name,
        Func<TEntity, TKey> keySelector
        ) where TEntity : class
    {
        if (entity != null)
        {
            var key = keySelector(entity);
            throw new EntityAlreadyExistsException<TEntity, TKey>(name, key);
        }
    }

    public static void ThrowIfNotFound<TEntity, TKey>
        (
        [NotNull]TEntity? entity,
        TKey id
        ) where TEntity : class
    {
        if (entity == null)
        {
            throw new EntityNotFoundException<TEntity, TKey>(id);
        }
    }
}
