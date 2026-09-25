using System.Text;
using NSec.Cryptography;
using NUnit.Framework;
using SpaceWay.Vendor.ZStd;

namespace SpaceWay.Core.Tests;

/// <summary>
/// Native content dependencies: zstd and Blake2b.
/// </summary>
[TestFixture]
public sealed class ContentHashingTests
{
    [Test]
    public void Blake2BMatchesKnownVector()
    {
        var hash = HashAlgorithm.Blake2b_256.Hash([]);

        Assert.That(
            Convert.ToHexString(hash).ToLowerInvariant(),
            Is.EqualTo("0e5751c026e543b2e8ab2eb06099daa1d1e5df47778f7787faab45cdf12fe3a8"));
    }

    [Test]
    public void Blake2BIncrementalMatchesOneShot()
    {
        var data = Encoding.UTF8.GetBytes(new string('ы', 5000));

        IncrementalHash.Initialize(HashAlgorithm.Blake2b_256, out var state);
        IncrementalHash.Update(ref state, data.AsSpan(0, 1234));
        IncrementalHash.Update(ref state, data.AsSpan(1234));
        var incremental = IncrementalHash.Finalize(ref state);

        Assert.That(incremental, Is.EqualTo(HashAlgorithm.Blake2b_256.Hash(data)));
    }

    [Test]
    public void ZStdRoundTripsData()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("контент ", 1000)));

        using var compressor = new ZStdCCtx();
        var compressed = new byte[ZStd.CompressBound(original.Length)];
        var compressedLength = compressor.Compress(compressed, original);

        using var decompressor = new ZStdDCtx();
        var restored = new byte[original.Length];
        var restoredLength = decompressor.Decompress(restored, compressed.AsSpan(0, compressedLength));

        Assert.Multiple(() =>
        {
            Assert.That(restoredLength, Is.EqualTo(original.Length));
            Assert.That(restored, Is.EqualTo(original));
            Assert.That(compressedLength, Is.LessThan(original.Length), "compression had no effect");
        });
    }

    [Test]
    public void ZStdDecompressStreamReadsCompressedData()
    {
        var original = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("поток ", 2000)));

        using var compressor = new ZStdCCtx();
        var compressed = new byte[ZStd.CompressBound(original.Length)];
        var compressedLength = compressor.Compress(compressed, original);

        using var source = new MemoryStream(compressed, 0, compressedLength);
        using var stream = new ZStdDecompressStream(source);
        using var restored = new MemoryStream();
        stream.CopyTo(restored);

        Assert.That(restored.ToArray(), Is.EqualTo(original));
    }
}
