using Npgsql;

namespace RegistrationApi.Services;

public sealed class AccountRepository
{
    private readonly string _connectionString;

    public AccountRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<string?> FindByEmailAsync(string email)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            """SELECT "LoginName" FROM data."Account" WHERE "EMail" = @email LIMIT 1""", conn);
        cmd.Parameters.AddWithValue("email", email);

        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    public async Task<bool> ExistsAsync(string username)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            """SELECT 1 FROM data."Account" WHERE "LoginName" = @name LIMIT 1""", conn);
        cmd.Parameters.AddWithValue("name", username);

        return await cmd.ExecuteScalarAsync() != null;
    }

    public Task CreateAccountAsync(string username, string passwordHash)
        => CreateAccountAsync(username, passwordHash, "");

    public async Task CreateAccountAsync(string username, string passwordHash, string email)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var tx = await conn.BeginTransactionAsync();

        try
        {
            var vaultId = Guid.NewGuid();
            var accountId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;

            // Create vault storage
            await using (var vaultCmd = new NpgsqlCommand(
                """INSERT INTO data."ItemStorage" ("Id", "Money") VALUES (@id, 0)""", conn, tx))
            {
                vaultCmd.Parameters.AddWithValue("id", vaultId);
                await vaultCmd.ExecuteNonQueryAsync();
            }

            // Create account
            await using (var accCmd = new NpgsqlCommand(
                """
                INSERT INTO data."Account"
                    ("Id", "VaultId", "LoginName", "PasswordHash", "SecurityCode",
                     "EMail", "RegistrationDate", "State", "TimeZone",
                     "VaultPassword", "IsVaultExtended", "IsTemplate", "LanguageIsoCode")
                VALUES
                    (@id, @vaultId, @login, @hash, '',
                     @email, @regDate, 0, 0,
                     '', false, false, 'en')
                """, conn, tx))
            {
                accCmd.Parameters.AddWithValue("id", accountId);
                accCmd.Parameters.AddWithValue("vaultId", vaultId);
                accCmd.Parameters.AddWithValue("login", username);
                accCmd.Parameters.AddWithValue("hash", passwordHash);
                accCmd.Parameters.AddWithValue("email", email);
                accCmd.Parameters.AddWithValue("regDate", now);
                await accCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
