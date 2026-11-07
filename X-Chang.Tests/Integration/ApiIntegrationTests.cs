using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using X_Chang.CORE.Core.DTOs;
using X_Chang.CORE.Core.DTOs.Auth;

namespace X_Chang.Tests.Integration;

// Pruebas de integracion: peticiones HTTP reales contra la API completa
// (rutas, controladores, autenticacion por sesion, servicios, repositorios y EF Core).
[Trait("Category", "Integration")]
public class ApiIntegrationTests : IClassFixture<XChangApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public ApiIntegrationTests(XChangApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMonedas_DevuelveSoloLasActivasOrdenadasPorCodigo()
    {
        var respuesta = await _client.GetAsync("/api/moneda");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var monedas = await respuesta.Content.ReadFromJsonAsync<List<MonedaDTO>>(Json);
        Assert.Equal(new[] { "PEN", "USD" }, monedas!.Select(m => m.CodigoISO));
    }

    [Fact]
    public async Task Register_ConDatosValidos_CreaUsuarioYPermiteIniciarSesion()
    {
        var registro = new RegisterRequestDto("jefferson", "jefferson@test.com", "Clave12345", "Clave12345", 1);

        var respuestaRegistro = await _client.PostAsJsonAsync("/api/auth/register", registro);
        var respuestaLogin = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto("jefferson@test.com", "Clave12345"));

        Assert.Equal(HttpStatusCode.OK, respuestaRegistro.StatusCode);
        Assert.Equal(HttpStatusCode.OK, respuestaLogin.StatusCode);
        var auth = await respuestaLogin.Content.ReadFromJsonAsync<AuthResponseDto>(Json);
        Assert.Equal("jefferson", auth!.Usuario.NombreUsuario);
        Assert.Equal("Usuario", auth.Usuario.Rol);
    }

    [Fact]
    public async Task Login_ConContrasenaIncorrecta_Devuelve401()
    {
        var respuesta = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto("angel", "NoEsLaClave1"));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Me_SinToken_Devuelve401()
    {
        var respuesta = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Me_ConTokenDeLogin_DevuelveLosDatosDelUsuario()
    {
        var login = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequestDto("angel@test.com", XChangApiFactory.PasswordUsuarioSemilla));
        var auth = await login.Content.ReadFromJsonAsync<AuthResponseDto>(Json);

        using var peticion = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        var respuesta = await _client.SendAsync(peticion);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var me = await respuesta.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Equal("angel", me.GetProperty("nombreUsuario").GetString());
        Assert.Equal("angel@test.com", me.GetProperty("correoElectronico").GetString());
    }
}
