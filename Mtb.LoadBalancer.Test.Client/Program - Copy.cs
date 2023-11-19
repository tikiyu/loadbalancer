//using System;
//using System.Net.Http;
//using System.Threading.Tasks;

//class Program
//{
//    static async Task Main(string[] args)
//    {
//        var client = new HttpClient();
//        bool continueRunning = true;

//        while (continueRunning)
//        {
//            Console.Clear();
//            Console.WriteLine("Enter the number of clients to simulate:");
//            int numberOfClients = int.Parse(Console.ReadLine());

//            Console.WriteLine("Enter the number of requests per client:");
//            int requestsPerClient = int.Parse(Console.ReadLine());

//            var tasks = new Task[numberOfClients * requestsPerClient];

//            for (int i = 0; i < numberOfClients; i++)
//            {
//                for (int j = 0; j < requestsPerClient; j++)
//                {
//                    int taskId = i * requestsPerClient + j;
//                    tasks[taskId] = SendRequest(client, taskId);
//                }
//            }

//            await Task.WhenAll(tasks);

//            Console.WriteLine("Test complete. Press 'Y' to run another test or any other key to exit.");
//            continueRunning = Console.ReadKey().Key == ConsoleKey.Y;
//        }
//    }


//    static async Task SendRequest(HttpClient client, int requestId)
//    {
//        try
//        {
//            string loadBalancerUrl = "https://localhost:7153/api";
//            var response = await client.GetAsync(loadBalancerUrl);
//            var content = await response.Content.ReadAsStringAsync();

//            Console.WriteLine($"Request {requestId}: {content}");
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine($"Request {requestId} failed: {ex.Message}");
//        }
//    }
//}
