namespace RAG.Application.Interfaces;

/// <summary>
/// Service for computing cryptographic hashes of files.
/// </summary>
public interface IHashService
{
    /// <summary>
    /// Computes the SHA-256 hash of a file stream.
    /// </summary>
    /// <param name="fileStream">The file stream to hash. Stream position will be reset after hashing.</param>
    /// <returns>The SHA-256 hash as a 64-character lowercase hexadecimal string.</returns>
    string ComputeHash(Stream fileStream);
}
