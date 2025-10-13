namespace Application;

using Application.Common.DTOs.Book;
using Application.Common.DTOs.Genre;
using Application.Common.DTOs.Status;
using Application.Common.DTOs.Type;
using Application.Features.BookFeatures.Commands;
using Application.Features.BookFeatures.Mappers;
using Application.Features.GenreFeatures.Commands;
using Application.Features.GenreFeatures.Mappers;
using Application.Features.GenreFeatures.Queries.GetGenre;
using Application.Features.StatusFeatures.Commands;
using Application.Features.StatusFeatures.Mappers;
using Application.Features.StatusFeatures.Queries.GetStatus;
using Application.Features.TypeFeatures.Commands;
using Application.Features.TypeFeatures.Mappers;
using Application.Features.TypeFeatures.Queries.GetType;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using Type = Domain.Entities.Type;

public static class DependencyInjection
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(CreateBookCommand).Assembly));
        builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        builder.Services.AddScoped<IRequestHandler<GetGenreByNameQuery<GenreDto>, GenreDto?>, GetGenreByNameQueryHandler<GenreDto>>();
        builder.Services.AddScoped<IRequestHandler<GetGenreByNameQuery<GenreDetailsDto>, GenreDetailsDto?>, GetGenreByNameQueryHandler<GenreDetailsDto>>();
        builder.Services.AddScoped<IRequestHandler<GetGenreQuery<GenreDto>, GenreDto?>, GetGenreQueryHandler<GenreDto>>();
        builder.Services.AddScoped<IRequestHandler<GetGenreQuery<GenreDetailsDto>, GenreDetailsDto?>, GetGenreQueryHandler<GenreDetailsDto>>();

        builder.Services.AddScoped<IRequestHandler<GetStatusByNameQuery<StatusDto>, StatusDto?>, GetStatusByNameQueryHandler<StatusDto>>();
        builder.Services.AddScoped<IRequestHandler<GetStatusByNameQuery<StatusDetailsDto>, StatusDetailsDto?>, GetStatusByNameQueryHandler<StatusDetailsDto>>();
        builder.Services.AddScoped<IRequestHandler<GetStatusQuery<StatusDto>, StatusDto?>, GetStatusQueryHandler<StatusDto>>();
        builder.Services.AddScoped<IRequestHandler<GetStatusQuery<StatusDetailsDto>, StatusDetailsDto?>, GetStatusQueryHandler<StatusDetailsDto>>();

        builder.Services.AddScoped<IRequestHandler<GetTypeByNameQuery<TypeDto>, TypeDto?>, GetTypeByNameQueryHandler<TypeDto>>();
        builder.Services.AddScoped<IRequestHandler<GetTypeByNameQuery<TypeDetailsDto>, TypeDetailsDto?>, GetTypeByNameQueryHandler<TypeDetailsDto>>();
        builder.Services.AddScoped<IRequestHandler<GetTypeQuery<TypeDto>, TypeDto?>, GetTypeQueryHandler<TypeDto>>();
        builder.Services.AddScoped<IRequestHandler<GetTypeQuery<TypeDetailsDto>, TypeDetailsDto?>, GetTypeQueryHandler<TypeDetailsDto>>();


        builder.Services.AddSingleton<IMapper<CreateBookCommand, Book>, CreateBookMapper>();
        builder.Services.AddSingleton<IMapper<Book, BookDto>, BookToDto>();

        builder.Services.AddSingleton<IMapper<CreateGenreCommand, Genre>, CreateGenreMapper>();
        builder.Services.AddSingleton<IMapper<UpdateGenreCommand, Genre>, UpdateGenreMapper>();
        builder.Services.AddSingleton<IMapper<Genre, GenreDto>, GenreToDto>();
        builder.Services.AddSingleton<IMapper<Genre, GenreDetailsDto>, GenreToDetailsDto>();

        builder.Services.AddSingleton<IMapper<CreateStatusCommand, Status>, CreateStatusMapper>();
        builder.Services.AddSingleton<IMapper<UpdateStatusCommand, Status>, UpdateStatusMapper>();
        builder.Services.AddSingleton<IMapper<Status, StatusDto>, StatusToDto>();
        builder.Services.AddSingleton<IMapper<Status, StatusDetailsDto>, StatusToDetailsDto>();

        builder.Services.AddSingleton<IMapper<CreateTypeCommand, Type>, CreateTypeMapper>();
        builder.Services.AddSingleton<IMapper<UpdateTypeCommand, Type>, UpdateTypeMapper>();
        builder.Services.AddSingleton<IMapper<Type, TypeDto>, TypeToDto>();
        builder.Services.AddSingleton<IMapper<Type, TypeDetailsDto>, TypeToDetailsDto>();
    }
}
