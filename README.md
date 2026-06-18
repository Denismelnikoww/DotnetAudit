# DotNetAuditTool

`DotNetAuditTool` — это утилита для аудита .NET-проектов, решений и директорий. Она объединяет построение графа зависимостей, проверку совместимости версий, сканирование на уязвимости и поиск секретов в одном CLI-инструменте.

## Возможности

- `analyze` — полный аудит проекта/решения/папки
  - построение графа зависимостей
  - проверка совместимости версий пакетов
  - сканирование на уязвимости
  - поиск секретов
  - генерация JSON или HTML отчёта по расширению файла
- `graph` — построение и экспорт графа зависимостей
  - вывод в консоль
  - экспорт в Mermaid
  - экспорт в JSON
- `check-versions` — поиск устаревших NuGet-пакетов
- `check-vulns` — проверка пакетов на известные уязвимости
- `scan-secrets` — поиск потенциальных секретов и конфиденциальных данных в файлах

## Использование CLI

Все команды запускаются через CLI-проект:

```powershell
dotnet run --project DotNetAuditTool.CLI -- <command> [options]

dotnet-audit <command> [options]
```

> Если вы хотите запускать утилиту как `dotnet-audit`, можно создать alias или скрипт в shell и привязать его к проекту `DotNetAuditTool.CLI`.

### `analyze`

Выполняет полный аудит .NET-проекта, решения или директории.

Использование:

```powershell
dotnet run --project DotNetAuditTool.CLI -- analyze <path> [--output|-o <file>] [--verbose|-v]

dotnet-audit analyze <path> [--output|-o <file>] [--verbose|-v]
```

Аргументы:
- `<path>` — путь к `.csproj`, `.sln`, `.slnx` или директории.

Опции:
- `--output`, `-o` — путь к файлу для сохранения отчёта. Формат определяется по расширению: `.json` или `.html`.
- `--verbose`, `-v` — вывод подробных таблиц по уязвимостям, устаревшим пакетам и секретам.

Пример:

```powershell
dotnet run --project DotNetAuditTool.CLI -- analyze "D:\Projects\DotNetAuditTool" --output audit-report.json --verbose

dotnet-audit analyze "D:\Projects\DotNetAuditTool" --output audit-report.json --verbose
```

Пример HTML-отчёта:

```powershell
dotnet run --project DotNetAuditTool.CLI -- analyze "D:\Projects\DotNetAuditTool" --output audit-report.html

dotnet-audit analyze "D:\Projects\DotNetAuditTool" --output audit-report.html
```

Примечания:
- Анализатор строит граф зависимостей и сканирует все проекты, которые он находит.
- Сгенерированный файл отчёта автоматически игнорируется при сканировании секретов, чтобы он сам себя не сканировал.
- Формат отчёта и репортера для `analyze` определяется расширением выходного файла: `.json` или `.html`.

### `graph`

Строит и визуализирует граф зависимостей для проекта, решения или директории.

Использование:

```powershell
dotnet run --project DotNetAuditTool.CLI -- graph <path> [--format|-f <format>] [--output|-o <file>]

dotnet-audit graph <path> [--format|-f <format>] [--output|-o <file>]
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

dotnet-audit graph . -f console

dotnet-audit graph "D:\Projects\DotNetAuditTool" -f mermaid -o graph.mmd

dotnet-audit graph "D:\Projects\DotNetAuditTool" -f json -o dependency-graph.json
```

### `check-versions`

Проверяет пакеты на устаревание и совместимость версий.

Использование:

```powershell
dotnet run --project DotNetAuditTool.CLI -- check-versions <path>

dotnet-audit check-versions <path>
```

Аргументы:
- `<path>` — путь к `.csproj`, `.sln`, `.slnx` или директории.

Пример:

```powershell
dotnet run --project DotNetAuditTool.CLI -- check-versions "D:\Projects\DotNetAuditTool"

dotnet-audit check-versions "D:\Projects\DotNetAuditTool"
```

### `check-vulns`

Проверяет зависимости пакетов на известные уязвимости.

Использование:

```powershell
dotnet run --project DotNetAuditTool.CLI -- check-vulns <path>

dotnet-audit check-vulns <path>
```

