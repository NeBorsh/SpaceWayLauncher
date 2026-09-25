using System.Buffers;

namespace SpaceWay.Core.Util;

public static class HttpDownload
{
    /// <summary>
    /// How often the progress callback fires.
    /// </summary>
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(100);

    private const int BufferSize = 64 * 1024;

    /// <summary>Downloads a URL into a stream, reporting progress.</summary>
    public static async Task DownloadToStream(
        this HttpClient http,
        string url,
        Stream destination,
        DownloadProgressCallback? progress = null,
        CancellationToken cancel = default)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength;
        progress?.Invoke(0, total);

        await using var source = await response.Content.ReadAsStreamAsync(cancel);

        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            var downloaded = 0L;
            var lastReport = DateTimeOffset.UtcNow;

            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, BufferSize), cancel);
                if (read == 0)
                    break;

                await destination.WriteAsync(buffer.AsMemory(0, read), cancel);
                downloaded += read;

                var now = DateTimeOffset.UtcNow;
                if (progress != null && now - lastReport >= ProgressInterval)
                {
                    lastReport = now;
                    progress(downloaded, total);
                }
            }

            progress?.Invoke(downloaded, total ?? downloaded);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
