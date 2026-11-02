using System.Globalization;
using Jellyfin.MovieRequests.Models;
using Microsoft.Data.Sqlite;

namespace Jellyfin.MovieRequests.Repositories;

public class RequestRepository
{
    private readonly string _connectionString;

    public RequestRepository(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true
        }.ToString();

        InitializeDatabase();
    }

    public IReadOnlyList<MovieRequest> GetAll(Guid? currentUserId = null)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.Id, r.TMDbId, r.Title, r.Year, r.PosterUrl, r.RequestedBy, r.UserId, r.Username,
                   r.Created, COUNT(v.UserId) AS VoteCount, r.IsCompleted,
                   EXISTS (
                       SELECT 1 FROM Votes AS ownVote
                       WHERE ownVote.RequestId = r.Id AND ownVote.UserId = $currentUserId
                   ) AS VotedByCurrentUser
            FROM Requests AS r
            LEFT JOIN Votes AS v ON v.RequestId = r.Id
            GROUP BY r.Id
            ORDER BY VoteCount DESC, r.Created DESC;
            """;
        command.Parameters.AddWithValue(
            "$currentUserId",
            currentUserId.HasValue ? currentUserId.Value.ToString("D") : DBNull.Value);

        using var reader = command.ExecuteReader();
        var requests = new List<MovieRequest>();
        while (reader.Read())
        {
            requests.Add(new MovieRequest
            {
                Id = Guid.Parse(reader.GetString(0)),
                TMDbId = reader.GetInt32(1),
                Title = reader.GetString(2),
                Year = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                PosterUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
                RequestedBy = reader.GetString(5),
                UserId = reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
                Username = reader.IsDBNull(7) ? null : reader.GetString(7),
                Created = DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.None),
                Votes = reader.GetInt32(9),
                IsCompleted = reader.GetBoolean(10),
                VotedByCurrentUser = reader.GetBoolean(11)
            });
        }

        return requests;
    }

    public MovieRequest? GetById(Guid requestId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT r.Id, r.TMDbId, r.Title, r.Year, r.PosterUrl, r.RequestedBy, r.UserId, r.Username,
                   r.Created, COUNT(v.UserId) AS VoteCount, r.IsCompleted
            FROM Requests AS r
            LEFT JOIN Votes AS v ON v.RequestId = r.Id
            WHERE r.Id = $id
            GROUP BY r.Id;
            """;
        command.Parameters.AddWithValue("$id", requestId.ToString("D"));

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new MovieRequest
        {
            Id = Guid.Parse(reader.GetString(0)),
            TMDbId = reader.GetInt32(1),
            Title = reader.GetString(2),
            Year = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            PosterUrl = reader.IsDBNull(4) ? null : reader.GetString(4),
            RequestedBy = reader.GetString(5),
            UserId = reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
            Username = reader.IsDBNull(7) ? null : reader.GetString(7),
            Created = DateTimeOffset.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.None),
            Votes = reader.GetInt32(9),
            IsCompleted = reader.GetBoolean(10)
        };
    }

    public void Add(MovieRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Requests (Id, TMDbId, Title, Year, PosterUrl, RequestedBy, UserId, Username, Created)
            VALUES ($id, $tmDbId, $title, $year, $posterUrl, $requestedBy, $userId, $username, $created);
            """;
        command.Parameters.AddWithValue("$id", request.Id.ToString("D"));
        command.Parameters.AddWithValue("$tmDbId", request.TMDbId);
        command.Parameters.AddWithValue("$title", request.Title);
        command.Parameters.AddWithValue("$year", request.Year.HasValue ? request.Year.Value : DBNull.Value);
        command.Parameters.AddWithValue("$posterUrl", request.PosterUrl is null ? DBNull.Value : request.PosterUrl);
        command.Parameters.AddWithValue("$requestedBy", request.RequestedBy);
        command.Parameters.AddWithValue("$userId", request.UserId.HasValue ? request.UserId.Value.ToString("D") : DBNull.Value);
        command.Parameters.AddWithValue("$username", request.Username is null ? DBNull.Value : request.Username);
        command.Parameters.AddWithValue("$created", request.Created.ToString("O", CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    public bool HasRequestForTmDbId(int tmDbId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM Requests WHERE TMDbId = $tmDbId AND TMDbId > 0);";
        command.Parameters.AddWithValue("$tmDbId", tmDbId);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1;
    }

    public bool AddVote(Guid requestId, Guid userId, string username)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO Votes (RequestId, UserId, Username)
            VALUES ($requestId, $userId, $username);
            """;
        command.Parameters.AddWithValue("$requestId", requestId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        command.Parameters.AddWithValue("$username", username);
        return command.ExecuteNonQuery() == 1;
    }

    public bool RemoveVote(Guid requestId, Guid userId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM Votes
            WHERE RequestId = $requestId AND UserId = $userId;
            """;
        command.Parameters.AddWithValue("$requestId", requestId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        return command.ExecuteNonQuery() == 1;
    }

    public bool SetCompleted(Guid requestId, bool isCompleted)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Requests SET IsCompleted = $isCompleted WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", requestId.ToString("D"));
        command.Parameters.AddWithValue("$isCompleted", isCompleted ? 1 : 0);
        return command.ExecuteNonQuery() == 1;
    }

    public bool Delete(Guid requestId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Requests WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", requestId.ToString("D"));
        return command.ExecuteNonQuery() == 1;
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void InitializeDatabase()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Requests (
                Id TEXT NOT NULL PRIMARY KEY,
                TMDbId INTEGER NOT NULL DEFAULT 0,
                Title TEXT NOT NULL,
                Year INTEGER NULL,
                PosterUrl TEXT NULL,
                RequestedBy TEXT NOT NULL,
                UserId TEXT NULL,
                Username TEXT NULL,
                Created TEXT NOT NULL,
                IsCompleted INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS Votes (
                RequestId TEXT NOT NULL,
                UserId TEXT NOT NULL,
                Username TEXT NULL,
                Created TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (RequestId, UserId),
                FOREIGN KEY (RequestId) REFERENCES Requests (Id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_Votes_RequestId ON Votes (RequestId);
            """;
        command.ExecuteNonQuery();

        EnsureColumnExists(connection, "Requests", "UserId", "TEXT NULL");
        EnsureColumnExists(connection, "Requests", "Username", "TEXT NULL");
        EnsureColumnExists(connection, "Requests", "TMDbId", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, "Requests", "PosterUrl", "TEXT NULL");
        EnsureColumnExists(connection, "Requests", "IsCompleted", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumnExists(connection, "Votes", "Username", "TEXT NULL");
    }

    private static void EnsureColumnExists(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition)
    {
        using var checkCommand = connection.CreateCommand();
        checkCommand.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = checkCommand.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        reader.Close();
        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        alterCommand.ExecuteNonQuery();
    }
}