Аргументы:
- `<path>` — путь к `.csproj`, `.sln`, `.slnx` или директории.

Пример:

```powershell
dotnet run --project DotNetAuditTool.CLI -- check-vulns "D:\Projects\DotNetAuditTool"

dotnet-audit check-vulns "D:\Projects\DotNetAuditTool"
```

### `scan-secrets`

Ищет секреты и потенциальные утечки данных в исходных файлах.

Использование:

```powershell
dotnet run --project DotNetAuditTool.CLI -- scan-secrets <path> [--entropy-threshold|-e <value>] [--output|-o <file>]

dotnet-audit scan-secrets <path> [--entropy-threshold|-e <value>] [--output|-o <file>]
```

Аргументы:
- `<path>` — путь к файлу или директории.

Опции:
- `--entropy-threshold`, `-e` — порог энтропии для обнаружения (по умолчанию `4.5`).
- `--output`, `-o` — путь к файлу для сохранения результатов. Формат выбирается по расширению: `.json` или `.html`.

Примеры:

```powershell
dotnet run --project DotNetAuditTool.CLI -- scan-secrets "D:\Projects\DotNetAuditTool"

dotnet run --project DotNetAuditTool.CLI -- scan-secrets "D:\Projects\DotNetAuditTool" --entropy-threshold 5.0 --output secrets-report.json

dotnet run --project DotNetAuditTool.CLI -- scan-secrets "D:\Projects\DotNetAuditTool" --output secrets-report.html

dotnet-audit scan-secrets "D:\Projects\DotNetAuditTool"

dotnet-audit scan-secrets "D:\Projects\DotNetAuditTool" --entropy-threshold 5.0 --output secrets-report.json

dotnet-audit scan-secrets "D:\Projects\DotNetAuditTool" --output secrets-report.html
```

## Демонстрация

### Полный аудит

```powershell
# Полный аудит с подробным выводом
dotnet run --project DotNetAuditTool.CLI -- analyze . --verbose

dotnet-audit analyze . --verbose

# Аудит с сохранением JSON-отчёта
dotnet run --project DotNetAuditTool.CLI -- analyze . --output audit-report.json

dotnet-audit analyze . --output audit-report.json

# Аудит с сохранением HTML-отчёта
dotnet run --project DotNetAuditTool.CLI -- analyze . --output audit-report.html

dotnet-audit analyze . --output audit-report.html
```

### Граф зависимостей

```powershell
# Вывод графа в консоль
dotnet run --project DotNetAuditTool.CLI -- graph .

dotnet-audit graph .

# Экспорт графа в Mermaid
dotnet run --project DotNetAuditTool.CLI -- graph . --format mermaid --output graph.mmd

dotnet-audit graph . --format mermaid --output graph.mmd

# Экспорт графа в JSON
dotnet run --project DotNetAuditTool.CLI -- graph . --format json --output dependency-graph.json

dotnet-audit graph . --format json --output dependency-graph.json
```

### Проверка версий пакетов

```powershell
# Показать устаревшие пакеты
dotnet run --project DotNetAuditTool.CLI -- check-versions .

dotnet-audit check-versions .
```

### Проверка уязвимостей

```powershell
# Сканировать пакеты на уязвимости
dotnet run --project DotNetAuditTool.CLI -- check-vulns .

dotnet-audit check-vulns .
```

### Сканирование секретов

```powershell
# Простое сканирование
dotnet run --project DotNetAuditTool.CLI -- scan-secrets .

dotnet-audit scan-secrets .

# С повышенным порогом энтропии (меньше false positives)
dotnet run --project DotNetAuditTool.CLI -- scan-secrets . --entropy-threshold 5.0

dotnet-audit scan-secrets . --entropy-threshold 5.0

# Сохранить результаты в JSON
dotnet run --project DotNetAuditTool.CLI -- scan-secrets . --output secrets-report.json

dotnet-audit scan-secrets . --output secrets-report.json

# Сохранить результаты в HTML
dotnet run --project DotNetAuditTool.CLI -- scan-secrets . --output secrets-report.html

dotnet-audit scan-secrets . --output secrets-report.html
```