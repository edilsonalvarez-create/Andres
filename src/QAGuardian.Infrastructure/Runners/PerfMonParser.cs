using System.Globalization;

namespace QAGuardian.Infrastructure.Runners;

/// <summary>Resumen de uso de recursos del servidor bajo prueba (PerfMon).</summary>
public record PerfMonSummary(
    bool HasData,
    double CpuAvgPercent,
    double CpuMaxPercent,
    double MemoryAvgMb,
    double MemoryMaxMb);

/// <summary>
/// Parsea el archivo de resultados del PerfMon Metrics Collector de JMeter (formato JTL/CSV).
/// El valor de la métrica va en la columna <c>elapsed</c> y su tipo se identifica por la
/// columna <c>label</c> (que contiene "cpu" o "mem"). CPU se normaliza a porcentaje;
/// memoria se asume en bytes y se convierte a MB.
/// </summary>
public static class PerfMonParser
{
    public static PerfMonSummary Parse(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0) return Empty();

        var header = lines[0].Split(',');
        var hasHeader = header.Any(h => h.Equals("label", StringComparison.OrdinalIgnoreCase));

        int iElapsed = 1, iLabel = 2;
        if (hasHeader)
        {
            int Idx(string name) => Array.FindIndex(header, h => h.Equals(name, StringComparison.OrdinalIgnoreCase));
            iElapsed = Idx("elapsed");
            iLabel = Idx("label");
        }
        if (iElapsed < 0 || iLabel < 0) return Empty();

        var cpu = new List<double>();
        var memory = new List<double>();

        foreach (var line in lines.Skip(hasHeader ? 1 : 0))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var parts = line.Split(',');
            if (parts.Length <= Math.Max(iElapsed, iLabel)) continue;
            if (!double.TryParse(parts[iElapsed], NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                continue;

            var label = parts[iLabel].ToLowerInvariant();
            if (label.Contains("cpu")) cpu.Add(value);
            else if (label.Contains("mem")) memory.Add(value);
        }

        if (cpu.Count == 0 && memory.Count == 0) return Empty();

        return new PerfMonSummary(
            HasData: true,
            CpuAvgPercent: cpu.Count == 0 ? 0 : Math.Round(NormalizeCpu(cpu.Average()), 2),
            CpuMaxPercent: cpu.Count == 0 ? 0 : Math.Round(NormalizeCpu(cpu.Max()), 2),
            MemoryAvgMb: memory.Count == 0 ? 0 : Math.Round(memory.Average() / 1024 / 1024, 2),
            MemoryMaxMb: memory.Count == 0 ? 0 : Math.Round(memory.Max() / 1024 / 1024, 2));
    }

    /// <summary>PerfMon puede reportar CPU como fracción (0-1) o porcentaje (0-100); se normaliza a %.</summary>
    private static double NormalizeCpu(double value) => value <= 1.0 ? value * 100 : value;

    private static PerfMonSummary Empty() => new(false, 0, 0, 0, 0);
}
