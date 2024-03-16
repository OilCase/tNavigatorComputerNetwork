﻿using System.Diagnostics;
using System.Text;
using MessageBroker;
using tNavigatorLauncher.FileParsers;
using tNavigatorModels;
using tNavigatorModels.Project;
using tNavigatorModels.Project.Schedule;


namespace tNavigatorLauncher
{
    public class Launcher(LauncherConfig launcherConfig, Project project)
    {
        public string? Output { get; set; }

        public NavigatorFileController FileController { get; set; } = new(launcherConfig, project);

        public static void SendTask(BrokerConfig config, Project project)
        {
            var calculationBroker = config.GetBroker(BrokerQueue.ModelReadyCalculation);
            calculationBroker.PublishMessage(JsonUtil.Serialize(project));
        }


        public tNavigatorModels.Result.ModelResult Start()
        {
            //CreateProjectFiles();
            //TNavigatorRun();
            var calculationResult = ReadCalculationResult();
            return calculationResult;
        }

        public void CreateProjectFiles()
        {
            FileController.ModifyData();
            FileController.InitSchedule();
            FileController.InitBoreholes();
        }

        /// <returns>Calculation result directory</returns>
        public void TNavigatorRun()
        {
            try
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

                process.Start();
                Output = process.StandardOutput.ReadToEnd();

                process.WaitForExit();

                if (!string.IsNullOrEmpty(Output))
                    Console.WriteLine(Output);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message} {Output} ");
            }
        }

        public tNavigatorModels.Result.ModelResult ReadCalculationResult()
        {
            return new tNavigatorModels.Result.ModelResult()
            {
                TeamName = project.Team.Name,
                BoreholeResults = Schedule.DebitDirName.Keys.SelectMany(FileController.GetDebit).ToList()
            };
        }
    }
}