using System.Diagnostics;
using System.Net;
using MessageBroker;
using System.Text;
using System.Text.Json;
using tNavigatorLauncher;
using tNavigatorModels;
using tNavigatorModels.Project;
using tNavigatorModels.Result;
using Utils;
using System;

namespace tNavigatorComputingNode;

public class Program
{

    static void Main(string[] args)
    {
        Console.Title = nameof(tNavigatorComputingNode);

        Log.Write("Node is up\n");

        var host = Dns.GetHostName();

        const string kos4v = "W10534";

        string configPath = host switch
        {
            kos4v => "config.Development.json",
            _ => "config.json"
        };
        var config = NodeConfig.LoadConfig(configPath);
        IMessageBroker? brokerForConsumeTask = null;

        while (true){
            try
            {
                brokerForConsumeTask?.ConsumeCancelTokenSource.Cancel();
                brokerForConsumeTask = config.GetBroker(BrokerQueue.ModelReadyCalculation);
                brokerForConsumeTask.ConsumeMessageAsync(Calculate).Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex);
            }
        }

        return;

        void Calculate(byte[] message)
        {
            var sw = Stopwatch.StartNew();
            LauncherConfig? launcherConfig = null;
            var resultMessage = Encoding.UTF8.GetString(message);
            var project = JsonSerializer.Deserialize<Project>(resultMessage)!;
            var result = new ModelResult()
            {
                TeamName = project.Team.Name
            };

            try
            {
                Attempt(() => SendResult(project.ResultAddress, "Calculation"));
                launcherConfig = new LauncherConfig(config.TNavPath, config.ProjectDirPath, project.ConverterAddress);
                var launcher = new Launcher(launcherConfig, project);
                var resultTask = Task.Run(launcher.Start);
                Log.Write($"Calculate is start {project.ResultAddress.Split("?").First()}");


                while (CaseIsLive(project.ResultAddress) & !resultTask.IsCompleted)
                {
                    Thread.Sleep(5000);
                }
                if (!resultTask.IsCompleted)
                {
                    Log.Write($"Calculate is cancelled\n");
                    return;
                }

                result = resultTask.Result;
                result.Report += $"Time Complete: {sw.Elapsed:g}";

                Attempt(() => SendResult(project.ResultAddress, "Received", result));
                Attempt(() => SendFile(project.ResultAddress, launcherConfig.UnrstPath));
                Log.Write("Calculate is complete");
            }
            catch (Exception e)
            {
                Log.Write(result.Report += e.Message + e +
                                     $"Time Complete: {sw.Elapsed:g} {JsonSerializer.Serialize(launcherConfig)}");
            }

            Log.Write("Iteration complete\n");
            
        }

        bool CaseIsLive(string url)
        {
            try
            {
                using var client = new HttpClient();
                using var response = client.GetAsync($"{url}&onlyStatus=true").Result;

                string responseContent = response.Content.ReadAsStringAsync().Result;
                var result = !(responseContent.Contains("Cancelled") || responseContent.Contains("Received"));
                return result;
            }
            catch (Exception e) {
                Log.Write($"{e.Message}\n{e}");
                return true;
            }
        }

        // status: OilCaseX.Model.Calculation.EnumCalculationStatus
        // Результаты в очереди: Sent
        // Расчёт завершён: Received,
        // Выполняется расчёт: Calculation,
        // Расчёт отменен: Cancelled,    
        HttpStatusCode SendResult(string url, string status, ModelResult? result = null)
        {
            var jsonPayload = JsonSerializer.Serialize(result);
            using var client = new HttpClient();
            StringContent content = new(jsonPayload, Encoding.UTF8, "application/json");

            using var response = client.PatchAsync($"{url}&calculationStatus={status}", content).Result;
            var res = response.Content.ReadAsStringAsync().Result;
            Log.Write($"Status: {status}, response: {response.StatusCode}, id: {res}");

            return response.StatusCode;
        }

        HttpStatusCode? SendFile(string url, string? filePath)
        {
            if (filePath == null) return null;

            using var httpClient = new HttpClient();
            using var formData = new MultipartFormDataContent();

            using var fileStream = File.OpenRead(filePath);
            formData.Add(new StreamContent(fileStream), "file", Path.GetFileName(filePath));

            using var response = httpClient.PostAsync(url, formData).Result;
            string responseContent = response.Content.ReadAsStringAsync().Result;

            return response.StatusCode;
        }

        T Attempt<T>(Func<T> action, int attemptCount = 5, int sleepTimeMS = 5000)
        {
            string? errorMessage = null;

            while (attemptCount > 0)
            {
                try
                {
                    return action();
                }
                catch (Exception e)
                {
                    attemptCount--;
                    errorMessage = "!!! SendFile Exception: \n" + e.Message + e;
                    Thread.Sleep(sleepTimeMS);
                }
            }
         
            return action();
        }
    }

}