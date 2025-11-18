using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;

namespace Database;

public sealed class Database
{
    private static readonly Database _instance = new();
    private readonly string _connectionString;

    private Database()
    {
        _connectionString = "Data Source=database.db";

        using var connection = CreateConnection();
        InitializeDatabase(connection);
    }

    public static Database Instance => _instance;

    // Create a NEW open connection each time
    private SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void InitializeDatabase(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL UNIQUE,
                password TEXT NOT NULL,
                salt TEXT NOT NULL
            )";
        command.ExecuteNonQuery();
    }

    public void AddUser(string username, string password)
    {
        using var connection = CreateConnection();
        using var command = connection.CreateCommand();

        var salt = GenerateSalt();
        var hashedPassword = HashPassword(password, salt);

        command.CommandText = "INSERT INTO users (username, password, salt) VALUES ($username, $password, $salt)";
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$password", hashedPassword);
        command.Parameters.AddWithValue("$salt", salt);
        command.ExecuteNonQuery();
    }

    public bool VerifyPassword(string username, string password)
    {
        using var connection = CreateConnection();
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT password, salt FROM users WHERE username = $username";
        command.Parameters.AddWithValue("$username", username);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return false;

        var storedHash = reader.GetString(0);
        var salt = reader.GetString(1);
        var inputHash = HashPassword(password, salt);

        return storedHash == inputHash;
    }

    private string GenerateSalt()
    {
        var saltBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(saltBytes);
        return Convert.ToBase64String(saltBytes);
    }

    private string HashPassword(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var passwordBytes = Encoding.UTF8.GetBytes(password);

        using var pbkdf2 = new Rfc2898DeriveBytes(passwordBytes, saltBytes, 100000, HashAlgorithmName.SHA256);
        var hashBytes = pbkdf2.GetBytes(32);

        return Convert.ToBase64String(hashBytes);
    }

    public bool UserExists(string username)
    {
        using var connection = CreateConnection();
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM users WHERE username = $username";
        command.Parameters.AddWithValue("$username", username);
        var result = command.ExecuteScalar();
        return Convert.ToInt64(result) > 0;
    }

    public int GetUserCount()
    {
        using var connection = CreateConnection();
        using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM users";
        var result = command.ExecuteScalar();
        return Convert.ToInt32(result);
    }
}
