﻿using tNavigatorModels.Project.Schedule;

namespace tNavigatorLauncher.FileParsers
{
    public partial class NavigatorFileController
    {
        public void ModifyData()
        {
            var modifier = new Modifier(launcherConfig.DataPath);

            modifier.DataText[modifier.FindIndex("START") + 1] = $" {24} {Schedule.MonthConvert(7)} {2024} /";
            
            modifier.ModifySubTagInTag("TNAVCTRL", "GPU_MODE", " GPU_MODE 4 /");
            modifier.ModifySubTagInTag("TNAVCTRL", "FRACTURE_BUILD_LOGIC", " FRACTURE_BUILD_LOGIC USE_VIRTUAL_CONNECTIONS /");


            modifier.Write();
        }
    }
}