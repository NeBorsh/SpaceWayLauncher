namespace SpaceWay.Core.Util;

/// <summary>Bytes downloaded out of total. Total is null if unknown.</summary>
public delegate void DownloadProgressCallback(long downloaded, long? total);
