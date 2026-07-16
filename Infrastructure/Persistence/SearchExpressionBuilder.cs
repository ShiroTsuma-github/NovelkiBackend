namespace Infrastructure.Persistence;

using Application.Common;
using Domain.Repositories;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

internal sealed class TextSearchExpressionFactory(bool useILike)
{
    private const string _escapeCharacter = @"\";

    private static readonly MethodInfo _likeMethod = typeof(DbFunctionsExtensions).GetMethod(
        nameof(DbFunctionsExtensions.Like),
        [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])!;

    private static readonly MethodInfo _iLikeMethod = typeof(NpgsqlDbFunctionsExtensions).GetMethod(
        nameof(NpgsqlDbFunctionsExtensions.ILike),
        [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])!;

    public Expression<Func<TEntity, bool>> Match<TEntity>(
        Expression<Func<TEntity, string>> valueSelector,
        Expression<Func<TEntity, string>> normalizedValueSelector,
        string search)
    {
        Expression<Func<TEntity, string>> selector = SelectSelector(valueSelector, normalizedValueSelector);
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
        Expression<Func<TElement, string>> selector = SelectSelector(valueSelector, normalizedValueSelector);
        var elementPredicate = Expression.Lambda<Func<TElement, bool>>(
            BuildLikeCall(selector.Body, search),
            selector.Parameters);
        MethodCallExpression anyCall = Expression.Call(
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
        return useILike ? valueSelector : normalizedValueSelector;
    }

    private MethodCallExpression BuildLikeCall(Expression value, string search)
    {
        string patternInput = useILike ? search : MappingExtensions.NormalizeName(search);
        return Expression.Call(
            useILike ? _iLikeMethod : _likeMethod,
            Expression.Property(null, typeof(EF), nameof(EF.Functions)),
            value,
            Expression.Constant(ToLikePattern(patternInput)),
            Expression.Constant(_escapeCharacter));
    }

    private static string ToLikePattern(string value)
    {
        string trimmed = value.Trim();
        string pattern = EscapeLike(trimmed).Replace("*", "%", StringComparison.Ordinal);
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

internal sealed class NumberSearchExpressionFactory(bool useNativeDecimalComparison)
{
    public Expression<Func<TEntity, bool>> MatchInteger<TEntity>(
        Expression<Func<TEntity, int?>> selector,
        BookSearchOperator op,
        decimal value)
    {
        return useNativeDecimalComparison
            ? PredicateExpression.CompareNullable(ConvertSelector<TEntity, int?, decimal?>(selector), op, value)
            : PredicateExpression.CompareNullable(ConvertSelector<TEntity, int?, double?>(selector), op,
                (double)value);
    }

    public Expression<Func<TEntity, bool>> MatchDecimal<TEntity>(
        Expression<Func<TEntity, decimal?>> selector,
        BookSearchOperator op,
        decimal value)
    {
        return PredicateExpression.CompareNullable(selector, op, value);
    }

    private static Expression<Func<TEntity, TTarget>> ConvertSelector<TEntity, TSource, TTarget>(
        Expression<Func<TEntity, TSource>> selector)
    {
        return Expression.Lambda<Func<TEntity, TTarget>>(
            Expression.Convert(selector.Body, typeof(TTarget)),
            selector.Parameters);
    }
}

internal sealed class DateSearchExpressionFactory(bool useNativeComparison)
{
    private static readonly MethodInfo _dateTimeOffsetToStringMethod = typeof(DateTimeOffset).GetMethod(
        nameof(DateTimeOffset.ToString),
        Type.EmptyTypes)!;

    public Expression<Func<TEntity, bool>> Match<TEntity>(
        Expression<Func<TEntity, DateTimeOffset>> selector,
        BookSearchOperator op,
        DateOnly value)
    {
        (DateTimeOffset start, DateTimeOffset nextDay) = ToUtcDateBounds(value);
        if (useNativeComparison)
        {
            return MatchBounds(
                op,
                start,
                nextDay,
                (comparison, boundary) => PredicateExpression.Compare(selector, comparison, boundary));
        }

        var textSelector = Expression.Lambda<Func<TEntity, string>>(
            Expression.Call(selector.Body, _dateTimeOffsetToStringMethod),
            selector.Parameters);
        return MatchBounds(
            op,
            ToSqliteDateTimeOffsetString(start),
            ToSqliteDateTimeOffsetString(nextDay),
            (comparison, boundary) => PredicateExpression.CompareString(textSelector, comparison, boundary));
    }

    private static Expression<Func<TEntity, bool>> MatchBounds<TEntity, TBoundary>(
        BookSearchOperator op,
        TBoundary start,
        TBoundary nextDay,
        Func<BookSearchOperator, TBoundary, Expression<Func<TEntity, bool>>> compare)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => compare(BookSearchOperator.GreaterThanOrEqual, nextDay),
            BookSearchOperator.GreaterThanOrEqual => compare(BookSearchOperator.GreaterThanOrEqual, start),
            BookSearchOperator.LessThan => compare(BookSearchOperator.LessThan, start),
            BookSearchOperator.LessThanOrEqual => compare(BookSearchOperator.LessThan, nextDay),
            _ => PredicateExpression.And(
                compare(BookSearchOperator.GreaterThanOrEqual, start),
                compare(BookSearchOperator.LessThan, nextDay))
        };
    }

    private static (DateTimeOffset Start, DateTimeOffset NextDay) ToUtcDateBounds(DateOnly value)
    {
        var start = new DateTimeOffset(value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        return (start, start.AddDays(1));
    }

    private static string ToSqliteDateTimeOffsetString(DateTimeOffset value)
    {
        return value.ToString("yyyy-MM-dd HH:mm:ss.FFFFFFFzzz", CultureInfo.InvariantCulture);
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

    public static Expression<Func<T, bool>>? AndAll<T>(
        IEnumerable<Expression<Func<T, bool>>> predicates)
    {
        return CombineAll(predicates, And);
    }

    public static Expression<Func<T, bool>>? OrAll<T>(
        IEnumerable<Expression<Func<T, bool>>> predicates)
    {
        return CombineAll(predicates, Or);
    }

    public static Expression<Func<T, bool>> Compare<T, TValue>(
        Expression<Func<T, TValue>> selector,
        BookSearchOperator op,
        TValue value)
    {
        return Expression.Lambda<Func<T, bool>>(
            BuildComparison(selector.Body, op, Expression.Constant(value, typeof(TValue))),
            selector.Parameters);
    }

    public static Expression<Func<T, bool>> CompareNullable<T, TValue>(
        Expression<Func<T, TValue?>> selector,
        BookSearchOperator op,
        TValue value)
        where TValue : struct
    {
        Expression hasValue = Expression.Property(selector.Body, nameof(Nullable<int>.HasValue));
        Expression actualValue = Expression.Property(selector.Body, nameof(Nullable<int>.Value));
        Expression comparison = BuildComparison(
            actualValue,
            op,
            Expression.Constant(value, typeof(TValue)));

        return Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(hasValue, comparison),
            selector.Parameters);
    }

    public static Expression<Func<T, bool>> CompareString<T>(
        Expression<Func<T, string>> selector,
        BookSearchOperator op,
        string value)
    {
        MethodCallExpression comparisonResult = Expression.Call(
            typeof(string),
            nameof(string.Compare),
            Type.EmptyTypes,
            selector.Body,
            Expression.Constant(value));

        return Expression.Lambda<Func<T, bool>>(
            BuildComparison(comparisonResult, op, Expression.Constant(0)),
            selector.Parameters);
    }

    private static Expression<Func<T, bool>>? CombineAll<T>(
        IEnumerable<Expression<Func<T, bool>>> predicates,
        Func<Expression<Func<T, bool>>, Expression<Func<T, bool>>, Expression<Func<T, bool>>> combine)
    {
        Expression<Func<T, bool>>? result = null;
        foreach (Expression<Func<T, bool>> predicate in predicates)
        {
            result = result == null ? predicate : combine(result, predicate);
        }

        return result;
    }

    private static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> merge)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(T), left.Parameters[0].Name);
        Expression leftBody = ReplaceParameter(left.Body, left.Parameters[0], parameter);
        Expression rightBody = ReplaceParameter(right.Body, right.Parameters[0], parameter);
        return Expression.Lambda<Func<T, bool>>(merge(leftBody, rightBody), parameter);
    }

    private static BinaryExpression BuildComparison(
        Expression left,
        BookSearchOperator op,
        Expression right)
    {
        return op switch
        {
            BookSearchOperator.GreaterThan => Expression.GreaterThan(left, right),
            BookSearchOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(left, right),
            BookSearchOperator.LessThan => Expression.LessThan(left, right),
            BookSearchOperator.LessThanOrEqual => Expression.LessThanOrEqual(left, right),
            _ => Expression.Equal(left, right)
        };
    }

    private static Expression ReplaceParameter(
        Expression expression,
        ParameterExpression source,
        ParameterExpression target)
    {
        return new ParameterReplaceVisitor(source, target).Visit(expression)!;
    }

    private sealed class ParameterReplaceVisitor(ParameterExpression source, ParameterExpression target)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == source ? target : base.VisitParameter(node);
        }
    }
}
