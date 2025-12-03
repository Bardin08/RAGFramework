using RAG.Core.Constants;
using RAG.Core.Exceptions;

namespace RAG.Application.Exceptions;

/// <summary>
/// Exception thrown when file size exceeds the maximum allowed limit.
/// Maps to HTTP 413 Payload Too Large.
/// </summary>
public class FileSizeException : RagException
{
    public long? FileSize { get; }
    public long? MaxAllowedSize { get; }

    public FileSizeException(string message)
        : base(message, ErrorCodes.FileTooLarge)
    {
    }

    public FileSizeException(string message, Exception innerException)
        : base(message, ErrorCodes.FileTooLarge, innerException)
    {
    }

    public FileSizeException(long fileSize, long maxAllowedSize)
        : base(
            $"File size ({FormatBytes(fileSize)}) exceeds maximum allowed size ({FormatBytes(maxAllowedSize)})",
            ErrorCodes.FileTooLarge,
            new Dictionary<string, object>
            {
                ["fileSize"] = fileSize,
                ["maxAllowedSize"] = maxAllowedSize
            })
    {
        FileSize = fileSize;
        MaxAllowedSize = maxAllowedSize;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        var order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
