using Moq;
using X_Chang.CORE.Core.Entities;
using X_Chang.CORE.Core.Interfaces;
using X_Chang.CORE.Core.Services;

namespace X_Chang.Tests.Unit;

[Trait("Category", "Unit")]
public class MonedaServiceTests
{
    [Fact]
    public async Task GetMonedas_MapeaLasEntidadesDelRepositorioADto()
    {
        var repo = new Mock<IMonedaRepository>();
        repo.Setup(r => r.GetMonedasActivas()).ReturnsAsync(new[]
        {
            new Monedas { MonedaId = 1, CodigoIso = "PEN", Nombre = "Sol peruano", Tipo = "Fiat", Activa = true },
            new Monedas { MonedaId = 2, CodigoIso = "USD", Nombre = "Dolar estadounidense", Tipo = "Fiat", Activa = true }
        });

        var monedas = (await new MonedaService(repo.Object).GetMonedas()).ToList();

        Assert.Equal(2, monedas.Count);
        Assert.Equal("PEN", monedas[0].CodigoISO);
        Assert.Equal("Sol peruano", monedas[0].Nombre);
        Assert.Equal(2, monedas[1].MonedaId);
    }
}
