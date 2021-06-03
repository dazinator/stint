namespace Stint
{
    public class FileSystemAnchorStoreFactory : IAnchorStoreFactory
    {
        private readonly string _basePath;

        public FileSystemAnchorStoreFactory(string basePath) => _basePath = basePath;
        public IAnchorStore GetAnchorStore(string name) => new FileSystemAnchorStore(_basePath, name);
    }
}
