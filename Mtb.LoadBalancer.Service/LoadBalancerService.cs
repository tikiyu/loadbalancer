using System.Diagnostics;
using System.Net;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;


/// <summary>
/// Load Balancer Service Details.
/// 1) Ensures all server will receive initial requests distributed. No server should have 0 request
/// 2) Order the server to fastest AverageResponseTime and checks if ResponseTime is Within Threshold, for example 3 servers are within threshold and 1 is not(or slow) will still do round robin selection between 3 servers
///    - This ensures that the LoadBalancer will not be bias to forward most request on just one server that has fastest average as long as it is within threshold, making sure we are distributing load.
/// 2) If a request fail on a server it will retry the requests on the next available servers else return status 503
/// 3) If a server has responded slow it will be ExcludeTemporarily for 3 seconds and will not be forwarded a request after the ExclusionTimeInSecs has expired it will then be reintegrated/re-added on list of servers available
///    - This ensures that we still try to forward a request on slow server after exclusion time in case it recovers from its performance issue  
/// </summary>
internal class LoadBalancerService
{
    private readonly List<Server> _servers;
    private readonly HttpClient _httpClient;
    private readonly ILogger<LoadBalancerService> _logger;
    private int _lastSelectedServerIndex = -1;
    public LoadBalancerService(ILogger<LoadBalancerService> logger)
    {
        var serverUrls = new Dictionary<string, string>
        {
            { "Mtb.Api1", "https://localhost:7268" },
            { "Mtb.Api2", "https://localhost:7278" },
            { "Mtb.Api3", "https://localhost:7213" },
            { "Mtb.Api4", "https://localhost:7098"}
        };
        _servers = serverUrls.Select(kvp => new Server(kvp.Key, kvp.Value)).ToList();
        _httpClient = new HttpClient();
        _logger = logger;
    }


    public async Task<HttpResponseMessage> ForwardRequest(HttpRequest request)
    {
        HttpResponseMessage response = null;
        int attempt = 0;

        // Loop to try each server until a successful response or all servers have been tried
        while (attempt < _servers.Count)
        {
            var server = SelectServerBasedOnResponseTime();// Select a server based on the response time
            server.IncrementRequestCount();

            var targetUri = new Uri(server.BaseUrl, request.Path + request.QueryString);

            _logger.LogInformation($"Forwarding request to {server.Name} at {server.BaseUrl}");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                response = await _httpClient.GetAsync(targetUri); // Attempt to forward the request to the selected server
                stopwatch.Stop();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Server {server.Name} responded in {stopwatch.ElapsedMilliseconds} ms");
                    server.UpdateAverageResponseTime(stopwatch.Elapsed);
               
                    break;// Exit loop on successful response
                }
                else
                {
                    _logger.LogWarning($"Server {server.Name} responded with error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurred while sending request to {server.Name}");
            }

            attempt++;
        }
        // Return the response or a default ServiceUnavailable response if all attempts fail
        return response ?? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
    }


    //private Server SelectServerBasedOnResponseTime()
    //{
    //    // Check if all servers have the same response time or on initial value of 0
    //    if (_servers.All(s => s.AverageResponseTime == _servers[0].AverageResponseTime))
    //    {
    //        // Implement round-robin selection
    //        _lastSelectedServerIndex = (_lastSelectedServerIndex + 1) % _servers.Count;
    //        return _servers[_lastSelectedServerIndex];
    //    }

    //    // If servers have different avg response times, use this logic
    //    return _servers.OrderBy(s => s.AverageResponseTime).FirstOrDefault();
    //}

    //  private int _lastSelectedServerIndex = -1;
    private const double ResponseTimeThreshold = 0.15; // Threshold in seconds

    private Server SelectServerBasedOnResponseTime()
    {
        ReintegrateSlowServersIfNeeded();

        var availableServers = _servers.Where(s => !s.IsTemporarilyExcluded).ToList();
        if (availableServers.Count == 0)
        {
            // If all servers are excluded, fall back to the full list
            availableServers = _servers;
        }

        // Prioritize servers that were just reintegrated
        var reintegratedServer = availableServers.FirstOrDefault(s => s.JustReintegrated);
        if (reintegratedServer != null)
        {
            reintegratedServer.MarkAsServed();
            return reintegratedServer;
        }


        var orderedServers = availableServers.OrderBy(s => s.AverageResponseTime).ToList();

        var isServerWithNoRequest = orderedServers.Any(s => s.AverageResponseTime == TimeSpan.Zero); //check if all servers has been forwarded a request

        if (orderedServers.Count > 1 &&
            !AreResponseTimesWithinThreshold(orderedServers) &&
            !isServerWithNoRequest)
        {
            // Exclude the slowest server temporarily if it is significantly slower
            orderedServers.Last().ExcludeTemporarily();
            return orderedServers.First();
        }

        // Use round-robin among available servers
        _lastSelectedServerIndex = (_lastSelectedServerIndex + 1) % orderedServers.Count;
        return orderedServers[_lastSelectedServerIndex];
    }

    private void ReintegrateSlowServersIfNeeded()
    {
        foreach (var server in _servers.Where(server => server.ShouldReintegrate()))
        {
            server.Reintegrate();
        }
    }

    private bool AreResponseTimesWithinThreshold(List<Server> servers)
    {
        double minResponseTime = servers.Min(s => s.AverageResponseTime.TotalSeconds);
        double maxResponseTime = servers.Max(s => s.AverageResponseTime.TotalSeconds);
        return (maxResponseTime - minResponseTime) <= ResponseTimeThreshold;
    }

}
