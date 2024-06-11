using Microsoft.Extensions.Logging;
using Kurmann.Videoschnitt.Messages.Metadata;
using Wolverine;
using Microsoft.Extensions.Options;

namespace Kurmann.Videoschnitt.MetadataProcessor.Services;

public class MetadataProcessingService(ILogger<MetadataProcessingService> logger,
                                 IOptions<Settings> settings,
                                 IMessageBus bus,
                                 MediaFileListenerService mediaFileListenerService)
{
    private readonly ILogger<MetadataProcessingService> _logger = logger;
    private readonly Settings _settings = settings.Value;
    private readonly IMessageBus _bus = bus;
    private readonly MediaFileListenerService _mediaFileListenerService = mediaFileListenerService;

    public async Task ProcessMetadataAsync()
    {
        _logger.LogInformation("Metadatenverarbeitung gestartet.");

        // Liste alle unterstützten Medien-Dateien im Verzeichnis auf
        var mediaFiles = _mediaFileListenerService.GetSupportedMediaFiles();
        if (mediaFiles.IsFailure)
        {
            _logger.LogError("Fehler beim Auflisten der Medien-Dateien: {Error}", mediaFiles.Error);
            return;
        }

        // todo: Eigene Logik zur Verarbeitung der Metadaten implementieren
    
        _logger.LogInformation("Metadatenverarbeitung abgeschlossen.");

        // Todo: Beziehe typisierte Directory-Informationen aus dem MediaFileListenerService
        var directory = new DirectoryInfo(_settings.InputDirectory);
        await _bus.PublishAsync(new MetadataProcessedEvent(directory, mediaFiles.Value));    
    }
}