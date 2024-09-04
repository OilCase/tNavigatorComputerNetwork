using static tNavigatorModels.Project.Schedule.Events.EnumBoreholeOperationModes;

namespace tNavigatorModels.Project.Schedule.Events
{
    public enum EnumControlTypeInjectionBorehole
    {
        /// <summary> RATE - Контроль по объему закачки </summary>
        VolumeOfLiquidInjection,

        /// <summary> BHP - Контроль по забойному давлению </summary>
        WellheadPressure
    }

    public class ChangeBoreholeToInjectionEvent : IBaseEvent
    {
        private static string ToTNavKeyWord(EnumControlTypeInjectionBorehole? controlType) => controlType switch
        {
            EnumControlTypeInjectionBorehole.VolumeOfLiquidInjection => "RATE",
            EnumControlTypeInjectionBorehole.WellheadPressure => "BHP",
            _ => "*"
        };

        public int Step { get; set; }
        public string EventTNavName => "WCONINJE";
        public string BoreholeName { get; set; }

        public EnumControlTypeInjectionBorehole? ControlType { get; set; }

        /// <summary> METRIC: rm3/day </summary>
        public double LiquidVolume { get; set; } = 1000000;

        /// <summary> METRIC: barsa </summary>
        public double WellheadPressure { get; set; } = 200;

        public EnumBoreholeOperationModes? BoreholeMode { get; set; } = OPEN;

        public string TNavString() =>
            string.Join("\t", [
                "",
                $"'{BoreholeName}'",
                "WATER",
                $"{(BoreholeMode == null ? "*" : BoreholeMode)}",
                ToTNavKeyWord(ControlType),
                LiquidVolume,
                WellheadPressure,
                "/"
            ]);
    }
}