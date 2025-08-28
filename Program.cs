using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// REGISTRANDO serviços
builder.Services.AddRazorPages();
builder.Services.AddTransient<DatabaseChecker>();

// Adicionando autenticação por cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/PaginasDoForum/LoginSystem";     // página de login
        options.LogoutPath = "/Index";   // página de logout [ MANDA DIRETO PRA A PAGINA PADRAO QUANDO ENTRA SITE AE LA TA O]
        options.AccessDeniedPath = "/AcessoNegado"; // opcional
    });

builder.Services.AddAuthorization();

// CONFIGURANDO sessão com opções
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// ATIVANDO sessão
app.UseSession();

// SISTEMA DE ERRO
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// VERIFICANDO estrutura do banco
using (var scope = app.Services.CreateAsyncScope())
{
    var checker = scope.ServiceProvider.GetRequiredService<DatabaseChecker>();
    checker.VerificarEstruturaDoBanco();
}

// MIDDLEWARES
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Language"] = "en";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 🔥 Ordem certa para login funcionar
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.Run();
