using System.Diagnostics;
using System.Net;
using MessageBroker;
using System.Text;
using System.Text.Json;
using tNavigatorLauncher;
using tNavigatorModels;
using tNavigatorModels.Project;
using tNavigatorModels.Result;

namespace tNavigatorComputingNode;

internal class Program
{
    public static object LockObject { get; set; } = new();

    static void Main(string[] args)
    {
        Console.Title = nameof(tNavigatorComputingNode);
        Log("Start");

        var host = Dns.GetHostName();
        string configPath = "config.json";

        var config = NodeConfig.LoadConfig(configPath);
        while (true)
        {
            try
            {
                var brokerForConsumeTask = config.GetBroker(BrokerQueue.Hakasia);
                brokerForConsumeTask.ConsumeMessageAsync(Calculate);
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Log($"{ex.Message} {ex}");
                Thread.Sleep(1000);
            }
        }

        return;

        async void Calculate(byte[] message)
        {
            var sw = Stopwatch.StartNew();
            LauncherConfig? launcherConfig = null;
            var resultMessage = Encoding.UTF8.GetString(message);
            var project = JsonSerializer.Deserialize<Project>(resultMessage)!;
            var result = new ModelResult()
            {
                TeamName = project.Team.Name
            };

            _ = SendResult(project.ResultAddress, "Calculation");

            try
            {
                Log("Calculate");
                launcherConfig = new LauncherConfig(config.TNavPath, config.ProjectDirPath, project.ConverterAddress);
                var launcher = new Launcher(launcherConfig, project);
                result = launcher.Start();
                result.Report += $"Time Complete: {sw.Elapsed:g}";
                Log("Calculate complete");
            }
            catch (Exception e)
            {
                Log(result.Report += e.Message + e +
                                     $"Time Complete: {sw.Elapsed:g} {JsonSerializer.Serialize(launcherConfig)}");
            }

            await SendResult(project.ResultAddress, "Received", result);
            await SendFile(project.ResultAddress, launcherConfig.UnrstPath);
            Log("Iteration complete");
        }


        // status: OilCaseX.Model.Calculation.EnumCalculationStatus
        // Результаты в очереди: Sent
        // Расчёт завершён: Received,
        // Выполняется расчёт: Calculation,
        // Расчёт отменен: Cancelled,    
        async Task SendResult(string url, string status, ModelResult? result = null)
        {
            var jsonPayload = JsonSerializer.Serialize(result);
            using var client = new HttpClient();
            StringContent content = new(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PatchAsync($"{url}&calculationStatus={status}", content);
                response.EnsureSuccessStatusCode();
                var res = await response.Content.ReadAsStringAsync();

                Log($"{res} {response.StatusCode}");
            }
            catch (Exception e)
            {
                Log("SendResult Exception: " + e.Message + e);
            }
        }

        async Task SendFile(string url, string? filePath)
        {
            if (filePath == null) return;

            try
            {
                using var client = new HttpClient();
                using var httpClient = new HttpClient();
                using var formData = new MultipartFormDataContent();

                await using var fileStream = File.OpenRead(filePath);
                formData.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));

                using var response = await httpClient.PostAsync(url, formData);

                string responseContent = await response.Content.ReadAsStringAsync();
            }
            catch (Exception e)
            {
                Log("SendFile Exception: " + e.Message + e);
            }
        }
    }

    public static void Log(string message)
    {
        lock (LockObject)
        {
            File.AppendAllLines("log.txt", [$"---{DateTime.Now}", message, "\n"]);
        }

        try
        {
            Console.WriteLine(message);
        }
        catch
        {
            // ignored
        }
    }
}