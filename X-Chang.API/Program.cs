using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using X_Chang.API.Authentication;
using X_Chang.API.Helpers;
using X_Chang.CORE.Core.Interfaces;
using X_Chang.CORE.Core.Services;
using X_Chang.CORE.Core.Settings;
using X_Chang.CORE.Infrastructure.Data;
using X_Chang.CORE.Infrastructure.Repositories;
using X_Chang.CORE.Infrastructure.Shared;
using X_Chang.CORE.Services;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Orígenes permitidos separados por coma (Cors__AllowedOrigins en producción).
// Sin configuración solo se permite el frontend local de Quasar.
var origenesPermitidos = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:9200")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("dev", policy =>
        policy.WithOrigins(origenesPermitidos)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<ExchangeDivisasDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<SessionSettings>(builder.Configuration.GetSection("SessionSettings"));
builder.Services.Configure<GroqSettings>(builder.Configuration.GetSection("Groq"));
builder.Services.Configure<FrontendSettings>(builder.Configuration.GetSection("FrontendSettings"));
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddAuthentication("Session")
    .AddScheme<AuthenticationSchemeOptions, SessionAuthHandler>("Session", null);

builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<ISesionUsuarioRepository, SesionUsuarioRepository>();
builder.Services.AddScoped<IBilleteraRepository, BilleteraRepository>();
builder.Services.AddScoped<IMonedaRepository, MonedaRepository>();
builder.Services.AddScoped<IPreciosParRepository, PreciosParRepository>();
builder.Services.AddScoped<IHistorialTransaccionesRepository, HistorialTransaccionesRepository>();
builder.Services.AddScoped<INotificacionesCorreoRepository, NotificacionesCorreoRepository>();

builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<IBilleteraService, BilleteraService>();
builder.Services.AddScoped<IMonedaService, MonedaService>();
builder.Services.AddScoped<IPreciosParService, PreciosParService>();
builder.Services.AddScoped<IOrdenService, OrdenService>();
builder.Services.AddScoped<IOfertaService, OfertaService>();
builder.Services.AddScoped<IHistorialTransaccionesService, HistorialTransaccionesService>();
builder.Services.AddScoped<IConfiguracionUsuarioService, ConfiguracionUsuarioService>();

builder.Services.Configure<BrevoSettings>(builder.Configuration.GetSection("BrevoSettings"));
builder.Services.AddHttpClient<IEmailService, EmailService>(client =>
{
    client.BaseAddress = new Uri("https://api.brevo.com/");
});
builder.Services.AddScoped<INotificacionesCorreoService, NotificacionesCorreoService>();
builder.Services.AddHostedService<NotificacionesBackgroundService>();

builder.Services.AddScoped<IAuditoriaAdministrativaRepository, AuditoriaAdministrativaRepository>();
builder.Services.AddScoped<IAuditoriaAdministrativaService, AuditoriaAdministrativaService>();
builder.Services.AddScoped<ICancelacionRepository, CancelacionRepository>();
builder.Services.AddScoped<ICancelacionService, CancelacionService>();
builder.Services.AddScoped<ICompraInmediataRepository, CompraInmediataRepository>();
builder.Services.AddScoped<ICompraInmediataService, CompraInmediataService>();
builder.Services.AddScoped<IDepositoRepository, DepositoRepository>();
builder.Services.AddScoped<IDepositoService, DepositoService>();
builder.Services.AddScoped<IGestionUsuariosAdminRepository, GestionUsuariosAdminRepository>();
builder.Services.AddHttpClient<IAdminMensajeIaService, GroqAdminMensajeIaService>();
builder.Services.AddScoped<IGestionUsuariosAdminService, GestionUsuariosAdminService>();
builder.Services.AddScoped<IVentaInmediataRepository, VentaInmediataRepository>();
builder.Services.AddScoped<IVentaInmediataService, VentaInmediataService>();
builder.Services.AddScoped<IRetiroRepository, RetiroRepository>();
builder.Services.AddScoped<IRetiroService, RetiroService>();
builder.Services.AddScoped<IMercadoRepository, MercadoRepository>();
builder.Services.AddScoped<IMercadoService, MercadoService>();
builder.Services.AddScoped<IPerfilService, PerfilService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
app.UseCors("dev");
app.UseHttpsRedirection();

// Health check para Railway y el balanceo de Nginx; no expone detalles del error.
app.MapGet("/health", async (ExchangeDivisasDbContext db) =>
    await db.Database.CanConnectAsync()
        ? Results.Ok(new { status = "ok" })
        : Results.Json(new { status = "error" }, statusCode: StatusCodes.Status503ServiceUnavailable));

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

