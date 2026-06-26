using KursMVVM.Models;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace KursMVVM.Services;

/// <summary>
/// Центральный сервис для работы с базой данных SQLite.
/// Автоматически создает БД со всеми таблицами и тестовыми данными,
/// если файл базы данных отсутствует в корневой папке проекта.
/// </summary>
public class DataBaseService
{
    private readonly string _connectionString;
    private readonly string _dbPath;

    public DataBaseService(string dbFileName = "kurs.db")
    {
        _dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dbFileName);
        _connectionString = $"Data Source={_dbPath};Foreign Keys=True;";

        if (!File.Exists(_dbPath))
        {
            CreateDatabase();
        }
        else
        {
            // Проверяем наличие новых таблиц и создаем если нужно
            EnsureNewTables();
        }
    }

    public string DatabasePath => _dbPath;

    private SqliteConnection CreateConnection() => new SqliteConnection(_connectionString);

    #region === СОЗДАНИЕ БАЗЫ ДАННЫХ ===

    public void CreateDatabase()
    {
        var directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = CreateConnection();
        connection.Open();

        using var transaction = connection.BeginTransaction();
        try
        {
            using (var cmd = new SqliteCommand("PRAGMA foreign_keys = ON;", connection, transaction))
                cmd.ExecuteNonQuery();

            CreateAllTables(connection, transaction);
            SeedAllData(connection, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void EnsureNewTables()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            // Проверяем наличие таблицы Пользователи
            using (var checkCmd = new SqliteCommand(
                "SELECT name FROM sqlite_master WHERE type='table' AND name='Пользователи';",
                connection, transaction))
            {
                var result = checkCmd.ExecuteScalar();
                if (result is null)
                {
                    // Создаем новые таблицы
                    using (var cmd = new SqliteCommand("PRAGMA foreign_keys = ON;", connection, transaction))
                        cmd.ExecuteNonQuery();

                    CreateUsersTable(connection, transaction);
                    CreateAuditTable(connection, transaction);
                    SeedUsers(connection, transaction);
                }
            }
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private void CreateAllTables(SqliteConnection conn, SqliteTransaction tr)
    {
        // Клиенты
        ExecuteNonQuery(conn, tr, @"
            CREATE TABLE IF NOT EXISTS Клиенты (
                номер_клиента   INTEGER PRIMARY KEY AUTOINCREMENT,
                фио             TEXT    NOT NULL,
                номер_паспорта  TEXT    NOT NULL UNIQUE
            );");
        ExecuteNonQuery(conn, tr, @"CREATE INDEX IF NOT EXISTS idx_клиенты_фио ON Клиенты(фио);");
        ExecuteNonQuery(conn, tr, @"CREATE INDEX IF NOT EXISTS idx_клиенты_паспорт ON Клиенты(номер_паспорта);");

        // Валюты
        ExecuteNonQuery(conn, tr, @"
            CREATE TABLE IF NOT EXISTS Валюты (
                код_валюты      TEXT PRIMARY KEY,
                название_валюты TEXT    NOT NULL,
                курс_продажи    REAL    NOT NULL,
                курс_покупки    REAL    NOT NULL,
                CONSTRAINT chk_курс_продажи_положительный CHECK (курс_продажи > 0),
                CONSTRAINT chk_курс_покупки_положительный CHECK (курс_покупки > 0)
            );");
        ExecuteNonQuery(conn, tr, @"CREATE INDEX IF NOT EXISTS idx_валюты_название ON Валюты(название_валюты);");

        // Кассиры
        ExecuteNonQuery(conn, tr, @"
            CREATE TABLE IF NOT EXISTS Кассиры (
                номер_кассира   INTEGER PRIMARY KEY AUTOINCREMENT,
                фио_кассира     TEXT    NOT NULL
            );");
        ExecuteNonQuery(conn, tr, @"CREATE INDEX IF NOT EXISTS idx_кассиры_фио ON Кассиры(фио_кассира);");

        // Сделки
        ExecuteNonQuery(conn, tr, @"
            CREATE TABLE IF NOT EXISTS Сделки (
                номер_сделки            INTEGER PRIMARY KEY AUTOINCREMENT,
                код_проданной_валюты    TEXT    NOT NULL,
                код_купленной_валюты    TEXT    NOT NULL,
                номер_кассира           INTEGER NOT NULL,
                номер_клиента           INTEGER NOT NULL,
                дата_сделки             DATE    NOT NULL,
                время_сделки            TIME    NOT NULL,
                сумма_проданной_валюты  REAL    NOT NULL,
                сумма_купленной_валюты  REAL    NOT NULL,
                CONSTRAINT fk_сделки_проданная_валюта
                    FOREIGN KEY (код_проданной_валюты) REFERENCES Валюты(код_валюты)
                    ON DELETE RESTRICT ON UPDATE CASCADE,
                CONSTRAINT fk_сделки_купленная_валюта
                    FOREIGN KEY (код_купленной_валюты) REFERENCES Валюты(код_валюты)
                    ON DELETE RESTRICT ON UPDATE CASCADE,
                CONSTRAINT fk_сделки_кассир
                    FOREIGN KEY (номер_кассира) REFERENCES Кассиры(номер_кассира)
                    ON DELETE RESTRICT ON UPDATE CASCADE,
                CONSTRAINT fk_сделки_клиент
                    FOREIGN KEY (номер_клиента) REFERENCES Клиенты(номер_клиента)
                    ON DELETE RESTRICT ON UPDATE CASCADE,
                CONSTRAINT chk_сумма_проданной_положительная CHECK (сумма_проданной_валюты > 0),
                CONSTRAINT chk_сумма_купленной_положительная CHECK (сумма_купленной_валюты > 0)
            );");
        ExecuteNonQuery(conn, tr, @"CREATE INDEX IF NOT EXISTS idx_сделки_дата ON Сделки(дата_сделки);");
        ExecuteNonQuery(conn, tr, @"CREATE INDEX IF NOT EXISTS idx_сделки_клиент ON Сделки(номер_клиента);");
        ExecuteNonQuery(conn, tr, @"CREATE INDEX IF NOT EXISTS idx_сделки_кассир ON Сделки(номер_кассира);");
        ExecuteNonQuery(conn, tr, @"CREATE INDEX IF NOT EXISTS idx_сделки_валюты ON Сделки(код_проданной_валюты, код_купленной_валюты);");

        // Пользователи
        CreateUsersTable(conn, tr);

        // Аудит
        CreateAuditTable(conn, tr);
    }

    private void CreateUsersTable(SqliteConnection conn, SqliteTransaction tr)
    {
        ExecuteNonQuery(conn, tr, @"
            CREATE TABLE IF NOT EXISTS Пользователи (
                id_пользователя INTEGER PRIMARY KEY AUTOINCREMENT,
                логин           TEXT    NOT NULL UNIQUE,
                пароль_хэш      TEXT    NOT NULL,
                роль            TEXT    NOT NULL DEFAULT 'Cashier' CHECK (роль IN ('Administrator', 'Cashier')),
                номер_кассира   INTEGER,
                FOREIGN KEY (номер_кассира) REFERENCES Кассиры(номер_кассира)
                    ON DELETE SET NULL ON UPDATE CASCADE
            );");
    }

    private void CreateAuditTable(SqliteConnection conn, SqliteTransaction tr)
    {
        ExecuteNonQuery(conn, tr, @"
            CREATE TABLE IF NOT EXISTS Аудит (
                id_записи       INTEGER PRIMARY KEY AUTOINCREMENT,
                дата_время      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                пользователь    TEXT    NOT NULL,
                действие        TEXT    NOT NULL,
                детали          TEXT
            );");
        ExecuteNonQuery(conn, tr, @"CREATE INDEX IF NOT EXISTS idx_аудит_дата ON Аудит(дата_время);");
    }

    private void SeedAllData(SqliteConnection conn, SqliteTransaction tr)
    {
        // Клиенты
        ExecuteNonQuery(conn, tr, @"
            INSERT INTO Клиенты (номер_клиента, фио, номер_паспорта) VALUES
            (1, 'Иванов Иван Иванович', '4501 123456'),
            (2, 'Петрова Анна Сергеевна', '4502 234567'),
            (3, 'Сидоров Виктор Петрович', '4503 345678'),
            (4, 'Козлова Елена Николаевна', '4504 456789'),
            (5, 'Михайлов Дмитрий Алексеевич', '4505 567890'),
            (6, 'Соколова Мария Игоревна', '4506 678901');");

        // Валюты
        ExecuteNonQuery(conn, tr, @"
            INSERT INTO Валюты (код_валюты, название_валюты, курс_продажи, курс_покупки) VALUES
            ('RUB', 'Российский рубль', 1.00, 1.00),
            ('USD', 'Доллар США', 95.50, 93.00),
            ('EUR', 'Евро', 105.20, 102.50),
            ('GBP', 'Фунт стерлингов', 125.00, 121.50),
            ('CNY', 'Китайский юань', 13.80, 13.20),
            ('CHF', 'Швейцарский франк', 108.00, 105.00);");

        // Кассиры
        ExecuteNonQuery(conn, tr, @"
            INSERT INTO Кассиры (номер_кассира, фио_кассира) VALUES
            (1, 'Кассирова Анна Ивановна'),
            (2, 'Обменников Петр Сергеевич'),
            (3, 'Валютова Елена Владимировна'),
            (4, 'Банкоматов Дмитрий Алексеевич');");

        // Сделки
        ExecuteNonQuery(conn, tr, @"
            INSERT INTO Сделки (
                код_проданной_валюты, код_купленной_валюты, номер_кассира, номер_клиента,
                дата_сделки, время_сделки, сумма_проданной_валюты, сумма_купленной_валюты
            ) VALUES
            ('USD', 'RUB', 1, 1, '2025-06-01', '10:15:00', 500, 47750),
            ('RUB', 'USD', 2, 2, '2025-06-01', '11:30:00', 100000, 1047),
            ('EUR', 'RUB', 1, 3, '2025-06-02', '09:45:00', 300, 31560),
            ('RUB', 'EUR', 3, 1, '2025-06-02', '14:20:00', 50000, 476),
            ('USD', 'RUB', 2, 4, '2025-06-03', '12:00:00', 200, 19100),
            ('GBP', 'RUB', 1, 5, '2025-06-03', '16:10:00', 100, 12500),
            ('RUB', 'USD', 3, 2, '2025-06-04', '10:30:00', 75000, 785),
            ('EUR', 'RUB', 4, 6, '2025-06-04', '13:45:00', 150, 15780),
            ('RUB', 'EUR', 2, 3, '2025-06-05', '11:15:00', 30000, 286),
            ('CHF', 'RUB', 1, 1, '2025-06-05', '15:00:00', 50, 5400),
            ('USD', 'RUB', 3, 5, '2025-06-06', '09:20:00', 350, 33425),
            ('RUB', 'GBP', 4, 4, '2025-06-06', '14:50:00', 20000, 160);");

        SeedUsers(conn, tr);
    }

    private void SeedUsers(SqliteConnection conn, SqliteTransaction tr)
    {
        // Администратор
        InsertUser(conn, tr, "admin", "admin", "Administrator", null);

        // Кассиры
        InsertUser(conn, tr, "cashier1", "cashier1", "Cashier", 1);
        InsertUser(conn, tr, "cashier2", "cashier2", "Cashier", 2);
        InsertUser(conn, tr, "cashier3", "cashier3", "Cashier", 3);
        InsertUser(conn, tr, "cashier4", "cashier4", "Cashier", 4);
    }

    private void InsertUser(SqliteConnection conn, SqliteTransaction tr,
        string login, string password, string role, int? cashierId)
    {
        using var cmd = new SqliteCommand(@"
        INSERT INTO Пользователи (логин, пароль_хэш, роль, номер_кассира)
        VALUES (@login, @hash, @role, @cashierId)", conn, tr);

        cmd.Parameters.AddWithValue("@login", login);
        cmd.Parameters.AddWithValue("@hash", BCrypt.Net.BCrypt.HashPassword(password));
        cmd.Parameters.AddWithValue("@role", role);
        cmd.Parameters.AddWithValue("@cashierId", (object?)cashierId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private void ExecuteNonQuery(SqliteConnection conn, SqliteTransaction tr, string sql)
    {
        using var cmd = new SqliteCommand(sql, conn, tr);
        cmd.ExecuteNonQuery();
    }

    #endregion

    #region === ПОЛЬЗОВАТЕЛИ ===

    public User? AuthenticateUser(string login, string password)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "SELECT id_пользователя, логин, пароль_хэш, роль, номер_кассира FROM Пользователи WHERE логин = @login",
            connection);
        cmd.Parameters.AddWithValue("@login", login);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var hash = reader.GetString(2);
        if (!BCrypt.Net.BCrypt.Verify(password, hash)) return null;

        return new User
        {
            UserId = reader.GetInt32(0),
            Login = reader.GetString(1),
            PasswordHash = hash,
            Role = Enum.Parse<UserRole>(reader.GetString(3)),
            CashierId = reader.IsDBNull(4) ? null : reader.GetInt32(4)
        };
    }

    public void RegisterUser(string login, string password, UserRole role, int? cashierId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "INSERT INTO Пользователи (логин, пароль_хэш, роль, номер_кассира) VALUES (@login, @hash, @role, @cashierId)",
            connection);
        cmd.Parameters.AddWithValue("@login", login);
        cmd.Parameters.AddWithValue("@hash", BCrypt.Net.BCrypt.HashPassword(password));
        cmd.Parameters.AddWithValue("@role", role.ToString());
        cmd.Parameters.AddWithValue("@cashierId", (object?)cashierId ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public List<User> GetAllUsers()
    {
        var users = new List<User>();
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "SELECT id_пользователя, логин, пароль_хэш, роль, номер_кассира FROM Пользователи ORDER BY id_пользователя",
            connection);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new User
            {
                UserId = reader.GetInt32(0),
                Login = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                Role = Enum.Parse<UserRole>(reader.GetString(3)),
                CashierId = reader.IsDBNull(4) ? null : reader.GetInt32(4)
            });
        }
        return users;
    }

    #endregion

    #region === АУДИТ ===

    public void LogAudit(string username, string action, string details)
    {
        try
        {
            using var connection = CreateConnection();
            connection.Open();
            using var cmd = new SqliteCommand(
                "INSERT INTO Аудит (пользователь, действие, детали) VALUES (@user, @action, @details)",
                connection);
            cmd.Parameters.AddWithValue("@user", username);
            cmd.Parameters.AddWithValue("@action", action);
            cmd.Parameters.AddWithValue("@details", details);
            cmd.ExecuteNonQuery();
        }
        catch { /* Аудит не должен ломать основную логику */ }
    }

    public List<AuditLog> GetAuditLogs(int limit = 1000)
    {
        var logs = new List<AuditLog>();
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "SELECT id_записи, дата_время, пользователь, действие, детали FROM Аудит ORDER BY дата_время DESC LIMIT @limit",
            connection);
        cmd.Parameters.AddWithValue("@limit", limit);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            logs.Add(new AuditLog
            {
                LogId = reader.GetInt32(0),
                Timestamp = reader.GetDateTime(1),
                Username = reader.GetString(2),
                Action = reader.GetString(3),
                Details = reader.IsDBNull(4) ? "" : reader.GetString(4)
            });
        }
        return logs;
    }

    #endregion

    #region === CRUD: КЛИЕНТЫ ===

    public List<Client> GetAllClients()
    {
        var clients = new List<Client>();
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand("SELECT * FROM Клиенты ORDER BY номер_клиента", connection);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            clients.Add(ReadClient(reader));
        }
        return clients;
    }

    public void AddClient(Client client)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "INSERT INTO Клиенты (фио, номер_паспорта) VALUES (@фио, @паспорт)", connection);
        cmd.Parameters.AddWithValue("@фио", client.FullName);
        cmd.Parameters.AddWithValue("@паспорт", client.PassportNumber);
        cmd.ExecuteNonQuery();
    }

    public void UpdateClient(Client client)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "UPDATE Клиенты SET фио = @фио, номер_паспорта = @паспорт WHERE номер_клиента = @id",
            connection);
        cmd.Parameters.AddWithValue("@id", client.ClientId);
        cmd.Parameters.AddWithValue("@фио", client.FullName);
        cmd.Parameters.AddWithValue("@паспорт", client.PassportNumber);
        cmd.ExecuteNonQuery();
    }

    public void DeleteClient(int clientId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "DELETE FROM Клиенты WHERE номер_клиента = @id", connection);
        cmd.Parameters.AddWithValue("@id", clientId);
        cmd.ExecuteNonQuery();
    }

    #endregion

    #region === CRUD: ВАЛЮТЫ ===

    public List<Currency> GetAllCurrencies()
    {
        var currencies = new List<Currency>();
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand("SELECT * FROM Валюты ORDER BY код_валюты", connection);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            currencies.Add(ReadCurrency(reader));
        }
        return currencies;
    }

    public void AddCurrency(Currency currency)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "INSERT INTO Валюты (код_валюты, название_валюты, курс_продажи, курс_покупки) VALUES (@код, @название, @продажа, @покупка)", connection);
        cmd.Parameters.AddWithValue("@код", currency.Code);
        cmd.Parameters.AddWithValue("@название", currency.Name);
        cmd.Parameters.AddWithValue("@продажа", currency.SellRate);
        cmd.Parameters.AddWithValue("@покупка", currency.BuyRate);
        cmd.ExecuteNonQuery();
    }

    public void UpdateCurrency(Currency currency)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "UPDATE Валюты SET название_валюты = @название, курс_продажи = @продажа, курс_покупки = @покупка WHERE код_валюты = @код",
            connection);
        cmd.Parameters.AddWithValue("@код", currency.Code);
        cmd.Parameters.AddWithValue("@название", currency.Name);
        cmd.Parameters.AddWithValue("@продажа", currency.SellRate);
        cmd.Parameters.AddWithValue("@покупка", currency.BuyRate);
        cmd.ExecuteNonQuery();
    }
    public bool IsCurrencyUsedInDeals(string code)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "SELECT COUNT(*) FROM Сделки WHERE код_проданной_валюты = @код OR код_купленной_валюты = @код",
            connection);
        cmd.Parameters.AddWithValue("@код", code);
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        return count > 0;
    }

    public void DeleteCurrency(string code)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "DELETE FROM Валюты WHERE код_валюты = @код", connection);
        cmd.Parameters.AddWithValue("@код", code);
        cmd.ExecuteNonQuery();
    }

    public async Task UpdateRatesFromCbrAsync()
    {
        using var client = new HttpClient();

        var bytes = await client.GetByteArrayAsync("https://www.cbr.ru/scripts/XML_daily.asp");
        var xml = System.Text.Encoding.GetEncoding("windows-1251").GetString(bytes);

        var doc = System.Xml.Linq.XDocument.Parse(xml);
        var rates = doc.Descendants("Valute")
            .Select(v => new
            {
                Code = v.Element("CharCode")?.Value,
                RateStr = v.Element("Value")?.Value ?? "0",
                Nominal = v.Element("Nominal")?.Value ?? "1"
            })
            .Where(r => !string.IsNullOrEmpty(r.Code))
            .Select(r => new
            {
                r.Code,
                Rate = double.Parse(r.RateStr.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture)
                       / double.Parse(r.Nominal.Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture)
            })
            .ToList();

        using var connection = CreateConnection();
        connection.Open();

        int updatedCount = 0;
        foreach (var rate in rates)
        {
            using var checkCmd = new SqliteCommand(
                "SELECT код_валюты FROM Валюты WHERE код_валюты = @код", connection);
            checkCmd.Parameters.AddWithValue("@код", rate.Code);
            var exists = checkCmd.ExecuteScalar() != null;

            if (exists)
            {
                var sellRate = rate.Rate * 1.02;
                var buyRate = rate.Rate * 0.98;

                using var updateCmd = new SqliteCommand(
                    "UPDATE Валюты SET курс_продажи = @продажа, курс_покупки = @покупка WHERE код_валюты = @код",
                    connection);
                updateCmd.Parameters.AddWithValue("@код", rate.Code);
                updateCmd.Parameters.AddWithValue("@продажа", sellRate);
                updateCmd.Parameters.AddWithValue("@покупка", buyRate);
                updateCmd.ExecuteNonQuery();
                updatedCount++;
            }
        }

        System.Diagnostics.Debug.WriteLine($"Updated {updatedCount} currencies from CBR");
    }

    #endregion

    #region === CRUD: КАССИРЫ ===

    public List<Cashier> GetAllCashiers()
    {
        var cashiers = new List<Cashier>();
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand("SELECT * FROM Кассиры ORDER BY номер_кассира", connection);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            cashiers.Add(ReadCashier(reader));
        }
        return cashiers;
    }

    public void AddCashier(Cashier cashier)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "INSERT INTO Кассиры (фио_кассира) VALUES (@фио)", connection);
        cmd.Parameters.AddWithValue("@фио", cashier.FullName);
        cmd.ExecuteNonQuery();
    }

    public void UpdateCashier(Cashier cashier)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "UPDATE Кассиры SET фио_кассира = @фио WHERE номер_кассира = @id", connection);
        cmd.Parameters.AddWithValue("@id", cashier.CashierId);
        cmd.Parameters.AddWithValue("@фио", cashier.FullName);
        cmd.ExecuteNonQuery();
    }

    public void DeleteCashier(int cashierId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "DELETE FROM Кассиры WHERE номер_кассира = @id", connection);
        cmd.Parameters.AddWithValue("@id", cashierId);
        cmd.ExecuteNonQuery();
    }

    public Cashier? GetCashierById(int id)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "SELECT * FROM Кассиры WHERE номер_кассира = @id", connection);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (reader.Read()) return ReadCashier(reader);
        return null;
    }

    #endregion

    #region === CRUD: СДЕЛКИ ===

    public List<Deal> GetAllDeals()
    {
        var deals = new List<Deal>();
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT 
                с.номер_сделки,
                с.код_проданной_валюты,
                vp.название_валюты AS название_проданной,
                с.код_купленной_валюты,
                vk.название_валюты AS название_купленной,
                с.номер_кассира,
                кс.фио_кассира,
                с.номер_клиента,
                кл.фио AS фио_клиента,
                с.дата_сделки,
                с.время_сделки,
                с.сумма_проданной_валюты,
                с.сумма_купленной_валюты
            FROM Сделки с
            JOIN Валюты vp ON с.код_проданной_валюты = vp.код_валюты
            JOIN Валюты vk ON с.код_купленной_валюты = vk.код_валюты
            JOIN Кассиры кс ON с.номер_кассира = кс.номер_кассира
            JOIN Клиенты кл ON с.номер_клиента = кл.номер_клиента
            ORDER BY с.дата_сделки DESC, с.время_сделки DESC", connection);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            deals.Add(ReadDeal(reader));
        }
        return deals;
    }

    public List<Deal> GetDealsByCashier(int cashierId)
    {
        var deals = new List<Deal>();
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT 
                с.номер_сделки,
                с.код_проданной_валюты,
                vp.название_валюты AS название_проданной,
                с.код_купленной_валюты,
                vk.название_валюты AS название_купленной,
                с.номер_кассира,
                кс.фио_кассира,
                с.номер_клиента,
                кл.фио AS фио_клиента,
                с.дата_сделки,
                с.время_сделки,
                с.сумма_проданной_валюты,
                с.сумма_купленной_валюты
            FROM Сделки с
            JOIN Валюты vp ON с.код_проданной_валюты = vp.код_валюты
            JOIN Валюты vk ON с.код_купленной_валюты = vk.код_валюты
            JOIN Кассиры кс ON с.номер_кассира = кс.номер_кассира
            JOIN Клиенты кл ON с.номер_клиента = кл.номер_клиента
            WHERE с.номер_кассира = @cashierId
            ORDER BY с.дата_сделки DESC, с.время_сделки DESC", connection);
        cmd.Parameters.AddWithValue("@cashierId", cashierId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            deals.Add(ReadDeal(reader));
        }
        return deals;
    }

    public void AddDeal(Deal deal)
    {
        if (deal.DealDate > DateTime.Now.Date)
            throw new ArgumentException("Дата сделки не может быть в будущем");

        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(@"
            INSERT INTO Сделки (
                код_проданной_валюты, код_купленной_валюты, номер_кассира, номер_клиента,
                дата_сделки, время_сделки, сумма_проданной_валюты, сумма_купленной_валюты
            ) VALUES (
                @проданная, @купленная, @кассир, @клиент,
                @дата, @время, @сумма_прод, @сумма_куп
            )", connection);

        cmd.Parameters.AddWithValue("@проданная", deal.SoldCurrencyCode);
        cmd.Parameters.AddWithValue("@купленная", deal.BoughtCurrencyCode);
        cmd.Parameters.AddWithValue("@кассир", deal.CashierId);
        cmd.Parameters.AddWithValue("@клиент", deal.ClientId);
        cmd.Parameters.AddWithValue("@дата", deal.DealDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@время", deal.DealTime.ToString());
        cmd.Parameters.AddWithValue("@сумма_прод", deal.SoldAmount);
        cmd.Parameters.AddWithValue("@сумма_куп", deal.BoughtAmount);
        cmd.ExecuteNonQuery();
    }

    public void DeleteDeal(int dealId)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(
            "DELETE FROM Сделки WHERE номер_сделки = @id", connection);
        cmd.Parameters.AddWithValue("@id", dealId);
        cmd.ExecuteNonQuery();
    }

    #endregion

    #region === ОТЧЁТЫ ===

    public DataTable GetClientReport()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT 
                кл.фио AS 'Ф.И.О. клиента',
                кл.номер_паспорта AS 'Номер паспорта',
                COUNT(с.номер_сделки) AS 'Кол-во сделок',
                COALESCE(SUM(CASE WHEN с.код_проданной_валюты = 'RUB' THEN с.сумма_проданной_валюты ELSE с.сумма_проданной_валюты * vp.курс_продажи END), 0) AS 'Общая сумма проданной (руб.)',
                COALESCE(SUM(CASE WHEN с.код_купленной_валюты = 'RUB' THEN с.сумма_купленной_валюты ELSE с.сумма_купленной_валюты * vk.курс_покупки END), 0) AS 'Общая сумма купленной (руб.)'
            FROM Клиенты кл
            LEFT JOIN Сделки с ON кл.номер_клиента = с.номер_клиента
            LEFT JOIN Валюты vp ON с.код_проданной_валюты = vp.код_валюты
            LEFT JOIN Валюты vk ON с.код_купленной_валюты = vk.код_валюты
            GROUP BY кл.номер_клиента, кл.фио, кл.номер_паспорта
            ORDER BY кл.фио", connection);

        var dt = new DataTable();
        dt.Load(cmd.ExecuteReader());
        return dt;
    }

    public DataTable GetCurrencyReport()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT 
                в.название_валюты AS 'Название валюты',
                в.курс_покупки AS 'Курс покупки',
                в.курс_продажи AS 'Курс продажи',
                COUNT(DISTINCT CASE WHEN с.код_купленной_валюты = в.код_валюты THEN с.номер_сделки END) AS 'Сделок покупки',
                COUNT(DISTINCT CASE WHEN с.код_проданной_валюты = в.код_валюты THEN с.номер_сделки END) AS 'Сделок продажи',
                COALESCE(SUM(CASE WHEN с.код_купленной_валюты = в.код_валюты AND с.код_купленной_валюты = 'RUB' THEN с.сумма_купленной_валюты WHEN с.код_купленной_валюты = в.код_валюты THEN с.сумма_купленной_валюты * в.курс_покупки WHEN с.код_проданной_валюты = в.код_валюты AND с.код_проданной_валюты = 'RUB' THEN с.сумма_проданной_валюты WHEN с.код_проданной_валюты = в.код_валюты THEN с.сумма_проданной_валюты * в.курс_продажи ELSE 0 END), 0) AS 'Общий объем (руб.)'
            FROM Валюты в
            LEFT JOIN Сделки с ON (с.код_проданной_валюты = в.код_валюты OR с.код_купленной_валюты = в.код_валюты)
            GROUP BY в.код_валюты, в.название_валюты, в.курс_покупки, в.курс_продажи
            ORDER BY в.код_валюты", connection);

        var dt = new DataTable();
        dt.Load(cmd.ExecuteReader());
        return dt;
    }

    public DataTable GetCashierReport()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT 
                кс.фио_кассира AS 'Ф.И.О. кассира',
                COUNT(с.номер_сделки) AS 'Кол-во сделок',
                COALESCE(SUM(CASE WHEN с.код_проданной_валюты = 'RUB' THEN с.сумма_проданной_валюты ELSE с.сумма_проданной_валюты * vp.курс_продажи END + CASE WHEN с.код_купленной_валюты = 'RUB' THEN с.сумма_купленной_валюты ELSE с.сумма_купленной_валюты * vk.курс_покупки END), 0) AS 'Общая сумма операций (руб.)'
            FROM Кассиры кс
            LEFT JOIN Сделки с ON кс.номер_кассира = с.номер_кассира
            LEFT JOIN Валюты vp ON с.код_проданной_валюты = vp.код_валюты
            LEFT JOIN Валюты vk ON с.код_купленной_валюты = vk.код_валюты
            GROUP BY кс.номер_кассира, кс.фио_кассира
            ORDER BY кс.фио_кассира", connection);

        var dt = new DataTable();
        dt.Load(cmd.ExecuteReader());
        return dt;
    }

    public DataTable GetInactiveClientsReport()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT 
                номер_клиента AS '№ клиента',
                фио AS 'Ф.И.О.',
                номер_паспорта AS 'Номер паспорта'
            FROM Клиенты
            WHERE номер_клиента NOT IN (SELECT DISTINCT номер_клиента FROM Сделки)
            ORDER BY фио", connection);

        var dt = new DataTable();
        dt.Load(cmd.ExecuteReader());
        return dt;
    }

    public DataTable GetProfitReport(DateTime fromDate, DateTime toDate)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT 
                с.дата_сделки AS 'Дата',
                COUNT(*) AS 'Кол-во сделок',
                SUM(CASE WHEN с.код_проданной_валюты = 'RUB' THEN с.сумма_проданной_валюты ELSE с.сумма_проданной_валюты * vp.курс_продажи END) AS 'Выручка от продаж (руб.)',
                SUM(CASE WHEN с.код_купленной_валюты = 'RUB' THEN с.сумма_купленной_валюты ELSE с.сумма_купленной_валюты * vk.курс_покупки END) AS 'Затраты на покупку (руб.)',
                SUM(CASE WHEN с.код_проданной_валюты = 'RUB' THEN с.сумма_проданной_валюты ELSE с.сумма_проданной_валюты * vp.курс_продажи END) - SUM(CASE WHEN с.код_купленной_валюты = 'RUB' THEN с.сумма_купленной_валюты ELSE с.сумма_купленной_валюты * vk.курс_покупки END) AS 'Прибыль (руб.)'
            FROM Сделки с
            JOIN Валюты vp ON с.код_проданной_валюты = vp.код_валюты
            JOIN Валюты vk ON с.код_купленной_валюты = vk.код_валюты
            WHERE с.дата_сделки BETWEEN @from AND @to
            GROUP BY с.дата_сделки
            ORDER BY с.дата_сделки", connection);

        cmd.Parameters.AddWithValue("@from", fromDate.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@to", toDate.ToString("yyyy-MM-dd"));

        var dt = new DataTable();
        dt.Load(cmd.ExecuteReader());
        return dt;
    }

    #endregion

    #region === ГРАФИКИ ===

    public List<(string Currency, double Volume)> GetCurrencyVolumeData()
    {
        var result = new List<(string, double)>();
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT 
                в.название_валюты,
                COALESCE(SUM(CASE WHEN с.код_проданной_валюты = 'RUB' THEN с.сумма_проданной_валюты ELSE с.сумма_проданной_валюты * vp.курс_продажи END + CASE WHEN с.код_купленной_валюты = 'RUB' THEN с.сумма_купленной_валюты ELSE с.сумма_купленной_валюты * vk.курс_покупки END), 0) AS объем
            FROM Валюты в
            LEFT JOIN Сделки с ON (с.код_проданной_валюты = в.код_валюты OR с.код_купленной_валюты = в.код_валюты)
            LEFT JOIN Валюты vp ON с.код_проданной_валюты = vp.код_валюты
            LEFT JOIN Валюты vk ON с.код_купленной_валюты = vk.код_валюты
            GROUP BY в.код_валюты, в.название_валюты
            HAVING объем > 0
            ORDER BY объем DESC", connection);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetString(0), reader.GetDouble(1)));
        }
        return result;
    }

    public List<(string Cashier, int DealCount)> GetCashierDealData()
    {
        var result = new List<(string, int)>();
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT кс.фио_кассира, COUNT(с.номер_сделки) AS количество
            FROM Кассиры кс
            LEFT JOIN Сделки с ON кс.номер_кассира = с.номер_кассира
            GROUP BY кс.номер_кассира, кс.фио_кассира
            ORDER BY количество DESC", connection);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetString(0), reader.GetInt32(1)));
        }
        return result;
    }

    public List<(DateTime Date, double Volume)> GetDailyVolumeData()
    {
        var result = new List<(DateTime, double)>();
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT с.дата_сделки, SUM(CASE WHEN с.код_проданной_валюты = 'RUB' THEN с.сумма_проданной_валюты ELSE с.сумма_проданной_валюты * vp.курс_продажи END + CASE WHEN с.код_купленной_валюты = 'RUB' THEN с.сумма_купленной_валюты ELSE с.сумма_купленной_валюты * vk.курс_покупки END) AS объем
            FROM Сделки с
            JOIN Валюты vp ON с.код_проданной_валюты = vp.код_валюты
            JOIN Валюты vk ON с.код_купленной_валюты = vk.код_валюты
            GROUP BY с.дата_сделки
            ORDER BY с.дата_сделки", connection);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((DateTime.Parse(reader.GetString(0)), reader.GetDouble(1)));
        }
        return result;
    }

    public List<(string Range, int Count)> GetAmountDistributionData()
    {
        var result = new List<(string, int)>();
        using var connection = CreateConnection();
        connection.Open();
        using var cmd = new SqliteCommand(@"
            SELECT 
                CASE WHEN сумма_проданной_валюты < 1000 THEN '0 - 1 000'
                     WHEN сумма_проданной_валюты < 10000 THEN '1 000 - 10 000'
                     WHEN сумма_проданной_валюты < 50000 THEN '10 000 - 50 000'
                     WHEN сумма_проданной_валюты < 100000 THEN '50 000 - 100 000'
                     ELSE '100 000+'
                END AS диапазон,
                COUNT(*) AS количество
            FROM Сделки
            GROUP BY диапазон
            ORDER BY MIN(сумма_проданной_валюты)", connection);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.GetString(0), reader.GetInt32(1)));
        }
        return result;
    }

    #endregion

    #region === ЧТЕНИЕ МОДЕЛЕЙ ===

    private Client ReadClient(SqliteDataReader reader) => new Client
    {
        ClientId = reader.GetInt32(0),
        FullName = reader.GetString(1),
        PassportNumber = reader.GetString(2)
    };

    private Currency ReadCurrency(SqliteDataReader reader) => new Currency
    {
        Code = reader.GetString(0),
        Name = reader.GetString(1),
        SellRate = reader.GetDouble(2),
        BuyRate = reader.GetDouble(3)
    };

    private Cashier ReadCashier(SqliteDataReader reader) => new Cashier
    {
        CashierId = reader.GetInt32(0),
        FullName = reader.GetString(1)
    };

    private Deal ReadDeal(SqliteDataReader reader) => new Deal
    {
        DealId = reader.GetInt32(0),
        SoldCurrencyCode = reader.GetString(1),
        SoldCurrencyName = reader.IsDBNull(2) ? null : reader.GetString(2),
        BoughtCurrencyCode = reader.GetString(3),
        BoughtCurrencyName = reader.IsDBNull(4) ? null : reader.GetString(4),
        CashierId = reader.GetInt32(5),
        CashierName = reader.IsDBNull(6) ? null : reader.GetString(6),
        ClientId = reader.GetInt32(7),
        ClientName = reader.IsDBNull(8) ? null : reader.GetString(8),
        DealDate = DateTime.Parse(reader.GetString(9)),
        DealTime = TimeSpan.Parse(reader.GetString(10)),
        SoldAmount = reader.GetDouble(11),
        BoughtAmount = reader.GetDouble(12)
    };

    #endregion

    #region === ИМПОРТ / ЭКСПОРТ ===

    public void ClearAllData()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            using (var cmd = new SqliteCommand("PRAGMA foreign_keys = OFF;", connection, transaction))
                cmd.ExecuteNonQuery();

            ExecuteNonQuery(connection, transaction, "DELETE FROM Сделки;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM Клиенты;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM Валюты;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM Кассиры;");
            ExecuteNonQuery(connection, transaction, "DELETE FROM sqlite_sequence WHERE name IN ('Клиенты', 'Кассиры', 'Сделки');");

            using (var cmd = new SqliteCommand("PRAGMA foreign_keys = ON;", connection, transaction))
                cmd.ExecuteNonQuery();

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public void BulkImport(List<Client> clients, List<Currency> currencies,
                          List<Cashier> cashiers, List<Deal> deals)
    {
        using var connection = CreateConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var c in clients)
            {
                using var cmd = new SqliteCommand(
                    "INSERT OR REPLACE INTO Клиенты (номер_клиента, фио, номер_паспорта) VALUES (@id, @фио, @паспорт)", connection, transaction);
                cmd.Parameters.AddWithValue("@id", c.ClientId);
                cmd.Parameters.AddWithValue("@фио", c.FullName);
                cmd.Parameters.AddWithValue("@паспорт", c.PassportNumber);
                cmd.ExecuteNonQuery();
            }

            foreach (var v in currencies)
            {
                using var cmd = new SqliteCommand(
                    "INSERT OR REPLACE INTO Валюты (код_валюты, название_валюты, курс_продажи, курс_покупки) VALUES (@код, @название, @продажа, @покупка)", connection, transaction);
                cmd.Parameters.AddWithValue("@код", v.Code);
                cmd.Parameters.AddWithValue("@название", v.Name);
                cmd.Parameters.AddWithValue("@продажа", v.SellRate);
                cmd.Parameters.AddWithValue("@покупка", v.BuyRate);
                cmd.ExecuteNonQuery();
            }

            foreach (var k in cashiers)
            {
                using var cmd = new SqliteCommand(
                    "INSERT OR REPLACE INTO Кассиры (номер_кассира, фио_кассира) VALUES (@id, @фио)", connection, transaction);
                cmd.Parameters.AddWithValue("@id", k.CashierId);
                cmd.Parameters.AddWithValue("@фио", k.FullName);
                cmd.ExecuteNonQuery();
            }

            foreach (var s in deals)
            {
                using var cmd = new SqliteCommand(@"
                    INSERT INTO Сделки (код_проданной_валюты, код_купленной_валюты, номер_кассира, номер_клиента, дата_сделки, время_сделки, сумма_проданной_валюты, сумма_купленной_валюты)
                    VALUES (@проданная, @купленная, @кассир, @клиент, @дата, @время, @сумма_прод, @сумма_куп)", connection, transaction);
                cmd.Parameters.AddWithValue("@проданная", s.SoldCurrencyCode);
                cmd.Parameters.AddWithValue("@купленная", s.BoughtCurrencyCode);
                cmd.Parameters.AddWithValue("@кассир", s.CashierId);
                cmd.Parameters.AddWithValue("@клиент", s.ClientId);
                cmd.Parameters.AddWithValue("@дата", s.DealDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@время", s.DealTime.ToString());
                cmd.Parameters.AddWithValue("@сумма_прод", s.SoldAmount);
                cmd.Parameters.AddWithValue("@сумма_куп", s.BoughtAmount);
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    #endregion
}
