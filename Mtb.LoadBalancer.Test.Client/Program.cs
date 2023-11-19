using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

class Program
{
    const int MAX_CLIENTS = 16;
    const string LoadBalancerUrl = "https://localhost:7153/api";
    static List<RequestInfo> requestInfos = new List<RequestInfo>();

    /// <summary>
    /// Test Client Details
    /// 1) Gets # of cients for simulation(Max 16), clients will execute request in parallel to simulate concurrent users
    /// 2) Gets # of request each client will execute sequentially
    /// 3) Sets wether we want to set a fixed response time on all servers for simulation, or use the default behavior on servers
    ///     - Default behavior of API servers:
    ///     - Mtb.Api1:  Response time 90ms - 100ms
    ///     - Mtb.Api2:  Response time 90ms - 100ms
    ///     - Mtb.Api3:  First 3 requests received will have a delay of 400ms after that fixed 100ms
    ///     - Mtb.Api4:  Slowest server having perf issue Response time between 200ms to 400ms    
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    static async Task Main(string[] args)
    {
        var client = new HttpClient();
        bool continueRunning = true;

        while (continueRunning)
        {
            int numberOfClients = GetNumberOfClients();
            int requestsPerClient = GetRequestsPerClient();
            int? delayInMs = GetDelayInMilliseconds();

            Console.ForegroundColor = ConsoleColor.Green;
            DisplayServerBehaviors();
            Console.ResetColor();

            await ExecuteRequestsParallelForEachClient(client, numberOfClients, requestsPerClient, delayInMs);
            GenerateReport();

            continueRunning = PromptForContinuation();
            requestInfos.Clear(); // Resets report on every re-run
        }
    }


    /// <summary>
    /// Sets # of clients for simulation, clients will execute request in parallel to simulate concurrent users
    /// </summary>
    /// <returns></returns>
    static int GetNumberOfClients()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Enter the number of clients to simulate Max# 16:");
        Console.ResetColor();
        int numberOfClients = int.Parse(Console.ReadLine());
        return Math.Min(numberOfClients, MAX_CLIENTS); // Restricts to max alllowed clients
    }

    /// <summary>
    /// Sets # of request each client will execute sequentially
    /// </summary>
    /// <returns></returns>
    static int GetRequestsPerClient()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Enter the number of requests per client:");
        Console.ResetColor();
        return int.Parse(Console.ReadLine());
    }

    /// <summary>
    /// Sets wether we want to set a fixed response time on all servers for simulation, or use the default current behavioir set
    /// </summary>
    /// <returns></returns>
    static int? GetDelayInMilliseconds()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nEnter the response time simulation in milliseconds for each server (or leave blank for default behavior):");
        Console.ResetColor();
        string delayInput = Console.ReadLine();
        return string.IsNullOrEmpty(delayInput) ? (int?)null : int.Parse(delayInput);
    }

    /// <summary>
    /// Displays info on default behavior of API servers
    /// </summary>
    static void DisplayServerBehaviors()
    {
        Console.WriteLine("-------------------------------------------------");
        Console.WriteLine("Default behavior of servers:");
        Console.WriteLine("Mtb.Api1:  Response time 90ms - 100ms");
        Console.WriteLine("Mtb.Api2:  Response time 90ms - 100ms");
        Console.WriteLine("Mtb.Api3:  First 3 requests received will have a delay of 400ms after that fixed 100ms");
        Console.WriteLine("Mtb.Api4:  Slowest server having perf issue Response time between 200ms to 400ms");
        Console.WriteLine("-------------------------------------------------");
    }

    /// <summary>
    /// Prompt asking for another test run
    /// </summary>
    /// <returns></returns>
    static bool PromptForContinuation()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("\nTest complete. Press 'Y' to run another test or any other key to exit.");
        Console.ResetColor();
        return Console.ReadKey().Key == ConsoleKey.Y;
    }




    /// <summary>
    /// Creates a task for each client and executes them in parallel to 
    /// simulate real world where different client can send request at the same time
    /// </summary>
    /// <param name="client"></param>
    /// <param name="numberOfClients"></param>
    /// <param name="requestsPerClient"></param>
    /// <returns></returns>
    static async Task ExecuteRequestsParallelForEachClient(HttpClient client, int numberOfClients, int requestsPerClient, int? delayInMs)
    {
        var clientTasks = new Task[numberOfClients];

        for (int i = 0; i < numberOfClients; i++)
        {
            clientTasks[i] = ExecuteRequestsForSingleClient(client, i, requestsPerClient, delayInMs);
        }

        await Task.WhenAll(clientTasks);
    }

    /// <summary>
    /// This sends requests sequentially for a single client.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="clientId"></param>
    /// <param name="requestsPerClient"></param>
    /// <returns></returns>
    static async Task ExecuteRequestsForSingleClient(HttpClient client, int clientId, int requestsPerClient, int? delayInMs)
    {
        for (int j = 1; j <= requestsPerClient; j++)
        {
            await SendRequest(client, clientId * requestsPerClient + j, delayInMs);
        }
    }

    /// <summary>
    /// Sends Http Request to load balancer, if delayInMs has value it will simulate that delay on all servers.
    /// -Also capture request to response times per server to create report
    /// </summary>
    /// <param name="client"></param>
    /// <param name="requestId"></param>
    /// <returns></returns>
    static async Task SendRequest(HttpClient client, int requestId, int? delayInMs)
    {
        try
        {
            string url = delayInMs.HasValue ? $"{LoadBalancerUrl}?delayInMs={delayInMs}" : LoadBalancerUrl;
            var stopwatch = Stopwatch.StartNew();
            var response = await client.GetAsync(url);
            stopwatch.Stop();
            var content = await response.Content.ReadAsStringAsync();

            lock (requestInfos)
            {
                requestInfos.Add(new RequestInfo { ServerName = content, ResponseTime = stopwatch.ElapsedMilliseconds });
            }

            Console.WriteLine($"Request {requestId}: Server: {content} Response: {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Request {requestId} failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate the captured report and displays on console.
    /// </summary>
    static void GenerateReport()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n--- Server Response Time Report ---");

        var groupedByServer = requestInfos
            .GroupBy(info => info.ServerName)
            .Select(group => new
            {
                ServerName = group.Key,
                AverageResponseTime = group.Average(info => info.ResponseTime),
                RequestCount = group.Count()
            });

        foreach (var server in groupedByServer)
        {
            Console.WriteLine($"Server {server.ServerName} handled {server.RequestCount} requests with an average response time of {Math.Round(server.AverageResponseTime,0)}ms.");
        }

        Console.ResetColor();
    }


}
internal class RequestInfo
{
    public string ServerName { get; set; }
    public long ResponseTime { get; set; }
}
