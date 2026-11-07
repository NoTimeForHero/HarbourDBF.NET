# HarbourDBF.NET

Библиотека для доступа из C# к DBF через Harbour: чтение и запись полей, memo FPT, подключение CDX и поиск по индексам. Операции выполняет Harbour RDD; при записи нужно открыть все индексы, которые должны сопровождаться.

## Требования и сборка

Проверенная конфигурация: Windows, процесс x86, Harbour 3.2.0dev (r2503200530), Borland BCC 5.8, .NET Framework 4.7.2 targeting pack и Newtonsoft.Json 13.0.3. Проверка x64 отложена. Нативная DLL и процесс приложения должны иметь одинаковую разрядность.

Восстановите пакет `Newtonsoft.Json` из `Library/packages.config` в каталог `packages` через Visual Studio или NuGet CLI. Например, с установленным `nuget.exe` из корня репозитория:

```powershell
nuget restore Library/packages.config -PackagesDirectory packages
```

В Developer PowerShell для Visual Studio вместо NuGet CLI можно восстановить `packages.config` через `msbuild`:

```powershell
msbuild Library/HarbourDBF.NET.csproj -t:Restore -p:RestorePackagesConfig=true "-p:SolutionDir=$((Get-Location).Path)/"
```

Затем соберите обе части из корня репозитория, указав свои пути к Harbour и компилятору:

```powershell
$env:PATH = 'C:\borland\bcc58\bin;C:\MiniGUI_2607\harbour\bin;' + $env:PATH
./HarbourLibrary/compile.ps1
dotnet build Library/HarbourDBF.NET.csproj -c Release --no-restore
```

В приложение включите `Library/bin/Release/HarbourDBF.NET.dll`, `Newtonsoft.Json.dll` и соответствующую ей по версии и разрядности `HarbourLibrary/hbdbf.dll`. При использовании `ProjectReference` управляемая DLL и зависимость обычно копируются сборкой; нативную DLL нужно доставить рядом с приложением самостоятельно. `compile.ps1` принимает путь к `hbmk2` через `-Hbmk2`.

## Быстрый пример чтения

Пример открывает готовую синтетическую таблицу только для чтения и загружает отдельный CDX. Выполняйте его из корня репозитория или подставьте полный путь к каталогу `tests/fixtures`.

```csharp
using System;
using System.IO;
using System.Linq;
using System.Text;
using HarbourDBF.NET;

string dir = Path.GetFullPath(@"tests\fixtures");
DbfHarbour.UseReadOnly(Path.Combine(dir, "basic.dbf"), "PEOPLE", true, "UA866");
try
{
    DbfHarbour.Indexes.Load(Path.Combine(dir, "basic_id.cdx"));
    DbfHarbour.Indexes.Select("ID");
    if (DbfHarbour.Indexes.Seek(2))
    {
        object[] values = DbfHarbour.GetValues(new[] { "ID", "NAME", "NOTE" }, Encoding.GetEncoding(866)).ToArray();
        Console.WriteLine($"{values[0]}: {((string)values[1]).TrimEnd()} — {values[2]}");
    }
}
finally { DbfHarbour.CloseArea("PEOPLE"); }
```

Перед изменением собственной таблицы откройте её в режиме записи и подключите все связанные индексы. Примеры изменения и блокировки находятся в [руководстве по API](docs/usage.md) и в [исходном коде лёгких проверок](tests/Program.cs).

## Лёгкие проверки

После сборки нативной DLL и восстановления `Newtonsoft.Json` из корня репозитория:

```powershell
dotnet restore tests/LightTests.csproj
dotnet build tests/LightTests.csproj --no-restore
./tests/bin/Debug/net472/LightTests.exe
```

Проект создаёт x86-приложение и копирует `hbdbf.dll` и файлы примеров в каталог сборки. Запись производится во временных копиях; данные в репозитории не изменяются. При успехе выводятся строки `PASS` и возвращается код 0. Генерация DBF, соседний `read-dbf` и Node.js для запуска не нужны. Подробности, состав файлов и восстановление тестовых данных описаны в [документации тестов](docs/testing.md).

## Документация

- [Типизированное чтение DBF](docs/typed-api.md) — первая версия API и её ограничения.

- [Типы полей и представление значений](docs/types.md).
- [Работа с таблицами, индексами и блокировками](docs/usage.md).
- [Тестовые данные и запуск проверок](docs/testing.md).
- [План развития](PLAN.md).

Лёгкие примеры иллюстрируют ограниченный набор операций на синтетических данных. Они не подтверждают все варианты схем и выражений CDX, длительную конкурентную нагрузку, SMB или совместимость с конкретной CRM. Более широкие сценарии и независимые проверки Harbour находятся в отдельном проекте `read-dbf`.

## Сравнительная таблица возможностей .NET-библиотек

Это ориентировочное сравнение для выбора направления исследования, а не единый тест совместимости. Возможности сторонних библиотек зависят от версии и формата таблицы; ссылки ведут на их исходные проекты. `⚠️` означает частичную поддержку или необходимость дополнительной проверки.

| Возможность | [FastDBF](https://github.com/SocialExplorer/FastDBF) | [dBASE.NET](https://github.com/henck/dBASE.NET) | [Форк dBASE.NET](https://github.com/NoTimeForHero/dBASE.NET) | HarbourDBF.NET |
| --- | --- | --- | --- | --- |
| Основные типы DBF | ✔️ | ✔️ | ✔️ | ✔️ |
| Служебные типы Harbour, например AutoIncrement | ❌ | ❌ | ⚠️¹ | ✔️² |
| Memo FPT | ❌ | ⚠️³ | ✔️⁴ | ✔️² |
| Memo DBT | ❌ | ⚠️³ | ✔️⁴ | Не проверено |
| Индексы CDX | ❌ | ❌ | ⚠️⁴ | ✔️² |
| Производительность, историческая оценка⁵ | 5⭐ | 2⭐ | 5⭐ | 3⭐ |

1. Форк декларирует чтение дополнительных полей; полное автоматическое поведение служебных полей, включая AutoIncrement, в его README отмечено как незавершённое.
2. Для HarbourDBF.NET подтверждены конкретные синтетические сценарии x86: служебные поля `+`, `=`, `^`, `@`, memo FPT и отдельные CDX. Подробности и границы проверок — в [описании типов](docs/types.md) и [тестов](docs/testing.md).
3. [README dBASE.NET](https://github.com/henck/dBASE.NET#working-with-memo-files) описывает чтение DBT/FPT. В актуальных исходниках также присутствует [запись FPT](https://github.com/henck/dBASE.NET/blob/master/dBASE.NET/DbfRecord.cs); она здесь независимо не проверялась, а опубликованный пакет может отличаться. Запись DBT требует отдельного подтверждения.
4. [README форка](https://github.com/NoTimeForHero/dBASE.NET) заявляет чтение и запись DBT/FPT; поддержку индексов он отмечает как находящуюся в разработке. Совместимость с конкретными файлами и приложениями нужно проверять отдельно.
5. Звёзды сохранены из прежней версии README как субъективная историческая оценка. Сравнительного измерения текущих версий и одинаковых сценариев здесь нет; эти оценки не следует считать актуальным бенчмарком.
