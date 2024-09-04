using System.Diagnostics;
using System.Text;
using System.Text.Json;
using MessageBroker;
using tNavigatorLauncher.FileParsers;
using tNavigatorModels;
using tNavigatorModels.Project;
using tNavigatorModels.Project.Schedule;
using Utils;


namespace tNavigatorLauncher
{
    public class Launcher(LauncherConfig launcherConfig, Project project)
    {
        private object LockObject = new();
        public string? Output { get; set; }

        public NavigatorFileController FileController { get; set; } = new(launcherConfig, project);

        public static async Task SendTask(BrokerConfig config, Project project)
        {
            var calculationBroker = config.GetBroker(BrokerQueue.Hakasia);
            var data = JsonSerializer.Serialize(project);
            await calculationBroker.PublishMessage(data);
        }


        public tNavigatorModels.Result.ModelResult Start()
        {
            CreateProjectFiles();
            TNavigatorRun();
            var calculationResult = ReadCalculationResult();
            return calculationResult;
        }

        public void CreateProjectFiles()
        {
            FileController.ModifyData();
            FileController.ModifySolution();
            FileController.InitSchedule();
            FileController.InitBoreholes();
            
            Dir.ReCreateWorkDir(launcherConfig.ResultDirPath);
        }

        /// <returns> Calculation result directory </returns>
        public void TNavigatorRun()
        {
            try
            {
                // Проверка, запущен ли уже такой процесс
                var existingProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(launcherConfig.TNavigatorConsoleExePath));
                foreach (var existingProcess in existingProcesses)
                {
                    try
                    {
                        existingProcess.Kill();
                        existingProcess.WaitForExit();
                        Console.WriteLine($"Завершён предыдущий процесс: {existingProcess.ProcessName} {existingProcess.Id}");

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при завершении процесса: {ex.Message}");
                    }
                }

                lock (LockObject)
                {
                    using Process process = new()
                    {
                        StartInfo = new()
                        {
                            FileName = launcherConfig.TNavigatorConsoleExePath,
                            Arguments = launcherConfig.TNavLaunchArgs,
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            StandardOutputEncoding = Encoding.UTF8,
                        }
                    };

                    var cursorPosition = Console.GetCursorPosition();

                    process.OutputDataReceived += (sender, e) => {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            if (e.Data.Contains("На шаге"))
                            {
                                Console.SetCursorPosition(cursorPosition.Left, cursorPosition.Top);
                                Console.Write(new string(' ', Console.WindowWidth));
                                Console.WriteLine(e.Data);
                            }
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.WaitForExit();

                    //process.Start();
                    //Output = process.StandardOutput.ReadToEnd();
                    //process.WaitForExit();
                }

                if (!string.IsNullOrEmpty(Output))
                {
                    Log.Write(Output, false);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message} {Output} ");
            }
        }

        public tNavigatorModels.Result.ModelResult ReadCalculationResult()
        {
            var data = FileController.SmspecUnsmryParser(launcherConfig);
            var result = new tNavigatorModels.Result.ModelResult()
            {
                TeamName = project.Team.Name,
                BoreholeResults = Schedule.DebitDirName.Keys.SelectMany(FileController.GetDebit).ToList(),
            };
            result.AddCalculationResult(data);
            return result;
        }
    }
}