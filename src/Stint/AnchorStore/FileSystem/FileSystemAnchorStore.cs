namespace Stint
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;

    public class FileSystemAnchorStore : IAnchorStore
    {
        private readonly string _contentPath;
        private readonly string _name;
        private readonly ILogger<FileSystemAnchorStore> _logger;

        public FileSystemAnchorStore(string contentPath, string name, ILogger<FileSystemAnchorStore> logger)
        {
            _contentPath = contentPath;
            _name = name;
            _logger = logger;
        }

        public async Task<DateTime?> GetAnchorAsync(CancellationToken token)
        {
            var path = Path.Combine(_contentPath, _name + "-anchor.txt");
            _logger.LogDebug("Getting anchor from {path}", path);

            int retryCount = 0;
            int maxRetries = 3;
            int delayMilliseconds = 1000; // Initial delay of 1 second

            while (true)
            {
                try
                {
                    if (!File.Exists(path))
                    {
                        _logger.LogDebug("No anchor file exists at {path}, returning null anchor.", path);
                        return null;
                    }

                    using (var outputFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var reader = new StreamReader(outputFile))
                        {
                            var anchorText = await reader.ReadToEndAsync();
                            if (!DateTime.TryParse(anchorText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
                            {
                                _logger.LogWarning("Anchor file {path} did not contain valid datetime. Returning null anchor.", path);
                                return null;
                            }

                            _logger.LogDebug("Anchor {anchorDateTime} loaded from file: {path}", result, path);
                            return result;
                        }
                    }
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Attempt {retry} failed to read anchor file: {path}. Retrying...", retryCount + 1, path);
                    if (retryCount++ >= maxRetries || token.IsCancellationRequested)
                    {
                        _logger.LogError("Maximum retry attempts reached or operation cancelled, failing with IOException.");
                        throw;
                    }

                    await Task.Delay(delayMilliseconds, token);
                    delayMilliseconds *= 2; // Exponential backoff: double the delay each retry
                }

                // if (File.Exists(path))
                // {
                //     try
                //     {
                //
                //         // seems to be not working on linux with cifs file share
                //         //   // var anchorText = await File.ReadAllTextAsync(path, token,);
                //         using (var outputFile = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                //         {
                //             using (var reader = new StreamReader(outputFile))
                //             {
                //                 var anchorText = await reader.ReadToEndAsync();
                //                 if (!DateTime.TryParse(anchorText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
                //                 {
                //                     _logger.LogWarning("Anchor file {path} did not contain valid datetime. Returning null anchor.", path);
                //                     return null;
                //                 }
                //
                //                 _logger.LogDebug("Anchor {anchorDateTime} loaded from file: {path}", result, path);
                //                 return result;
                //             }
                //         }
                //
                //         // using (var outputFile = new StreamReader(path, true))
                //         // {
                //         //     var anchorText = await outputFile.ReadToEndAsync();
                //         //     if (!DateTime.TryParse(anchorText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
                //         //     {
                //         //         _logger.LogWarning("Anchor file {path} did not contain valid datetime. Returning null anchor.", path);
                //         //         return null;
                //         //     }
                //         //
                //         //     _logger.LogDebug("Anchor {anchorDateTime} loaded from file: {path}", result, path);
                //         //     return result;
                //         // }
                //  }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to read contents of anchor file: {path}", path);
                    throw;
                }
            }
        }

        public Task<DateTime> DropAnchorAsync(CancellationToken token)
        {
            var path = Path.Combine(_contentPath, _name + "-anchor.txt");
            var anchor = DateTime.UtcNow;
            var anchorDateText = anchor.ToString("O");

            _logger.LogDebug("Writing anchor {anchorDateTime} to anchor file: {path}", anchorDateText, path);

            // seeing odd issues with async io on linux writing to cifs share- empty file is left orphaned.
            // so switching to sync io, with async writes to the stream.

            // https://github.com/dotnet/runtime/issues/23196
            // await File.WriteAllTextAsync(path, anchorDateText, token);

            try
            {
                // https://github.com/dotnet/runtime/issues/42790#issuecomment-700362617
                using (var outputFile = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                {
                    using (var writer = new StreamWriter(outputFile))
                    {
                        writer.Write(anchorDateText);
                    }
                }

                _logger.LogDebug("Anchor {anchorDateText} successfully written to anchor file: {path}", anchorDateText, path);
                return Task.FromResult(anchor);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unable to write anchor {anchorDateText} to file: {path}", anchorDateText, path);
                throw;
            }
        }
    }
}
