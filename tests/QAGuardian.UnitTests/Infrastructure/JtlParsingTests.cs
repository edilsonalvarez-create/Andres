using FluentAssertions;
using QAGuardian.Infrastructure.Runners;
using Xunit;

namespace QAGuardian.UnitTests.Infrastructure;

public class JtlParsingTests
{
    [Fact]
    public void Parsea_con_encabezado_y_extrae_usuarios_concurrentes()
    {
        var lines = new[]
        {
            "timeStamp,elapsed,label,responseCode,success,grpThreads,allThreads,URL",
            "1700000000000,120,Login,200,true,5,10,http://app/login",
            "1700000001000,150,Home,200,true,8,15,http://app/home",
            "1700000002000,300,Search,500,false,8,15,http://app/search"
        };

        var samples = JMeterTestRunner.ParseSamples(lines);

        samples.Should().HaveCount(3);
        samples.Max(s => s.AllThreads).Should().Be(15, "usuarios concurrentes = máximo de hilos activos");
        samples.Count(s => !s.Success).Should().Be(1);
    }

    [Fact]
    public void Parsea_columnas_en_orden_distinto_por_nombre()
    {
        // allThreads antes que elapsed: el parseo debe mapear por nombre, no por posición.
        var lines = new[]
        {
            "timeStamp,allThreads,elapsed,success",
            "1700000000000,25,120,true",
            "1700000001000,25,90,true"
        };

        var samples = JMeterTestRunner.ParseSamples(lines);

        samples.Should().HaveCount(2);
        samples.Max(s => s.AllThreads).Should().Be(25);
        samples[0].Elapsed.Should().Be(120);
    }

    [Fact]
    public void Sin_encabezado_usa_el_orden_por_defecto_de_jmeter()
    {
        // timeStamp, elapsed, label, code, message, threadName, dataType, success
        var lines = new[]
        {
            "1700000000000,120,Login,200,OK,tg 1-1,text,true",
            "1700000001000,90,Home,200,OK,tg 1-2,text,false"
        };

        var samples = JMeterTestRunner.ParseSamples(lines);

        samples.Should().HaveCount(2);
        samples[0].Elapsed.Should().Be(120);
        samples.Count(s => !s.Success).Should().Be(1);
        samples.Max(s => s.AllThreads).Should().Be(0, "sin columna allThreads no hay dato de concurrencia");
    }

    [Fact]
    public void Ignora_lineas_invalidas()
    {
        var lines = new[]
        {
            "timeStamp,elapsed,success,allThreads",
            "no-es-numero,120,true,5",
            "1700000000000,120,true,5",
            ""
        };

        var samples = JMeterTestRunner.ParseSamples(lines);

        samples.Should().HaveCount(1);
    }
}
