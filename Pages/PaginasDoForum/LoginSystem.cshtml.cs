using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

public class LoginSystemModel : PageModel
{
    private readonly IConfiguration _configuration;

    public LoginSystemModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    public async Task<IActionResult> OnPost()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ModelState.AddModelError(string.Empty, "Fill in all fields.");
            return Page();
        }

        using var conn = new MySqlConnection(_configuration.GetConnectionString("MySqlConnection"));
        await conn.OpenAsync();

        // 🔍 Busca apenas senha e nome
        string sql = "SELECT SENHA, NAME FROM usuarios WHERE EMAIL = @Email";
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Email", Email);

        string? hashFromDb = null;
        string? nomeUsuario = null;

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                ModelState.AddModelError(string.Empty, "Email not found.");
                return Page();
            }

            hashFromDb = reader["SENHA"].ToString();
            nomeUsuario = reader["NAME"].ToString();
        }

        // 🔑 Valida senha
        string inputHash = GerarHash(Password);
        if (hashFromDb != inputHash)
        {
            ModelState.AddModelError(string.Empty, "Incorrect password.");
            return Page();
        }

        // 👤 Claims de autenticação (🔥 Foto fixa como padrão)
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, nomeUsuario!),
            new Claim("FotoPerfil", "~/imagens/FotoPadrao.png")
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true, // mantém logado
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties
        );

        // 🕒 Atualiza último login
        string sqlUpdate = "UPDATE usuarios SET ULTIMOLOGIN = NOW() WHERE EMAIL = @Email";
        using (var atualizarLogin = new MySqlCommand(sqlUpdate, conn))
        {
            atualizarLogin.Parameters.AddWithValue("@Email", Email);
            await atualizarLogin.ExecuteNonQueryAsync();
        }

        return RedirectToPage("/PaginasDoForum/BoasVindas");
    }

    private string GerarHash(string senha)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(senha);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

}
