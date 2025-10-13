namespace Application.Common.Interfaces;

public interface IMapper<TSource, TDestination>
{
    TDestination Map(TSource source);
    void Map(TSource source, TDestination destination);
    IEnumerable<TDestination> Map(IEnumerable<TSource> source);
}
