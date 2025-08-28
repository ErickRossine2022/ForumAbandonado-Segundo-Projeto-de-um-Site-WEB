using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using System;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

public class RegisterSystemModel(IConfiguration configuration) : PageModel
{
    // ============================
    // DEPENDÊNCIA: ARQUIVO DE CONFIGURAÇÃO
    // ============================

    private readonly IConfiguration _configuration = configuration;

    // ============================
    // CAMPOS DO FORMULÁRIO (BIND)
    // ============================

    [BindProperty] public string? nome { get; set; }
    [BindProperty] public string? email { get; set; }
    [BindProperty] public string? password { get; set; }
    [BindProperty] public string? rptpassword { get; set; }
    [BindProperty] public string? Codigo { get; set; } // Código digitado pelo usuário

    // Indica se o código foi enviado (mantido entre requisições com TempData)
    [TempData] public bool CodigoFoiEnviado { get; set; }

    public void OnGet() { }

    public IActionResult OnPost()
    {
        // =====================================
        // ETAPA 1: ENVIAR CÓDIGO DE VERIFICAÇÃO
        // =====================================

        if (!CodigoFoiEnviado)
        {
            if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(rptpassword))
            {
                ModelState.AddModelError(string.Empty, "Fill in all fields!");
                return Page();
            }

            if (password != rptpassword)
            {
                ModelState.AddModelError(string.Empty, "Passwords do not match.");
                return Page();
            }

            using var conn = Conectar();
            conn.Open();

            var verificarEmail = new MySqlCommand("SELECT COUNT(*) FROM usuarios WHERE EMAIL = @email", conn);
            verificarEmail.Parameters.AddWithValue("@email", email);

            if (Convert.ToInt32(verificarEmail.ExecuteScalar()) > 0)
            {
                ModelState.AddModelError(string.Empty, "This email is already registered in our bank!");
                return Page();
            }

            var codigoAleatorio = new Random().Next(100000, 999999).ToString();
            string senhaCriptografada = GerarHash(password);

            var inserirPendentes = new MySqlCommand(@"
                INSERT INTO usuarios_pendentes (NAME, EMAIL, SENHA, CODIGO_VERIFICACAO, DATA_EXPIRACAO)
                VALUES (@name, @email, @senha, @codigo, @expira)", conn);

            inserirPendentes.Parameters.AddWithValue("@name", nome);
            inserirPendentes.Parameters.AddWithValue("@email", email);
            inserirPendentes.Parameters.AddWithValue("@senha", senhaCriptografada);
            inserirPendentes.Parameters.AddWithValue("@codigo", codigoAleatorio);
            inserirPendentes.Parameters.AddWithValue("@expira", DateTime.Now.AddMinutes(5));
            inserirPendentes.ExecuteNonQuery();

            EnviarCodigoEmail(email, codigoAleatorio);

            CodigoFoiEnviado = true;

            ModelState.AddModelError(string.Empty, "Verification code sent. Check your email.");
            return Page();
        }

        // ============================
        // ETAPA 2: VALIDAR O CÓDIGO
        // ============================

        if (string.IsNullOrWhiteSpace(Codigo))
        {
            ModelState.AddModelError(string.Empty, "Enter the verification code.");
            return Page();
        }

        using var conexao = Conectar();
        conexao.Open();

        var verificar = new MySqlCommand(@"
            SELECT * FROM usuarios_pendentes
            WHERE EMAIL = @email AND CODIGO_VERIFICACAO = @codigo AND DATA_EXPIRACAO > NOW()", conexao);

        verificar.Parameters.AddWithValue("@email", email);
        verificar.Parameters.AddWithValue("@codigo", Codigo);

        using var reader = verificar.ExecuteReader();
        if (!reader.Read())
        {
            ModelState.AddModelError(string.Empty, "Invalid or expired code.");
            return Page();
        }
        reader.Close();

        var inserirUser = new MySqlCommand("INSERT INTO usuarios (NAME, EMAIL, SENHA) VALUES (@name, @email, @senha)", conexao);
        inserirUser.Parameters.AddWithValue("@name", nome);
        inserirUser.Parameters.AddWithValue("@email", email);
        inserirUser.Parameters.AddWithValue("@senha", GerarHash(password));
        inserirUser.ExecuteNonQuery();

        var remover = new MySqlCommand("DELETE FROM usuarios_pendentes WHERE EMAIL = @email", conexao);
        remover.Parameters.AddWithValue("@email", email);
        remover.ExecuteNonQuery();

        return RedirectToPage("/PaginasDoForum/LoginSystem");
    }

    // =====================================
    // MÉTODO PARA CONEXÃO COM O BANCO MYSQL
    // =====================================

    public MySqlConnection Conectar()
    {
        string connectionString = _configuration.GetConnectionString("MySqlConnection")
            ?? throw new InvalidOperationException("MySQL connection string is missing.");
        return new MySqlConnection(connectionString);
    }

    // ==========================
    // GERAR HASH DA SENHA (SHA256)
    // ==========================

    private string GerarHash(string senha)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(senha);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    // ==========================
    // ENVIAR CÓDIGO VIA E-MAIL
    // ==========================

    private void EnviarCodigoEmail(string destino, string codigo)
    {
        var smtp = _configuration.GetSection("Smtp");

        string host = smtp["Host"] ?? throw new InvalidOperationException("SMTP host not configured.");
        int port = int.Parse(smtp["Port"] ?? "587");
        string user = smtp["User"] ?? throw new InvalidOperationException("SMTP user not configured.");
        string pass = smtp["Pass"] ?? throw new InvalidOperationException("SMTP password not configured.");
        bool enableSsl = bool.Parse(smtp["EnableSsl"] ?? "true");

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, pass),
            EnableSsl = enableSsl
        };

        var mail = new MailMessage
        {
            From = new MailAddress(user, smtp["From"] ?? "Site"),
            Subject = "Email Confirmation",
            IsBodyHtml = true,
            Body = $@"
                <h2>Hello!</h2>
                <p>Your confirmation code is:</p>
                <h1 style='color:blue'>{codigo}</h1>
                <p>Enter this code on the site to complete your registration.</p>
                <br>
                <img src='https://scontent.fcau23-1.fna.fbcdn.net/v/t51.82787-15/525114944_18053728367575597_5551359607193552235_n.jpg?_nc_cat=104&ccb=1-7&_nc_sid=127cfc&_nc_ohc=IKS-rUk2us8Q7kNvwHup83c&_nc_oc=AdlkO-27s46pvXUD8H3vDky63y8P-PQDW206IJ83vgk7Ij6dPzduBqX9AGMSo6mPcM0&_nc_zt=23&_nc_ht=scontent.fcau23-1.fna&_nc_gid=xeD9Ryp5-wtQ2lLwUvJ0rg&oh=00_AfTjKzoO5wcfBf0Y15-1Ul24Orbgus0ZQyroAs-xV2V_3A&oe=688D3DCD' alt='Site Logo' width='150'/>"
        };

        mail.To.Add(destino);

        try
        {
            client.Send(mail);
        }
        catch (SmtpException ex)
        {
            throw new ApplicationException("Erro ao enviar e-mail de confirmação: " + ex.Message);
        }
    }
}
