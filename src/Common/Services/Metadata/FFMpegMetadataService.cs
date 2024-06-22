using CSharpFunctionalExtensions;
using Kurmann.Videoschnitt.Common;
using Microsoft.Extensions.Logging;

namespace Kurmann.Videoschnitt.LocalFileSystem.Services.Metadata;

/// <summary>
/// Verantwortlich für das Extrahieren von Metadaten aus Medien-Dateien mit FFmpeg.
/// </summary>
public class FFmpegMetadataService
{
    private readonly ExecuteCommandService _executeCommandService;
    private readonly ILogger<FFmpegMetadataService> _logger;

    public FFmpegMetadataService(ExecuteCommandService executeCommandService, ILogger<FFmpegMetadataService> logger)
    {
        _executeCommandService = executeCommandService;
        _logger = logger;
    }

    /// <summary>
    /// Gibt die Roh-Metadaten eines Medien-Files zurück im FFmpeg-Format.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    public async Task<Result<string>> GetRawMetadataAsync(string filePath)
    {
        var arguments = $"-i \"{filePath}\" -f ffmetadata -";
        var result = await _executeCommandService.ExecuteCommandAsync("ffmpeg", arguments);

        if (result.IsSuccess)
        {
            var rawMetadata = string.Join("\n", result.Value);
            return Result.Success(rawMetadata);
        }

        _logger.LogError($"Error retrieving FFmpeg metadata: {result.Error}");
        return Result.Failure<string>(result.Error);
    }

    public async Task<Result<string>> GetMetadataFieldAsync(FileInfo fileInfo, string field)
    {
        return await GetMetadataFieldAsync(fileInfo.FullName, field);
    }

    public async Task<Result<string>> GetMetadataFieldAsync(string filePath, string field)
    {
        var arguments = $"-v quiet -show_entries format_tags={field} -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"";
        var result = await _executeCommandService.ExecuteCommandAsync("ffprobe", arguments);

        if (result.IsSuccess)
        {
            var metadataValue = string.Join("\n", result.Value).Trim();
            return Result.Success(metadataValue);
        }

        _logger.LogError($"Error retrieving FFprobe metadata field '{field}': {result.Error}");
        return Result.Failure<string>(result.Error);
    }

    public async Task<Result<string>> GetTitleAsync(string filePath)
    {
        return await GetMetadataFieldAsync(filePath, "title");
    }

    public async Task<Result<string>> GetDescriptionAsync(string filePath)
    {
        return await GetMetadataFieldAsync(filePath, "description");
    }
}