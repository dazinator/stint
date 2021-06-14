namespace Stint.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public partial class StintTests
    {
        public class MockAnchorStoreFactory : IAnchorStoreFactory
        {
            private readonly Func<string, IAnchorStore> _getAnchorStore;

            public MockAnchorStoreFactory(Func<string, IAnchorStore> getAnchorStore) => _getAnchorStore = getAnchorStore;
            public int CallCount { get; set; }
            public IAnchorStore GetAnchorStore(string name) => _getAnchorStore?.Invoke(name);

        }
    }

    public class MockAnchorStore : IAnchorStore
    {

        public DateTime? CurrentAnchor { get; set; }
        public Task<DateTime> DropAnchorAsync(CancellationToken token)
        {
            CurrentAnchor = DateTime.UtcNow;
            return Task.FromResult(CurrentAnchor.Value);
        }

        public Task<DateTime?> GetAnchorAsync(CancellationToken token) => Task.FromResult(CurrentAnchor);
    }
}
