using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Kurmann.Videoschnitt.MetadataProcessor;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMetadataProcessor(this IServiceCollection services)
    {
        // Register MetadataProcessingService
        services.AddSingleton<MetadataProcessingService>();

        return services;
    }
}