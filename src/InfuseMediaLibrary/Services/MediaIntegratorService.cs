using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CSharpFunctionalExtensions;
using Kurmann.Videoschnitt.Common.Models;
using Kurmann.Videoschnitt.Common.Services;
using Kurmann.Videoschnitt.Common.Services.FileSystem;
using Kurmann.Videoschnitt.Common.Entities.MediaTypes;

namespace Kurmann.Videoschnitt.InfuseMediaLibrary.Services;

public class MediaIntegratorService
{
    private readonly ILogger<MediaIntegratorService> _logger;
    private readonly IFileOperations _fileOperations;
    private readonly FFmpegMetadataService _ffmpegMetadataService;
    private readonly PosterAndFanartService _posterAndFanartService;
    private readonly ApplicationSettings _applicationSettings; 
    private readonly ModuleSettings _moduleSettings;

    public MediaIntegratorService(ILogger<MediaIntegratorService> logger,
                                  IFileOperations fileOperations,
                                  FFmpegMetadataService ffmpegMetadataService,
                                  PosterAndFanartService posterAndFanartService,
                                  IOptions<ApplicationSettings> applicationSettings,
                                  IOptions<ModuleSettings> moduleSettings)
    {
        _logger = logger;
        _fileOperations = fileOperations;
        _ffmpegMetadataService = ffmpegMetadataService;
        _posterAndFanartService = posterAndFanartService;
        _applicationSettings = applicationSettings.Value;
        _moduleSettings = moduleSettings.Value;
    }

