namespace Stint.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;

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
