namespace Stint
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    public class FileSystemAnchorStore : IAnchorStore
    {
        private readonly string _contentPath;
        private readonly string _name;

        public FileSystemAnchorStore(string contentPath, string name)
        {
            _contentPath = contentPath;
            _name = name;
        }

        public async Task<DateTime?> GetAnchorAsync(CancellationToken token)
        {
            var path = Path.Combine(_contentPath, _name + "-anchor.txt");
            if (File.Exists(path))
            {
                var anchorText = await File.ReadAllTextAsync(path, token);
                var anchorDateTime =
                    DateTime.Parse(anchorText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                return anchorDateTime;
            }

            return null;
        }

        public async Task<DateTime> DropAnchorAsync(CancellationToken token)
        {
            var path = Path.Combine(_contentPath, _name + "-anchor.txt");
            var anchor = DateTime.UtcNow;
            var anchorDateText = anchor.ToString("O");
            await File.WriteAllTextAsync(path, anchorDateText, token);
            return anchor;
        }
    }
}
