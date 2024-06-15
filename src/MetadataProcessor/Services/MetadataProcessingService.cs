using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CSharpFunctionalExtensions;

namespace Kurmann.Videoschnitt.MetadataProcessor.Services;

public class MetadataProcessingService
{
    private readonly ILogger<MetadataProcessingService> _logger ;
    private readonly ApplicationSettings _settings;

    public MetadataProcessingService(ILogger<MetadataProcessingService> logger, IOptions<ApplicationSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task<Result<List<FileInfo>>> ProcessMetadataAsync(IEnumerable<FileInfo> mediaFiles)
    {
        _logger.LogInformation("Metadatenverarbeitung gestartet.");

        // todo: Eigene Logik zur Verarbeitung der Metadaten implementieren

        // Fake processing by waiting 5 seconds
        await Task.Delay(5000);
    
        _logger.LogInformation("Metadatenverarbeitung abgeschlossen.");

        // Todo: Beziehe typisierte Directory-Informationen aus dem MediaFileListenerService
        if (_settings.InputDirectory == null)
        {
            return Result.Failure<List<FileInfo>>("Kein Eingabeverzeichnis konfiguriert.");
        }

        return mediaFiles.ToList();
    }

    internal async Task ProcessMetadataAsync(FileInfo mediaFile)
    {
        _logger.LogInformation($"Verarbeite Metadaten für {mediaFile.Name}");

        // Fake processing by waiting 5 seconds
        await Task.Delay(5000);

        _logger.LogInformation($"Metadaten für {mediaFile.Name} verarbeitet.");
    }
}