# DotNetAuditTool

`DotNetAuditTool` — это утилита для аудита .NET-проектов, решений и директорий. Она объединяет построение графа зависимостей, проверку совместимости версий, сканирование на уязвимости и поиск секретов в одном CLI-инструменте.

## Возможности

- `analyze` — полный аудит проекта/решения/папки
  - построение графа зависимостей
  - проверка совместимости версий пакетов
  - сканирование на уязвимости
  - поиск секретов
  - генерация JSON-отчёта
- `graph` — построение и экспорт графа зависимостей
  - вывод в консоль
  - экспорт в Mermaid
  - экспорт в JSON
- `check-versions` — поиск устаревших NuGet-пакетов и генерация скрипта обновления
- `check-vulns` — проверка пакетов на известные уязвимости
- `scan-secrets` — поиск потенциальных секретов и конфиденциальных данных в файлах

## Сборка

Из корня репозитория:

```powershell
dotnet build
```

## Использование CLI

Все команды запускаются через CLI-проект:

```powershell
dotnet run --project DotNetAuditTool.CLI -- <command> [options]
```

### `analyze`

Выполняет полный аудит .NET-проекта, решения или директории.

Использование:

```powershell
dotnet run --project DotNetAuditTool.CLI -- analyze <path> [--output|-o <file>] [--verbose|-v]
```

Аргументы:
- `<path>` — путь к `.csproj`, `.sln`, `.slnx` или директории.

Опции:
- `--output`, `-o` — путь к файлу для сохранения JSON-отчёта (по умолчанию `audit-report.json`).
- `--verbose`, `-v` — вывод подробных таблиц по уязвимостям, устаревшим пакетам и секретам.

Пример:

```powershell
dotnet run --project DotNetAuditTool.CLI -- analyze "D:\Projects\DotNetAuditTool" --output audit-report.json --verbose
```

Примечания:
- Анализатор строит граф зависимостей и сканирует все проекты, которые он находит.
- Сгенерированный файл отчёта автоматически игнорируется при сканировании секретов, чтобы он сам себя не сканировал.

### `graph`

Строит и визуализирует граф зависимостей для проекта, решения или директории.

Использование:

```powershell
dotnet run --project DotNetAuditTool.CLI -- graph <path> [--format|-f <format>] [--output|-o <file>]
```

Аргументы:
- `<path>` — путь к `.csproj`, `.sln`, `.slnx` или директории. По умолчанию текущая директория.

Опции:
- `--format`, `-f` — формат вывода: `console`, `mermaid` или `json` (по умолчанию `console`).
- `--output`, `-o` — путь к файлу для `mermaid` или `json` вывода.

Примеры:

```powershell
dotnet run --project DotNetAuditTool.CLI -- graph . -f console

dotnet run --project DotNetAuditTool.CLI -- graph "D:\Projects\DotNetAuditTool" -f mermaid -o graph.mmd

dotnet run --project DotNetAuditTool.CLI -- graph "D:\Projects\DotNetAuditTool" -f json -o dependency-graph.json
```

### `check-versions`

Проверяет пакеты на устаревание и при необходимости генерирует PowerShell-скрипт обновления.

Использование:

```powershell
dotnet run --project DotNetAuditTool.CLI -- check-versions <path> [--fix|-f]
```

Аргументы:
- `<path>` — путь к `.csproj`, `.sln`, `.slnx` или директории.

Опции:
- `--fix`, `-f` — сгенерировать файл `update-packages.ps1` с командами `dotnet add package` для устаревших пакетов.

Пример:

```powershell
dotnet run --project DotNetAuditTool.CLI -- check-versions "D:\Projects\DotNetAuditTool"

dotnet run --project DotNetAuditTool.CLI -- check-versions "D:\Projects\DotNetAuditTool" --fix
```

### `check-vulns`

Проверяет зависимости пакетов на известные уязвимости.

Использование:

```powershell
dotnet run --project DotNetAuditTool.CLI -- check-vulns <path> [--github-token|-t <token>]
```

Аргументы:
- `<path>` — путь к `.csproj`, `.sln`, `.slnx` или директории.

Опции:
- `--github-token`, `-t` — опциональный GitHub-токен для работы с advisory API.

Пример:

```powershell
dotnet run --project DotNetAuditTool.CLI -- check-vulns "D:\Projects\DotNetAuditTool"

dotnet run --project DotNetAuditTool.CLI -- check-vulns "D:\Projects\DotNetAuditTool" --github-token YOUR_TOKEN
```

### `scan-secrets`

Ищет секреты и потенциальные утечки данных в исходных файлах.

Использование:

```powershell
dotnet run --project DotNetAuditTool.CLI -- scan-secrets <path> [--entropy-threshold|-e <value>] [--output|-o <file>]
```

Аргументы:
- `<path>` — путь к файлу или директории.

Опции:
- `--entropy-threshold`, `-e` — порог энтропии для обнаружения (по умолчанию `4.5`).
- `--output`, `-o` — путь к файлу для сохранения JSON-результатов.

Примеры:

```powershell
dotnet run --project DotNetAuditTool.CLI -- scan-secrets "D:\Projects\DotNetAuditTool"

dotnet run --project DotNetAuditTool.CLI -- scan-secrets "D:\Projects\DotNetAuditTool" --entropy-threshold 5.0 --output secrets-report.json
```

## Примечания по реализации

- CLI реализован с помощью `System.CommandLine` и содержит команды в `DotNetAuditTool.CLI/Commands`.
- Сериализация JSON-отчётов вынесена в общий интерфейс `DotNetAuditTool.CLI.Reporters.IReportWriter<T>` и класс `JsonReportWriter<T>`.
- Построение графа зависимостей выполняется в `DotNetAuditTool.DependencyGraphBuilder`.
- Логика поиска секретов реализована в `DotNetAuditTool.Secrets` и автоматически игнорирует сгенерированные файлы отчётов.
- Сканирование уязвимостей и проверка версий реализованы в `DotNetAuditTool.Security` и `DotNetAuditTool.VersionChecker`.

## Примечания

- Пути могут быть абсолютными или относительными.
- Если путь не указан, некоторые команды используют текущую директорию по умолчанию.
- Команда `graph` поддерживает экспорт в Mermaid для визуализации.

## Дальнейшие улучшения

- Добавить поддержку XML-отчётов через `XmlReportWriter<T>`.
- Добавить выбор типа репортера на уровне команды.
- Расширить визуализацию графа зависимостей и вывод дерева пакетов.
