using NSec.Cryptography;

namespace SpaceWay.Core.Content;

/// <summary>
/// Content hashes.
/// </summary>
public static class ContentHash
{
    /// <summary>Blake2b-256 of the rest of the stream.</summary>
    public static byte[] OfStream(Stream stream)
    {
        IncrementalHash.Initialize(HashAlgorithm.Blake2b_256, out var state);

        var buffer = new byte[64 * 1024];
        int read;
        while ((read = stream.Read(buffer)) > 0)
            IncrementalHash.Update(ref state, buffer.AsSpan(0, read));

        return IncrementalHash.Finalize(ref state);
    }
}
