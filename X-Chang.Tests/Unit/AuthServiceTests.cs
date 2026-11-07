using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Moq;
using X_Chang.CORE.Core.DTOs.Auth;
using X_Chang.CORE.Core.Entities;
using X_Chang.CORE.Core.Interfaces;
using X_Chang.CORE.Core.Services;
using X_Chang.CORE.Core.Settings;

namespace X_Chang.Tests.Unit;

// Pruebas unitarias de AuthService: los repositorios y el correo son dobles (Moq),
// asi que solo se prueba la logica de registro e inicio de sesion.
[Trait("Category", "Unit")]
public class AuthServiceTests
{
    private readonly Mock<IAuthRepository> _authRepo = new();
    private readonly Mock<ISesionUsuarioRepository> _sesionRepo = new();
    private readonly Mock<IEmailService> _email = new();

    private AuthService CrearServicio() => new(
        _authRepo.Object,
        _sesionRepo.Object,
        Options.Create(new SessionSettings { ExpiresInDays = 7 }),
        Options.Create(new FrontendSettings { BaseUrl = "http://localhost:9200" }),
        _email.Object);

    private static string Sha256(string valor) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor))).ToLowerInvariant();

    private static Usuarios Usuario(string password, string rol = "USU", string estado = "Activo") => new()
    {
        UsuarioId = 10,
        NombreUsuario = "mattias",
        CorreoElectronico = "mattias@test.com",
        PasswordHash = Sha256(password),
        TemaVisual = "Oscuro",
        Estado = estado,
        Rol = new Roles { RolId = 1, Nombre = rol }
    };

    [Fact]
    public async Task Registrar_ConContrasenasDistintas_LanzaErrorYNoCreaUsuario()
    {
        var request = new RegisterRequestDto("mattias", "mattias@test.com", "Clave1234", "Otra12345", 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CrearServicio().RegistrarAsync(request));

        Assert.Equal("No coinciden", ex.Message);
        _authRepo.Verify(r => r.CrearUsuarioAsync(It.IsAny<Usuarios>()), Times.Never);
    }

    [Fact]
    public async Task Registrar_ConCorreoYaRegistrado_LanzaError()
    {
        _authRepo.Setup(r => r.ExisteCorreoAsync("mattias@test.com")).ReturnsAsync(true);
        var request = new RegisterRequestDto("mattias", "mattias@test.com", "Clave1234", "Clave1234", 1);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => CrearServicio().RegistrarAsync(request));

        Assert.Equal("Correo ya registrado", ex.Message);
        _authRepo.Verify(r => r.CrearUsuarioAsync(It.IsAny<Usuarios>()), Times.Never);
    }

    [Fact]
    public async Task Login_ConContrasenaIncorrecta_LanzaNoAutorizadoYRegistraAccesoFallido()
    {
        _authRepo.Setup(r => r.BuscarPorIdentificadorAsync("mattias")).ReturnsAsync(Usuario("Clave1234"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => CrearServicio().LoginAsync(new LoginRequestDto("mattias", "Incorrecta1")));

        _authRepo.Verify(r => r.RegistrarAccesoAsync(It.Is<AccesosUsuario>(a => !a.Exitoso)), Times.Once);
        _sesionRepo.Verify(r => r.CrearSesionAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task Login_Correcto_DevuelveTokenYCreaSesionNueva()
    {
        _authRepo.Setup(r => r.BuscarPorIdentificadorAsync("mattias@test.com")).ReturnsAsync(Usuario("Clave1234", rol: "ADM"));

        var respuesta = await CrearServicio().LoginAsync(new LoginRequestDto("mattias@test.com", "Clave1234"));

        Assert.False(string.IsNullOrWhiteSpace(respuesta.Token));
        Assert.Equal("Administrador", respuesta.Usuario.Rol);
        Assert.True(respuesta.Expira > DateTime.UtcNow.AddDays(6));
        _sesionRepo.Verify(r => r.CerrarSesionesActivasDeUsuarioAsync(10), Times.Once);
        _sesionRepo.Verify(r => r.CrearSesionAsync(10, respuesta.Token, respuesta.Expira), Times.Once);
    }
}
