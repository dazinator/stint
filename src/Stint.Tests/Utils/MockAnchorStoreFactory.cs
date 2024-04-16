namespace Stint.Tests
{
    using System;

    public class MockAnchorStoreFactory : IAnchorStoreFactory
    {
        private readonly Func<string, IAnchorStore> _getAnchorStore;

        public MockAnchorStoreFactory(Func<string, IAnchorStore> getAnchorStore) => _getAnchorStore = getAnchorStore;
        public int CallCount { get; set; }
        public IAnchorStore GetAnchorStore(string name) => _getAnchorStore?.Invoke(name);

    }
}
