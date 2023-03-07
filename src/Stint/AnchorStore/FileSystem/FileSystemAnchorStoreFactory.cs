namespace Stint
{
    using Microsoft.Extensions.Logging;

    public class FileSystemAnchorStoreFactory : IAnchorStoreFactory
    {
        private readonly string _basePath;
        private readonly ILoggerFactory _loggerFacory;

        public FileSystemAnchorStoreFactory(string basePath, ILoggerFactory loggerFacory)
        {
            _basePath = basePath;
            _loggerFacory = loggerFacory;
        }
        public IAnchorStore GetAnchorStore(string name)
        {
            var logger = _loggerFacory.CreateLogger<FileSystemAnchorStore>();
            return new FileSystemAnchorStore(_basePath, name, logger);

        }
    }
}