    public async Task<Result<Maybe<LocalMediaServerFiles>>> IntegrateMediaSetToInfuseMediaLibrary(MediaSet mediaSet)
    {
        _logger.LogInformation("Integriere Medienset in die Infuse-Mediathek.");

        if (mediaSet == null)
            return Result.Failure<Maybe<LocalMediaServerFiles>>("Das Medienset ist null.");

        _logger.LogInformation($"Prüfe ob im Medienset {mediaSet.Title} Medien für lokale Medienserver vorhanden sind.");
        if (mediaSet.LocalMediaServerFiles.HasNoValue)
        {
            _logger.LogInformation($"Keine Videos für lokale Medienserver im Medienset {mediaSet.Title} vorhanden.");
            _logger.LogInformation("Überspringe Integration in die Infuse-Mediathek für dieses Medienset.");
            return Maybe<LocalMediaServerFiles>.None;
        }

        // Ermittle das Album aus den Metadaten der Video-Datei
        var albumResult = await _ffmpegMetadataService.GetMetadataFieldAsync(mediaSet.LocalMediaServerFiles.Value.VideoFile.FileInfo, "album");
        if (albumResult.IsFailure)
        {
            return Result.Failure<Maybe<LocalMediaServerFiles>>($"Das Album konnte nicht aus den Metadaten der Video-Datei {mediaSet.LocalMediaServerFiles.Value.VideoFile.FileInfo.Name} ermittelt werden: {albumResult.Error}");
        }
        Maybe<string> album = string.IsNullOrWhiteSpace(albumResult.Value) ? Maybe<string>.None : albumResult.Value;
        if (album.HasNoValue)
        {
            _logger.LogTrace($"Album-Tag ist nicht in den Metadaten der Video-Datei {mediaSet.LocalMediaServerFiles.Value.VideoFile.FileInfo.Name} vorhanden.");
            _logger.LogTrace("Das Album wird für die Integration in die Infuse-Mediathek nicht verwendet.");
        }
        else
        {
            _logger.LogTrace($"Album-Tag aus den Metadaten der Video-Datei {mediaSet.LocalMediaServerFiles.Value.VideoFile.FileInfo.Name} ermittelt: {album.Value}");
            _logger.LogTrace($"Das Album wird für die Integration in die Infuse-Mediathek als erste Verzeichnisebene verwendet.");
        }

        // Ermittle das Aufnahmedatum aus dem Titel der Video-Datei. Das Aufnahemdatum ist als ISO-String im Titel enthalten mit einem Leerzeichen getrennt.
        var recordingDate = GetRecordingDateFromTitle(mediaSet.Title);
        if (recordingDate.HasNoValue)
        {
            _logger.LogTrace($"Das Aufnahmedatum konnte nicht aus dem Titel der Video-Datei {mediaSet.LocalMediaServerFiles.Value.VideoFile.FileInfo.Name} ermittelt werden.");
            _logger.LogTrace("Das Aufnahmedatum wird für die Integration in die Infuse-Mediathek nicht verwendet.");
        }
        else
        {
            _logger.LogTrace($"Aufnahmedatum aus dem Titel der Video-Datei {mediaSet.LocalMediaServerFiles.Value.VideoFile.FileInfo.Name} ermittelt: {recordingDate.Value}");
            _logger.LogTrace($"Das Aufnahmedatum wird für die Integration in die Infuse-Mediathek als zweite Verzeichnisebene verwendet.");
        }

        _logger.LogInformation("Gefunden in den Metadaten der Video-Datei:");
        _logger.LogInformation($"Album: {album}");
        _logger.LogInformation($"Aufnahmedatum: {recordingDate}");

        var targetFilePathResult = GetTargetFilePath(mediaSet.LocalMediaServerFiles.Value.VideoFile, album.Value, mediaSet.Title, recordingDate.Value);
        if (targetFilePathResult.IsFailure)
        {
            return Result.Failure<Maybe<LocalMediaServerFiles>>($"Das Zielverzeichnis für die Integration in die Infuse-Mediathek konnte nicht ermittelt werden: {targetFilePathResult.Error}");
        }
        _logger.LogInformation($"Zielverzeichnis für die Integration in die Infuse-Mediathek ermittelt: {targetFilePathResult.Value.FullName}");

        // Verschiebe die Video-Datei in das Infuse-Mediathek-Verzeichnis und erstelle das Verzeichnis falls es nicht existiert
        var targetDirectory = targetFilePathResult.Value.Directory;
        if (targetDirectory == null)
        {
            return Result.Failure<Maybe<LocalMediaServerFiles>>($"Das Zielverzeichnis für die Integration in die Infuse-Mediathek konnte nicht ermittelt werden: {targetFilePathResult.Value.FullName}");
        }
        if (!targetDirectory.Exists)
        {
            _logger.LogInformation($"Das Zielverzeichnis für die Integration in die Infuse-Mediathek existiert nicht. Erstelle Verzeichnis: {targetDirectory.FullName}");
            var createDirectoryResult = await _fileOperations.CreateDirectoryAsync(targetDirectory.FullName);
            if (createDirectoryResult.IsFailure)
            {
                return Result.Failure<Maybe<LocalMediaServerFiles>>($"Das Zielverzeichnis für die Integration in die Infuse-Mediathek konnte nicht erstellt werden: {targetDirectory.FullName}. Fehler: {createDirectoryResult.Error}");
            }
        }
        _logger.LogInformation($"Verschiebe Video-Datei {mediaSet.LocalMediaServerFiles.Value.VideoFile.FileInfo.FullName} in das Infuse-Mediathek-Verzeichnis {targetDirectory.FullName}");
        var moveFileResult = await _fileOperations.MoveFileAsync(mediaSet.LocalMediaServerFiles.Value.VideoFile.FileInfo.FullName, targetFilePathResult.Value.FullName);
        if (moveFileResult.IsFailure)
        {
            return Result.Failure<Maybe<LocalMediaServerFiles>>($"Die Video-Datei {mediaSet.LocalMediaServerFiles.Value.VideoFile.FileInfo.FullName} konnte nicht in das Infuse-Mediathek-Verzeichnis {targetDirectory.FullName} verschoben werden. Fehler: {moveFileResult.Error}");
        }
        _logger.LogInformation($"Video-Datei {mediaSet.LocalMediaServerFiles.Value.VideoFile.FileInfo.FullName} erfolgreich in das Infuse-Mediathek-Verzeichnis {targetDirectory.FullName} verschoben.");

        // Verschiebe die Bild-Dateien in das Infuse-Mediathek-Verzeichnis. Diese haben den gleichen Namen und das gleiche Zielverzeichnis wie die Video-Datei.
        var movedSupportedImagesResult = await MoveSupportedImagesToInfuseMediaLibrary(mediaSet.LocalMediaServerFiles.Value.ImageFiles, targetDirectory, mediaSet.Title);
        if (movedSupportedImagesResult.IsFailure)
        {
            _logger.LogWarning($"Die Bild-Dateien konnten nicht in das Infuse-Mediathek-Verzeichnis {targetDirectory.FullName} verschoben werden: {movedSupportedImagesResult.Error}");
            _logger.LogInformation("Es werden keine Bild-Dateien in das Infuse-Mediathek-Verzeichnis verschoben.");
        }

        // Erstelle neues LocalMediaServerFiles-Objekt mit der verschobenen Video-Datei
        var movedSupportedVideo = SupportedVideo.Create(targetFilePathResult.Value);
        if (movedSupportedVideo.IsFailure)
        {
            return Result.Failure<Maybe<LocalMediaServerFiles>>($"Die verschobene Video-Datei {targetFilePathResult.Value.FullName} konnte nicht als SupportedVideo-Objekt erstellt werden: {movedSupportedVideo.Error}");
        }

        var localMediaServerFiles = new LocalMediaServerFiles(mediaSet.LocalMediaServerFiles.Value.ImageFiles, movedSupportedVideo.Value);
        return Maybe<LocalMediaServerFiles>.From(localMediaServerFiles);
    }

