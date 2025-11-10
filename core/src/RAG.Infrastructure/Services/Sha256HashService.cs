using System.Security.Cryptography;
using RAG.Application.Interfaces;

namespace RAG.Infrastructure.Services;

/// <summary>
/// SHA-256 hash service implementation.
/// </summary>
public class Sha256HashService : IHashService
{
    /// <summary>
    /// Computes the SHA-256 hash of a file stream.
    /// </summary>
    /// <param name="fileStream">The file stream to hash. Stream position will be reset after hashing.</param>
    /// <returns>The SHA-256 hash as a 64-character lowercase hexadecimal string.</returns>
    public string ComputeHash(Stream fileStream)
    {
        ArgumentNullException.ThrowIfNull(fileStream);

        if (!fileStream.CanRead)
            throw new ArgumentException("Stream must be readable", nameof(fileStream));

        if (fileStream.CanSeek) fileStream.Position = 0;

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(fileStream);

        if (fileStream.CanSeek) fileStream.Position = 0;

        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
