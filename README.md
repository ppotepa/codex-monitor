# Codex Monitor

Mała aplikacja Windows pokazująca zużycie limitów Codex w zasobniku obok zegara. Dane pobiera przez `codex-cli-usage.exe`; sama nie czyta ani nie zapisuje danych logowania.

## Instalacja

1. Pobierz `CodexMonitor-0.1.0-Setup.exe` z zakładki **Releases** i uruchom instalator.
2. Zainstaluj [.NET 10 Desktop Runtime dla Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), jeśli nie jest jeszcze dostępny. Mały instalator nie zawiera runtime’u.
3. Uruchom Codex Monitor z menu Start. Codex CLI powinien być zalogowany na tym samym koncie Windows.

Jeżeli brakuje `codex-cli-usage.exe`, aplikacja pokaże przycisk instalacji przez `uv tool install codex-cli-usage`. Wymaga to [uv](https://docs.astral.sh/uv/getting-started/installation/) w `PATH` lub `%USERPROFILE%\.local\bin`.

Kliknięcie ikony pokazuje limity i czas ich odnowienia. Prawy przycisk otwiera menu z odświeżaniem, autostartem i zamknięciem. Windows może początkowo schować ikonę w rozwijanym zasobniku; można ją przeciągnąć obok zegara.

## Budowanie

Wymagane są .NET 10 SDK oraz Inno Setup 6. Z katalogu projektu uruchom:

```powershell
./build-installer.ps1
```

Instalator powstanie w `installer\`. Można też zbudować samo EXE:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

## Dane

Aplikacja co 5 minut uruchamia `codex-cli-usage.exe json --max-age 300` bez okna konsoli. Pokazuje limity zwrócone w `windows[]`, bez zakładania, że każde konto ma limit 5-godzinny. Gdy odczyt się nie powiedzie, zachowuje ostatni wynik i oznacza go jako nieaktualny.
