# KursMVVM — Автоматизация обменного пункта валют

Avalonia-приложение с архитектурой MVVM для автоматизации учета клиентов, валют, кассиров и сделок по обмену валюты.

## Требования

- .NET 8.0 SDK
- OS: Windows / Linux / macOS

## Стек технологий

| Компонент | Технология |
|-----------|------------|
| GUI | Avalonia UI 11.2 |
| База данных | SQLite |
| Графики | LiveCharts2 |
| Excel | EPPlus 7.x |
| CSV | CsvHelper |
| Auth | BCrypt |
| Архитектура | MVVM (ReactiveUI) |

## Структура проекта

```
KursMVVM/
├── Assets/                # Ресурсы
├── Converters/            # Value converters
│   ├── EqualsConverter.cs
│   └── DateTimeOffsetConverter.cs
├── Models/                # Модели данных
│   ├── Client.cs
│   ├── Currency.cs
│   ├── Cashier.cs
│   ├── Deal.cs
│   ├── User.cs            # Пользователь (Admin/Cashier)
│   └── AuditLog.cs        # Запись аудита
├── Services/              # Бизнес-логика
│   ├── DataBaseService.cs # SQLite + CRUD + отчеты + графики
│   └── AuthService.cs     # Аутентификация и авторизация
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── LoginViewModel.cs
│   ├── MainWindowViewModel.cs
│   ├── DealsViewModel.cs
│   ├── CalculatorViewModel.cs
│   ├── ClientsViewModel.cs
│   ├── CashiersViewModel.cs
│   ├── CurrenciesViewModel.cs
│   ├── ReportsViewModel.cs
│   ├── ChartsViewModel.cs
│   ├── ImportExportViewModel.cs
│   └── AuditViewModel.cs
├── Views/
│   ├── LoginWindow.axaml      # Окно авторизации
│   ├── MainWindow.axaml       # Главное окно (левая навигация)
│   ├── DealsView.axaml
│   ├── CalculatorView.axaml
│   ├── ClientsView.axaml
│   ├── CashiersView.axaml
│   ├── CurrenciesView.axaml
│   ├── ReportsView.axaml
│   ├── ChartsView.axaml
│   ├── ImportExportView.axaml
│   └── AuditView.axaml
├── App.axaml
├── ViewLocator.cs
├── init_database.sql
└── README.md
```

## Запуск

```bash
dotnet restore
dotnet run
```

## Авторизация

| Логин | Пароль | Роль |
|-------|--------|------|
| admin | admin | Администратор |
| cashier1 | cashier1 | Кассир #1 |
| cashier2 | cashier2 | Кассир #2 |
| cashier3 | cashier3 | Кассир #3 |
| cashier4 | cashier4 | Кассир #4 |

### Ролевая модель

**Администратор:**
- Полный доступ ко всем вкладкам
- Управление курсами валют
- Журнал аудита
- Удаление любых данных

**Кассир:**
- Калькулятор (создание сделок только от своего имени)
- Просмотр своих сделок
- Просмотр клиентов, кассиров, валют
- Отчеты и графики
- Без доступа: валюты (редактирование), аудит, удаление

## База данных

При первом запуске `kurs.db` создается автоматически:
- 6 клиентов, 5 валют, 4 кассира, 12 сделок
- Таблица пользователей (admin + 4 кассира)
- Таблица аудита

### SQL-скрипт
`init_database.sql` — для ручного создания в DataGrip.

## Вкладки

| Вкладка | Доступ | Описание |
|---------|--------|----------|
| Калькулятор | Все | Создание сделки с автоподстановкой курса |
| Сделки | Все | История (кассир видит только свои) |
| Клиенты | Все | Справочник клиентов |
| Кассиры | Все | Справочник кассиров |
| Валюты | Только Admin | Управление курсами |
| Отчеты | Все | 5 типов отчетов |
| Графики | Все | 4 типа графиков (LiveCharts2) |
| Импорт/Экспорт | Все | JSON, XML, CSV, Excel с проводником |
| Журнал аудита | Только Admin | История всех действий |

## Дополнительные функции

- **Подтверждение удаления** — диалог перед удалением
- **Множественный выбор** — в DataGrid
- **Журнал аудита** — фиксирует все действия пользователей
- **Проводник файлов** — OpenFileDialog / SaveFileDialog для импорта/экспорта
- **Автосоздание БД** — если файл отсутствует
- **Миграция БД** — новые таблицы добавляются автоматически
