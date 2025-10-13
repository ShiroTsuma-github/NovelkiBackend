namespace Application;

using Application.Common.DTOs.Genre;
using Application.Features.GenreFeatures.Mappers;
using Application.Features.BookFeatures.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
using Application.Common.DTOs.Book;
using Application.Features.BookFeatures.Mappers;
using Application.Features.GenreFeatures.Commands;
using Application.Features.StatusFeatures.Commands;
using Application.Features.StatusFeatures.Mappers;
using Application.Common.DTOs.Status;

public static class DependencyInjection
{
    public static void AddApplicationServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(typeof(CreateBookCommand).Assembly));
        builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        builder.Services.AddSingleton<IMapper<CreateBookCommand, Book>, CreateBookMapper>();
        builder.Services.AddSingleton<IMapper<CreateGenreCommand, Genre>, CreateGenreMapper>();
        builder.Services.AddSingleton<IMapper<CreateStatusCommand, Status>, CreateStatusMapper>();

        builder.Services.AddSingleton<IMapper<UpdateGenreCommand, Genre>, UpdateGenreMapper>();
        builder.Services.AddSingleton<IMapper<UpdateStatusCommand, Status>, UpdateStatusMapper>();

        builder.Services.AddSingleton<IMapper<Genre, GenreDto>, GenreToDto>();
        builder.Services.AddSingleton<IMapper<Book, BookDto>, BookToDto>();
        builder.Services.AddSingleton<IMapper<Status, StatusDto>, StatusToDto>();
    }
}
