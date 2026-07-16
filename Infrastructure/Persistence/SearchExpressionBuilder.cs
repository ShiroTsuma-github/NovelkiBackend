namespace Infrastructure.Persistence;

using Application.Common;
using System.Linq.Expressions;
using System.Reflection;

internal sealed class TextSearchExpressionFactory
{
    private const string EscapeCharacter = @"\";

    private static readonly MethodInfo LikeMethod = typeof(DbFunctionsExtensions).GetMethod(
        nameof(DbFunctionsExtensions.Like),
        [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])!;

    private static readonly MethodInfo ILikeMethod = typeof(NpgsqlDbFunctionsExtensions).GetMethod(
        nameof(NpgsqlDbFunctionsExtensions.ILike),
        [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])!;

    private readonly bool _useILike;

    public TextSearchExpressionFactory(bool useILike)
    {
        _useILike = useILike;
    }

    public Expression<Func<TEntity, bool>> Match<TEntity>(
        Expression<Func<TEntity, string>> valueSelector,
        Expression<Func<TEntity, string>> normalizedValueSelector,
        string search)
    {
        var selector = SelectSelector(valueSelector, normalizedValueSelector);
        return Expression.Lambda<Func<TEntity, bool>>(
            BuildLikeCall(selector.Body, search),
            selector.Parameters);
    }

    public Expression<Func<TEntity, bool>> AnyMatch<TEntity, TElement>(
        Expression<Func<TEntity, IEnumerable<TElement>>> collectionSelector,
        Expression<Func<TElement, string>> valueSelector,
        Expression<Func<TElement, string>> normalizedValueSelector,
        string search)
    {
        var selector = SelectSelector(valueSelector, normalizedValueSelector);
        var elementPredicate = Expression.Lambda<Func<TElement, bool>>(
            BuildLikeCall(selector.Body, search),
            selector.Parameters);
        var anyCall = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Any),
            [typeof(TElement)],
            collectionSelector.Body,
            elementPredicate);

        return Expression.Lambda<Func<TEntity, bool>>(anyCall, collectionSelector.Parameters);
    }

    private Expression<Func<TEntity, string>> SelectSelector<TEntity>(
        Expression<Func<TEntity, string>> valueSelector,
        Expression<Func<TEntity, string>> normalizedValueSelector)
    {
        return _useILike ? valueSelector : normalizedValueSelector;
    }

    private MethodCallExpression BuildLikeCall(Expression value, string search)
    {
        var patternInput = _useILike ? search : MappingExtensions.NormalizeName(search);
        return Expression.Call(
            _useILike ? ILikeMethod : LikeMethod,
            Expression.Property(null, typeof(EF), nameof(EF.Functions)),
            value,
            Expression.Constant(ToLikePattern(patternInput)),
            Expression.Constant(EscapeCharacter));
    }

    private static string ToLikePattern(string value)
    {
        var trimmed = value.Trim();
        var pattern = EscapeLike(trimmed).Replace("*", "%", StringComparison.Ordinal);
        return trimmed.Contains('*') ? pattern : $"%{pattern}%";
    }

    private static string EscapeLike(string value)
    {
        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
    }
}

internal static class PredicateExpression
{
    public static Expression<Func<T, bool>> And<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        return Combine(left, right, Expression.AndAlso);
    }

    public static Expression<Func<T, bool>> Or<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        return Combine(left, right, Expression.OrElse);
    }

    public static Expression<Func<T, bool>>? OrAll<T>(
        IEnumerable<Expression<Func<T, bool>>> predicates)
    {
        Expression<Func<T, bool>>? combined = null;
        foreach (var predicate in predicates)
        {
            combined = combined == null ? predicate : Or(combined, predicate);
        }

        return combined;
    }

    private static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> merge)
    {
        var parameter = Expression.Parameter(typeof(T), left.Parameters[0].Name);
        var leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
        var rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
        return Expression.Lambda<Func<T, bool>>(merge(leftBody, rightBody), parameter);
    }

    private static Expression ReplaceParameter(
        Expression expression,
        ParameterExpression source,
        ParameterExpression target)
    {
        return new ParameterReplaceVisitor(source, target).Visit(expression)!;
    }

    private sealed class ParameterReplaceVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        public ParameterReplaceVisitor(ParameterExpression source, ParameterExpression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }
}
