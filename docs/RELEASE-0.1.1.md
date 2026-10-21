Naprawa odczytu limitów, gdy `codex-cli-usage.exe` wskazuje na brakujące środowisko Pythona.

- Automatyczna, jednorazowa próba instalacji lub naprawy przez `uv` z Pythonem 3.12.
- Gdy instalacja pakietu nie zadziała, instalacja rdzenia z przypiętej wersji `v0.4.1` repozytorium `wakamex/clanker-usage` i odczyt przez `clanker-usage codex json`.
- Przycisk „Napraw CLI” pozwala ponowić próbę; komunikat błędu jest dostępny w podpowiedzi.

Instalator dla Windows x64 ma około 2,1 MB i wymaga .NET 10 Desktop Runtime oraz `uv` do automatycznej instalacji narzędzia CLI.
