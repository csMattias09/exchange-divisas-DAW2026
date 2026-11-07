using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using X_Chang.API.Controllers;
using X_Chang.API.Helpers;
using X_Chang.CORE.Core.Entities;
using X_Chang.CORE.Core.Interfaces;
using X_Chang.CORE.Infrastructure.Data;

namespace X_Chang.Tests.Integration;

// Levanta la API real en memoria (TestServer) con toda su configuracion de Program.cs,
// pero reemplaza tres dependencias externas para que las pruebas no toquen servicios reales:
// - PostgreSQL de Supabase -> base de datos EF Core InMemory (una por fabrica)
// - Brevo (correo)        -> doble que no envia nada
// - Servicio en segundo plano de notificaciones -> desactivado
// Se usa MonedaController como tipo de referencia para ubicar el ensamblado de la API,
// asi no hace falta modificar Program.cs.
public class XChangApiFactory : WebApplicationFactory<MonedaController>
{
    public const string PasswordUsuarioSemilla = "ClaveSegura123";

    private readonly string _nombreBd = "xchang-tests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=no-usado-en-pruebas");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ExchangeDivisasDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ExchangeDivisasDbContext>>();
            services.AddDbContext<ExchangeDivisasDbContext>(options => options
                .UseInMemoryDatabase(_nombreBd)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            services.RemoveAll<IEmailService>();
            var email = new Mock<IEmailService>();
            email.Setup(e => e.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<AdjuntosCorreo>?>()))
                .ReturnsAsync(true);
            services.AddSingleton(email.Object);

            var notificaciones = services.Where(d => d.ImplementationType == typeof(NotificacionesBackgroundService)).ToList();
            foreach (var descriptor in notificaciones)
                services.Remove(descriptor);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExchangeDivisasDbContext>();
        Sembrar(db);

        return host;
    }

    private static void Sembrar(ExchangeDivisasDbContext db)
    {
        var usu = new Roles { RolId = 1, Nombre = "USU" };
        db.Roles.Add(usu);
        db.Monedas.AddRange(
            new Monedas { MonedaId = 1, CodigoIso = "USD", Nombre = "Dolar estadounidense", Tipo = "Fiat", Activa = true },
            new Monedas { MonedaId = 2, CodigoIso = "PEN", Nombre = "Sol peruano", Tipo = "Fiat", Activa = true },
            new Monedas { MonedaId = 3, CodigoIso = "VES", Nombre = "Bolivar", Tipo = "Fiat", Activa = false });
        db.Paises.Add(new Paises { PaisId = 1, Nombre = "Peru", MonedaId = 2 });
        db.Usuarios.Add(new Usuarios
        {
            UsuarioId = 1,
            RolId = 1,
            PaisId = 1,
            NombreUsuario = "angel",
            CorreoElectronico = "angel@test.com",
            PasswordHash = Sha256(PasswordUsuarioSemilla),
            TemaVisual = "Oscuro",
            Estado = "Activo",
            FechaRegistro = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    private static string Sha256(string valor) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor))).ToLowerInvariant();
}
