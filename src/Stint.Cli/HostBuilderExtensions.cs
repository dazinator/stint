namespace Stint
{
    using Microsoft.Extensions.Hosting;

    public static class HostBuilderExtensions
    {
        public static IHostBuilder UseCrossPlatformService(this IHostBuilder builder) =>
            builder
                .UseWindowsService()
                .UseSystemd();
    }
}
