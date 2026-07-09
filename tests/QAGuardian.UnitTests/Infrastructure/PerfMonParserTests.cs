using FluentAssertions;
using QAGuardian.Infrastructure.Runners;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

public class PerfMonParserTests
{
    [Fact]
    public void Parsea_cpu_en_porcentaje_y_memoria_en_mb()
    {
        var lines = new[]
        {
            "timeStamp,elapsed,label",
            "1700000000000,40,localhost CPU",
            "1700000001000,60,localhost CPU",
            "1700000000000,104857600,localhost Memory",   // 100 MB
            "1700000001000,209715200,localhost Memory"    // 200 MB
        };

        var summary = PerfMonParser.Parse(lines);

        summary.HasData.Should().BeTrue();
        summary.CpuAvgPercent.Should().Be(50);
        summary.CpuMaxPercent.Should().Be(60);
        summary.MemoryAvgMb.Should().Be(150);
        summary.MemoryMaxMb.Should().Be(200);
    }

    [Fact]
    public void Normaliza_cpu_reportada_como_fraccion()
    {
        var lines = new[]
        {
            "timeStamp,elapsed,label",
            "1700000000000,0.25,host cpu",
            "1700000001000,0.75,host cpu"
        };

        var summary = PerfMonParser.Parse(lines);

        summary.CpuAvgPercent.Should().Be(50);
        summary.CpuMaxPercent.Should().Be(75);
    }

    [Fact]
    public void Sin_metricas_reconocibles_no_reporta_datos()
    {
        var lines = new[]
        {
            "timeStamp,elapsed,label",
            "1700000000000,123,algo-desconocido"
        };

        PerfMonParser.Parse(lines).HasData.Should().BeFalse();
    }

    [Fact]
    public void Archivo_vacio_no_reporta_datos()
    {
        PerfMonParser.Parse(Array.Empty<string>()).HasData.Should().BeFalse();
    }
}
