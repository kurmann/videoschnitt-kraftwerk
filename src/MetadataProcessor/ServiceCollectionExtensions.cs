using Kurmann.Videoschnitt.MetadataProcessor.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Kurmann.Videoschnitt.Common;
using Kurmann.Videoschnitt.Common.Services;

namespace Kurmann.Videoschnitt.MetadataProcessor;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMetadataProcessor(this IServiceCollection services, IConfiguration configuration)
    {   
        // Add configuration sources
        services.Configure<ModuleSettings>(options => configuration.GetSection(ModuleSettings.SectionName).Bind(options));
        services.Configure<ApplicationSettings>(options => configuration.GetSection(ApplicationSettings.SectionName).Bind(options));

        // Register MetadataProcessingService
        services.AddScoped<Engine>();
        services.AddScoped<FFmpegMetadataService>();
        services.AddScoped<MediaSetService>();
        services.AddScoped<MediaPurposeOrganizer>();

        services.AddCommonServicesEngine();

        return services;
    }
}