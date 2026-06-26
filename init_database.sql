-- ============================================================
-- Скрипт инициализации базы данных SQLite
-- Проект: KursMVVM (Автоматизация обменного пункта валют)
-- ============================================================

DROP TABLE IF EXISTS Аудит;
DROP TABLE IF EXISTS Пользователи;
DROP TABLE IF EXISTS Сделки;
DROP TABLE IF EXISTS Клиенты;
DROP TABLE IF EXISTS Валюты;
DROP TABLE IF EXISTS Кассиры;

-- ============================================================
-- 1. Клиенты
-- ============================================================
CREATE TABLE Клиенты (
    номер_клиента   INTEGER PRIMARY KEY AUTOINCREMENT,
    фио             TEXT    NOT NULL,
    номер_паспорта  TEXT    NOT NULL UNIQUE
);
CREATE INDEX idx_клиенты_фио ON Клиенты(фио);
CREATE INDEX idx_клиенты_паспорт ON Клиенты(номер_паспорта);

-- ============================================================
-- 2. Валюты
-- ============================================================
CREATE TABLE Валюты (
    код_валюты      TEXT PRIMARY KEY,
    название_валюты TEXT    NOT NULL,
    курс_продажи    REAL    NOT NULL,
    курс_покупки    REAL    NOT NULL,
    CONSTRAINT chk_курс_продажи CHECK (курс_продажи > 0),
    CONSTRAINT chk_курс_покупки CHECK (курс_покупки > 0)
);
CREATE INDEX idx_валюты_название ON Валюты(название_валюты);

-- ============================================================
-- 3. Кассиры
-- ============================================================
CREATE TABLE Кассиры (
    номер_кассира   INTEGER PRIMARY KEY AUTOINCREMENT,
    фио_кассира     TEXT    NOT NULL
);
CREATE INDEX idx_кассиры_фио ON Кассиры(фио_кассира);

-- ============================================================
-- 4. Сделки
-- ============================================================
CREATE TABLE Сделки (
    номер_сделки            INTEGER PRIMARY KEY AUTOINCREMENT,
    код_проданной_валюты    TEXT    NOT NULL,
    код_купленной_валюты    TEXT    NOT NULL,
    номер_кассира           INTEGER NOT NULL,
    номер_клиента           INTEGER NOT NULL,
    дата_сделки             DATE    NOT NULL,
    время_сделки            TIME    NOT NULL,
    сумма_проданной_валюты  REAL    NOT NULL,
    сумма_купленной_валюты  REAL    NOT NULL,
    CONSTRAINT fk_проданная FOREIGN KEY (код_проданной_валюты) REFERENCES Валюты(код_валюты) ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT fk_купленная FOREIGN KEY (код_купленной_валюты) REFERENCES Валюты(код_валюты) ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT fk_кассир    FOREIGN KEY (номер_кассира) REFERENCES Кассиры(номер_кассира) ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT fk_клиент    FOREIGN KEY (номер_клиента) REFERENCES Клиенты(номер_клиента) ON DELETE RESTRICT ON UPDATE CASCADE,
    CONSTRAINT chk_сумма_прод CHECK (сумма_проданной_валюты > 0),
    CONSTRAINT chk_сумма_куп  CHECK (сумма_купленной_валюты > 0),
    CONSTRAINT chk_дата       CHECK (дата_сделки <= DATE('now'))
);
CREATE INDEX idx_сделки_дата   ON Сделки(дата_сделки);
CREATE INDEX idx_сделки_клиент ON Сделки(номер_клиента);
CREATE INDEX idx_сделки_кассир ON Сделки(номер_кассира);
CREATE INDEX idx_сделки_валюты ON Сделки(код_проданной_валюты, код_купленной_валюты);

-- ============================================================
-- 5. Пользователи (администраторы и кассиры)
-- ============================================================
CREATE TABLE Пользователи (
    id_пользователя INTEGER PRIMARY KEY AUTOINCREMENT,
    логин           TEXT    NOT NULL UNIQUE,
    пароль_хэш      TEXT    NOT NULL,
    роль            TEXT    NOT NULL DEFAULT 'Cashier' CHECK (роль IN ('Administrator', 'Cashier')),
    номер_кассира   INTEGER,
    FOREIGN KEY (номер_кассира) REFERENCES Кассиры(номер_кассира) ON DELETE SET NULL ON UPDATE CASCADE
);

-- ============================================================
-- 6. Аудит (журнал действий)
-- ============================================================
CREATE TABLE Аудит (
    id_записи       INTEGER PRIMARY KEY AUTOINCREMENT,
    дата_время      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    пользователь    TEXT    NOT NULL,
    действие        TEXT    NOT NULL,
    детали          TEXT
);
CREATE INDEX idx_аудит_дата ON Аудит(дата_время);

-- ============================================================
-- Тестовые данные
-- ============================================================

INSERT INTO Клиенты (номер_клиента, фио, номер_паспорта) VALUES
(1, 'Иванов Иван Иванович', '4501 123456'),
(2, 'Петрова Анна Сергеевна', '4502 234567'),
(3, 'Сидоров Виктор Петрович', '4503 345678'),
(4, 'Козлова Елена Николаевна', '4504 456789'),
(5, 'Михайлов Дмитрий Алексеевич', '4505 567890'),
(6, 'Соколова Мария Игоревна', '4506 678901');

INSERT INTO Валюты (код_валюты, название_валюты, курс_продажи, курс_покупки) VALUES
('USD', 'Доллар США', 95.50, 93.00),
('EUR', 'Евро', 105.20, 102.50),
('GBP', 'Фунт стерлингов', 125.00, 121.50),
('CNY', 'Китайский юань', 13.80, 13.20),
('CHF', 'Швейцарский франк', 108.00, 105.00);

INSERT INTO Кассиры (номер_кассира, фио_кассира) VALUES
(1, 'Кассирова Анна Ивановна'),
(2, 'Обменников Петр Сергеевич'),
(3, 'Валютова Елена Владимировна'),
(4, 'Банкоматов Дмитрий Алексеевич');

INSERT INTO Сделки (код_проданной_валюты, код_купленной_валюты, номер_кассира, номер_клиента, дата_сделки, время_сделки, сумма_проданной_валюты, сумма_купленной_валюты) VALUES
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
('RUB', 'GBP', 4, 4, '2025-06-06', '14:50:00', 20000, 160);

-- Пользователи (пароли хэшированы BCrypt)
-- admin / admin
-- cashier1..4 / cashier1..4
INSERT INTO Пользователи (логин, пароль_хэш, роль, номер_кассира) VALUES
('admin', '$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', 'Administrator', NULL),
('cashier1', '$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', 'Cashier', 1),
('cashier2', '$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', 'Cashier', 2),
('cashier3', '$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', 'Cashier', 3),
('cashier4', '$2a$10$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy', 'Cashier', 4);

-- Проверка
SELECT 'Клиенты' AS таблица, COUNT(*) AS записей FROM Клиенты
UNION ALL SELECT 'Валюты', COUNT(*) FROM Валюты
UNION ALL SELECT 'Кассиры', COUNT(*) FROM Кассиры
UNION ALL SELECT 'Сделки', COUNT(*) FROM Сделки
UNION ALL SELECT 'Пользователи', COUNT(*) FROM Пользователи;
