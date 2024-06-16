using Microsoft.Extensions.Logging;
using CSharpFunctionalExtensions;
using Kurmann.Videoschnitt.InfuseMediaLibrary.Entities;
using Kurmann.Videoschnitt.CommonServices;

namespace Kurmann.Videoschnitt.InfuseMediaLibrary.Services;

public class InfuseMetadataXmlService
{
    private readonly ILogger<InfuseMetadataXmlService> _logger;
    private readonly FileTransferService _fileTransferService;

    public InfuseMetadataXmlService(ILogger<InfuseMetadataXmlService> logger, FileTransferService fileTransferService)
    {
        _logger = logger;
        _fileTransferService = fileTransferService;
    }

    public async Task<Result<List<CustomProductionInfuseMetadataFile>>> GetInfuseMetadataXmlFilesAsync(string? directoryPath)
    {
        // Prüfe, ob ein Verzeichnis angegeben wurde
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return Result.Failure<List<CustomProductionInfuseMetadataFile>>("Kein Verzeichnis angegeben.");
        }

        // Parse den Verzeichnispfad
        var directoryResult = ParseDirectoryPath(directoryPath);
        if (directoryResult.IsFailure)
        {
            return Result.Failure<List<CustomProductionInfuseMetadataFile>>("Fehler beim Parsen des Verzeichnispfads: " + directoryResult.Error);
        }

        // Ermittle alle XML-Dateien im Verzeichnis
        var directory = directoryResult.Value;
        var xmlFiles = directory.EnumerateFiles("*.xml", SearchOption.TopDirectoryOnly)
            .Where(file => file.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Informiere über die Anzahl der gefundenen XML-Dateien
        _logger.LogInformation($"Anzahl der gefundenen XML-Dateien: {xmlFiles.Count}");

        // Iteriere über XML-Dateien in den Medienset-Dateien und gib alle validen Infuse-Metadaten-XML-Dateien zurück
        var customProductionInfuseMetadataFiles = await GetCustomProductionInfuseMetadataFilesAsync(xmlFiles);

        // Informiere über die Anzahl der gefundenen Infuse-Metadaten-XML-Dateien
        _logger.LogInformation($"Anzahl der gefundenen Infuse-Metadaten-XML-Dateien: {customProductionInfuseMetadataFiles.Count}");

        return Result.Success(customProductionInfuseMetadataFiles);
    }

    /// <summary>
    /// Ermittelt alle validen Infuse-Metadaten-XML-Dateien aus einer Liste von FileInfo-Objekten.
    /// </summary>
    public async Task<List<CustomProductionInfuseMetadataFile>> GetCustomProductionInfuseMetadataFilesAsync(List<FileInfo> xmlFiles)
    {
        var customProductionInfuseMetadataFiles = new List<CustomProductionInfuseMetadataFile>();
        foreach (var infuseMetadataXmlFile in xmlFiles)
        {
            var infuseMetadataXmlContentResult = await _fileTransferService.ReadFileAsync(infuseMetadataXmlFile.FullName);
            if (infuseMetadataXmlContentResult.IsSuccess)
            {
                var infuseMetadataResult = CustomProductionInfuseMetadata.Create(infuseMetadataXmlContentResult.Value);
                if (infuseMetadataResult.IsSuccess)
                {
                    customProductionInfuseMetadataFiles.Add(new CustomProductionInfuseMetadataFile(infuseMetadataXmlFile, infuseMetadataResult.Value));
                }
                else
                {
                    // Informiere, dass die XML-Datei nicht als Infuse-Metadaten-XML-Datei erkannt wurde
                    _logger.LogWarning($"Die XML-Datei {infuseMetadataXmlFile.FullName} konnte nicht als Infuse-Metadaten-XML-Datei erkannt werden. XML-Datei wird ignoriert.");
                }
            }
            else
            {
                _logger.LogWarning($"Die XML-Datei {infuseMetadataXmlFile.FullName} konnte nicht gelesen werden: {infuseMetadataXmlContentResult.Error}");
            }
        }

        return customProductionInfuseMetadataFiles;
    }

    private Result<DirectoryInfo> ParseDirectoryPath(string directoryPath)
    {
        try
        {
            return new DirectoryInfo(directoryPath);
        }
        catch (Exception ex)
        {
            return Result.Failure<DirectoryInfo>($"Fehler beim Parsen des Verzeichnispfads: {ex.Message}");
        }
    }
}

public record CustomProductionInfuseMetadataFile(FileInfo FileInfo, CustomProductionInfuseMetadata Metadata);