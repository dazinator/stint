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

            if (File.Exists(path))
            {
                var anchorText = await File.ReadAllTextAsync(path, token);
                var anchorDateTime =
                    DateTime.Parse(anchorText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                _logger.LogDebug("Anchor {anchorDateTime} loaded from file: {path}", anchorDateTime, path);
                return anchorDateTime;
            }

            _logger.LogDebug("No anchor file exists at {path}, returning null anchor.", path);
            return null;
        }

        public async Task<DateTime> DropAnchorAsync(CancellationToken token)
        {
            var path = Path.Combine(_contentPath, _name + "-anchor.txt");
            var anchor = DateTime.UtcNow;
            var anchorDateText = anchor.ToString("O");

            _logger.LogDebug("Writing anchor {anchorDateTime} to anchor file: {path}", anchorDateText, path);
            await File.WriteAllTextAsync(path, anchorDateText, token);
            _logger.LogDebug("Anchor {anchorDateText} successfully written to anchor file: {path}", anchorDateText, path);

            return anchor;
        }
    }
}