    private async Task<Result> MoveSupportedImagesToInfuseMediaLibrary(IEnumerable<SupportedImage> supportedImages, DirectoryInfo targetDirectory, string mediaSetTitle)
    {
        // Wenn kein Bild vorhanden sind, wird mit einer Info geloggt und die Methode beendet.
        if (supportedImages.Count() == 0)
        {
            _logger.LogInformation($"Keine Bild-Dateien für das Medienset {mediaSetTitle} vorhanden.");
            _logger.LogInformation("Es wird kein Bild in das Infuse-Mediathek-Verzeichnis verschoben.");
            return Result.Success();
        }

        // Wenn nur ein Bild vorhanden ist, wird dieses als Poster verwendet. Der Name des Bildes entspricht dem Namen der Video-Datei, also indirekt auch dem Namen des Mediensets.
        if (supportedImages.Count() == 1)
        {
            var supportedImage = supportedImages.First();
            var targetFilePath = Path.Combine(targetDirectory.FullName, $"{mediaSetTitle}{supportedImage.FileInfo.Extension}");
            var moveFileResult = await _fileOperations.MoveFileAsync(supportedImage.FileInfo.FullName, targetFilePath);
            if (moveFileResult.IsFailure)
            {
                return Result.Failure($"Die Bild-Datei {supportedImage.FileInfo.FullName} konnte nicht in das Infuse-Mediathek-Verzeichnis {targetDirectory.FullName} verschoben werden. Fehler: {moveFileResult.Error}");
            }
            _logger.LogInformation($"Bild-Datei {supportedImage.FileInfo.FullName} erfolgreich in das Infuse-Mediathek-Verzeichnis {targetDirectory.FullName} verschoben.");
        }

        // Wenn mehr als ein Bild vorhanden ist, dann werden die ersten zwei Bilder als Poster und Fanart verwendet und mit Hilfe des PosterAndFanartService die passenden Bilder ermittelt.
        var detectPosterAndFanartImagesResult = await _posterAndFanartService.DetectPosterAndFanartImages(supportedImages.ElementAt(0), supportedImages.ElementAt(1), targetDirectory);
        if (detectPosterAndFanartImagesResult.IsFailure)
        {
            return Result.Failure($"Das Poster und Fanart konnte nicht ermittelt werden: {detectPosterAndFanartImagesResult.Error}");
        }
        _logger.LogInformation($"Das Poster und Fanart wurde erfolgreich ermittelt.");
        _logger.LogInformation($"Poster: {detectPosterAndFanartImagesResult.Value.PosterImage.FileInfo.FullName}");
        _logger.LogInformation($"Fanart: {detectPosterAndFanartImagesResult.Value.FanartImage.FileInfo.FullName}");

        // Das Posterbild hat den gleichen Dateinamen wie das Medienset und wird in das Infuse-Mediathek-Verzeichnis verschoben.
        var posterImage = detectPosterAndFanartImagesResult.Value.PosterImage;
        var targetPosterFilePath = Path.Combine(targetDirectory.FullName, $"{mediaSetTitle}{posterImage.FileInfo.Extension}");
        var movePosterFileResult = await _fileOperations.MoveFileAsync(posterImage.FileInfo.FullName, targetPosterFilePath);
        if (movePosterFileResult.IsFailure)
        {
            return Result.Failure($"Das Posterbild {posterImage.FileInfo.FullName} konnte nicht in das Infuse-Mediathek-Verzeichnis {targetDirectory.FullName} verschoben werden. Fehler: {movePosterFileResult.Error}");
        }
        _logger.LogInformation($"Posterbild {posterImage.FileInfo.FullName} erfolgreich in das Infuse-Mediathek-Verzeichnis {targetDirectory.FullName} verschoben.");

        // Das Fanartbild hat den gleichen Dateinamen wie das Medienset mit dem Postfix definiert aus den Einstellungen und wird in das Infuse-Mediathek-Verzeichnis verschoben.
        var bannerFilePostfix = _moduleSettings.BannerFilePostfix;
        if (string.IsNullOrWhiteSpace(bannerFilePostfix))
        {
            return Result.Failure("Das Suffix des Dateinamens, das für die Banner-Datei verwendet wird für die Infuse-Mediathek als Titelbild, ist nicht definiert.");
        }
        var fanartImage = detectPosterAndFanartImagesResult.Value.FanartImage;
        var targetFanartFilePath = Path.Combine(targetDirectory.FullName, $"{mediaSetTitle}{bannerFilePostfix}{fanartImage.FileInfo.Extension}");
        var moveFanartFileResult = await _fileOperations.MoveFileAsync(fanartImage.FileInfo.FullName, targetFanartFilePath);
        if (moveFanartFileResult.IsFailure)
        {
            return Result.Failure($"Das Fanartbild {fanartImage.FileInfo.FullName} konnte nicht in das Infuse-Mediathek-Verzeichnis {targetDirectory.FullName} verschoben werden. Fehler: {moveFanartFileResult.Error}");
        }
        _logger.LogInformation($"Fanartbild {fanartImage.FileInfo.FullName} erfolgreich in das Infuse-Mediathek-Verzeichnis {targetDirectory.FullName} verschoben.");

        return Result.Success();
    }

