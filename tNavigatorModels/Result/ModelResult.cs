﻿using System.Text.Json;

namespace tNavigatorModels.Result;

using CalculationResult = Dictionary<string, Dictionary<DateTime, double>>;

public enum EnumDataType
{
    WaterDebit,
    OilDebit,
    GasDebit,
    TotalDebit,
    Pressure,
}

public enum EnumPointKeys
{
    /// <summary> Well FlowrateReservoir </summary>
    WLPR = 0,

    /// <summary> Well FlowrateWater </summary>
    WWPR = 1,

    /// <summary> Well FlowrateOil </summary>
    WOPR = 2,

    /// <summary> Well FlowrateGas </summary>
    WGPR = 3,

    /// <summary> Well FlowrateCondensate </summary>
    WCPR = 4,

    /// <summary> Well PressureBottomhole </summary>
    WBHP = 5,

    /// <summary> Well PressureSupply </summary>
    WTHP = 6,

    /// <summary> Full FlowrateResevoir Дебит жидкости для месторождения</summary>
    FVPR = 7,

    /// <summary> Full FlowrateOil  Дебит нефти для месторождения </summary>
    FOPR = 8,

    /// <summary> Full FlowrateWater Дебит воды для месторождения</summary>
    FWPR = 9,

    /// <summary> Full FlowrateGas Дебит газа для месторождения</summary>
    FGPR = 10,

    /// <summary> Full FlowrateCondensate Дебит Конденсата для месторождения</summary>
    FCPR = 11,
}

public enum EnumCubeProperty
{
    /// <summary> Куб давления </summary>
    PRESSURE,

    /// <summary> Куб запасов газа в пластовых условиях </summary>
    RFIPGAS,

    /// <summary> Куб запасов нефти в пластовых условиях </summary>
    RFIPOIL,

    /// <summary> Куб запасов воды в пластовых условиях </summary>
    RFIPWAT,

    /// <summary> КУб насыщенности нефтью сеточных блоков  </summary>
    SOIL,

    /// <summary> КУб насыщенности газом сеточных блоков  </summary>
    SGAS,

    /// <summary> КУб насыщенности водой сеточных блоков  </summary>
    SWAT
}

public record MultiValuePoint(
    DateTime Date,
    double FlowrateReservoir,
    double FlowrateWater,
    double FlowrateOil,
    double FlowrateGas,
    double FlowrateCondensate,
    double PressureBottomhole,
    double PressureSupply);

public class BoreholeData
{
    public DateTime Date { get; set; }
    public string BoreholeName { get; set; }
    public EnumDataType DataType { get; set; }
    public List<double> MD { get; set; } = new();
    public List<double> Value { get; set; } = new();
}

public class ModelResult
{
    public string Report { get; set; } = "";
    public string TeamName { get; set; }
    public List<BoreholeData> BoreholeResults { get; set; } = new();
    public CalculationResult CalculationResult { get; set; } = new();

    public void AddCalculationResult(string data)
    {
        CalculationResult = JsonSerializer.Deserialize<CalculationResult>(data)!;
        AddCondensate();
    }

    /// <summary>
    /// Берём дебит газа в млн. м3 / сут умножаем его 
    /// на конденсато содержание (у нас оно 505 г/м3) 
    /// и делим на 1_000_000.
    /// 
    /// Получаем дебит конденсата в тонн / сут
    /// потом из тонн в сутки перевести в юниты
    /// </summary>
    public void AddCondensate()
    {
        ReCalculate("WGPR", "WCPR", GasToCondensate);
        ReCalculate("FGPR", "FCPR", GasToCondensate);
        
        return;

        void ReCalculate(string originKey, string newKey, Func<double, double> transformation)
        {
            CalculationResult
                .Where(pair => pair.Key.Contains(originKey))
                .ToList()
                .ForEach(pair =>
                {
                    var newName = pair.Key.Replace(originKey, newKey);
                    CalculationResult[newName] = pair.Value
                        .OrderBy(p => p.Key)
                        .ToDictionary(v => v.Key, v => transformation(v.Value));
                });
        }

        /// <summary> gas M3 to condensate tones </summary>
        double GasToCondensate(double gasM3)
        {
            // the proportion of condensate доля конденста в газе
            const double condensateProportion = 0.073;
            
            // condensate density плотность конденсата г/m3
            const double condensateDensity = 505;

            var condensateGramms = gasM3 * condensateProportion * condensateDensity;
            var condensateTones = condensateGramms / 1_000_000;
            return condensateTones;
        }
    }
        

    /// <summary>Возврщает значение всех дебитов с точностью в день. По месторождению</summary>
    public MultiValuePoint[] GetPoints()
    {
        if (!CalculationResult.ContainsKey($"{EnumPointKeys.FOPR}"))
            return [];

        var result = CalculationResult[$"{EnumPointKeys.FOPR}"].Keys.Select(g =>
            new MultiValuePoint(
                g,
                CalculationResult[$"{EnumPointKeys.FVPR}"][g],
                CalculationResult[$"{EnumPointKeys.FWPR}"][g],
                CalculationResult[$"{EnumPointKeys.FOPR}"][g],
                CalculationResult[$"{EnumPointKeys.FGPR}"][g],
                CalculationResult[$"{EnumPointKeys.FCPR}"][g],
                0,
                0
            )).ToArray();
        return result;
    }

    /// <summary>Возврщает значение всех дебитов с точностью в день. По скважине</summary>
    public MultiValuePoint[] GetPoints(string boreholeName)
    {
        //var x = CalculationResult.ToDictionary(p => p.Key, p => p.Value.Values.OrderByDescending(c => c));
        var boreholeParamsHistory = CalculationResult
            .Where(cr => cr.Key.Contains(boreholeName))
            .ToDictionary(pair => pair.Key.Split(':').First(), pair => pair.Value);

        if (!boreholeParamsHistory.ContainsKey($"{EnumPointKeys.WOPR}"))
            return [];

        var result = boreholeParamsHistory[$"{EnumPointKeys.WOPR}"].Keys
            .Select(g =>
                new MultiValuePoint(
                    g,
                    boreholeParamsHistory[$"{EnumPointKeys.WLPR}"][g],
                    boreholeParamsHistory[$"{EnumPointKeys.WWPR}"][g],
                    boreholeParamsHistory[$"{EnumPointKeys.WOPR}"][g],
                    boreholeParamsHistory[$"{EnumPointKeys.WGPR}"][g],
                    boreholeParamsHistory[$"{EnumPointKeys.WCPR}"][g],
                    boreholeParamsHistory[$"{EnumPointKeys.WBHP}"][g],
                    boreholeParamsHistory[$"{EnumPointKeys.WTHP}"][g]
                )
            ).ToArray();
        return result;
    }
}