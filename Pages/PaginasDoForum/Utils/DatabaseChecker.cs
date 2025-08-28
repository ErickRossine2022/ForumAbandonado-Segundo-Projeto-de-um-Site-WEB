using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System;

public class DatabaseChecker
{
    // ========================
    // INJEÇÃO DE DEPENDÊNCIA
    // ========================
    private readonly IConfiguration _configuration;

    public DatabaseChecker(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    // ==================================================
    // MÉTODO PRINCIPAL DE VERIFICAÇÃO E CRIAÇÃO AUTOMÁTICA
    // ==================================================
    public void VerificarEstruturaDoBanco()
    {
        var connectionString = _configuration.GetConnectionString("MySqlConnection");

        using var conn = new MySqlConnection(connectionString);
        conn.Open();

        // =============================
        // Verificar tabela "usuarios"
        // =============================
        var cmdUsuarios = new MySqlCommand("SHOW TABLES LIKE 'usuarios';", conn);
        var tabelaUsuariosExiste = cmdUsuarios.ExecuteScalar() != null;

        if (!tabelaUsuariosExiste)
        {
            var createUsuarios = new MySqlCommand(@"
                CREATE TABLE usuarios (
                    ID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    NAME VARCHAR(25),
                    SENHA VARCHAR(255),
                    EMAIL VARCHAR(255) UNIQUE,
                    ULTIMOLOGIN VARCHAR(255)
                );", conn);

            createUsuarios.ExecuteNonQuery();

            Console.WriteLine("====================================================================");
            Console.WriteLine("[ MySQL ] A tabela [usuarios] foi verificada e criada automaticamente!");
            Console.WriteLine("====================================================================");
        }
        else
        {
            Console.WriteLine("====================================================================");
            Console.WriteLine("[ MySQL ] A tabela [usuarios] já existe. Nenhuma ação foi necessária.");
            Console.WriteLine("====================================================================");
        }

        // ==========================================
        // Verificar tabela "usuarios_pendentes"
        // ==========================================
        var cmdPendentes = new MySqlCommand("SHOW TABLES LIKE 'usuarios_pendentes';", conn);
        var tabelaPendentesExiste = cmdPendentes.ExecuteScalar() != null;

        if (!tabelaPendentesExiste)
        {
            var createPendentes = new MySqlCommand(@"
                CREATE TABLE usuarios_pendentes (
                    ID INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    NAME VARCHAR(25),
                    EMAIL VARCHAR(255),
                    SENHA VARCHAR(255),
                    CODIGO_VERIFICACAO VARCHAR(10),
                    DATA_EXPIRACAO DATETIME
                );", conn);

            createPendentes.ExecuteNonQuery();

            Console.WriteLine("====================================================================");
            Console.WriteLine("[ MySQL ] A tabela [usuarios_pendentes] foi verificada e criada automaticamente!");
            Console.WriteLine("====================================================================");
        }
        else
        {
            Console.WriteLine("====================================================================");
            Console.WriteLine("[ MySQL ] A tabela [usuarios_pendentes] já existe. Nenhuma ação foi necessária.");
            Console.WriteLine("====================================================================");
        }

        Console.WriteLine("[OK] Verificação do banco concluída com sucesso.");
    }
}