    /// <summary>
    /// Gibt das Aufnahmedatum aus dem Titel der Video-Datei zurück.
    /// Das Aufnahemdatum ist zu Beginn des Titels als ISO-String enthalten mit einem Leerzeichen getrennt.
    /// </summary>
    /// <param name="videoFile"></param>
    /// <returns></returns>
    private Maybe<DateOnly> GetRecordingDateFromTitle(string titleFromMetadata)
    {
        if (string.IsNullOrWhiteSpace(titleFromMetadata))
            return Maybe<DateOnly>.None;

        var titleParts = titleFromMetadata.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (titleParts.Length == 0)
            return Maybe<DateOnly>.None;

        var recordingDate = titleParts[0];
        if (!DateOnly.TryParse(recordingDate, out var recordingDateValue))
            return Maybe<DateOnly>.None;

        return recordingDateValue;
    }

    /// <summary>
    /// Gibt das Zielverzeichnis für das Medienset zurück nach folgendem Schema:
    /// <Infuse-Mediathek-Verzeichnis>/<Album>/<Aufnahmedatum.JJJJ>/<Aufnahmedatum.JJJJ-MM-DD>/<Titel ohne ISO-Datum>.<Dateiendung>
    /// </summary>
    /// <param name="album"></param>
    /// <param name="recordingDate"></param>
    /// <returns></returns>
    private Result<FileInfo> GetTargetFilePath(SupportedVideo supportedVideo, string album, string title, DateOnly recordingDate)
    {
        if (_applicationSettings.InfuseMediaLibraryPath == null)
            return Result.Failure<FileInfo>("Das Infuse-Mediathek-Verzeichnis wurde nicht korrekt aus den Einstellungen geladen.");

        if (supportedVideo.FileInfo == null)
            return Result.Failure<FileInfo>("Die Quelldatei des SupportedVideo-Objekts ist null.");

        var targetDirectory = Path.Combine(_applicationSettings.InfuseMediaLibraryPath, album, recordingDate.Year.ToString(), recordingDate.ToString("yyyy-MM-dd"));

        // Der Ziel-Dateiname ist ohne vorangestelltes ISO-Datum. Dieses muss also aus dem Titel entfernt werden.
        var titleWithoutLeadingRecordingDate = title.Replace($"{recordingDate.ToString("yyyy-MM-dd")} ", string.Empty);

        var targetFileName = $"{titleWithoutLeadingRecordingDate}{supportedVideo.FileInfo.Extension}";
        var targetFilePath = Path.Combine(targetDirectory, targetFileName);
    
        return new FileInfo(targetFilePath);
    }
}

public record IntegratedMediaSetFile
{
    /// <summary>
    /// Die Quelldatei, die in das Infuse-Mediathek-Verzeichnis integriert wurde.
    /// </summary>
    public FileInfo SourceFile { get; }

    /// <summary>
    /// Die Zieldatei im Infuse-Mediathek-Verzeichnis.
    /// </summary>
    public FileInfo TargetFile { get; }

    public IntegratedMediaSetFile(FileInfo sourceFile, FileInfo targetFile)
    {
        SourceFile = sourceFile;
        TargetFile = targetFile;
    }
}