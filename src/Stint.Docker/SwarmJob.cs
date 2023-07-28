namespace Stint.Docker;

using global::Docker.DotNet;
using global::Docker.DotNet.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class SwarmJob : IJob
{

    private ILogger<SwarmJob> _logger;
    private readonly DockerClient _dockerClient;
    private readonly IOptionsMonitor<ServiceCreateParameters> _serviceOptionsMonitor;

    public SwarmJob(
        ILogger<SwarmJob> logger,
        DockerClient dockerClient,
        IOptionsMonitor<ServiceCreateParameters> serviceOptionsMonitor)
    {
        _logger = logger;
        _dockerClient = dockerClient;
        _serviceOptionsMonitor = serviceOptionsMonitor;
    }

    public async Task ExecuteAsync(ExecutionInfo runInfo, CancellationToken token)
    {

        var jobName = runInfo.Name;
        var serviceOptions = _serviceOptionsMonitor.Get(jobName);
        await OnBeforeCreateService(runInfo, serviceOptions, token); // oppotunity for derived class to modify the service options before creating the service.

        string id = null;
        try
        {           
            var response = await _dockerClient.Swarm.CreateServiceAsync(serviceOptions, token);
            if (response != null)
            {
                response.Warnings?.ToList().ForEach(w => _logger.LogWarning(w));
            }
            id = response.ID;
            _logger.LogDebug("Created service {ServiceId}", id);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error creating service {ServiceName}", jobName);
            throw;
        }
        finally
        {
            if(!string.IsNullOrWhiteSpace(id))
            {
                await _dockerClient.Swarm.RemoveServiceAsync(id, token);
                _logger.LogDebug("Deleted service {ServiceId}", id);
            }
        }
        
    }

    protected virtual async Task OnBeforeCreateService(ExecutionInfo info, ServiceCreateParameters serviceCreateParams, CancellationToken cancellation)
    {

    }
}
