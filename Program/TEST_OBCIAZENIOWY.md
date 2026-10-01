# Test obciążeniowy REST API

Test zapisuje rekordy do tabeli `OdebraneDane`. Należy uruchamiać go na
środowisku testowym, użyć osiągalnego adresu drukarki i przed testem uzgodnić
sposób usunięcia danych testowych.

## Wymagania

- działająca aplikacja w IIS/IIS Express,
- dostęp aplikacji do testowego SQL Servera,
- zainstalowane narzędzie `k6`,
- token API oraz adres drukarki osiągalny z serwera aplikacji.

## Uruchomienie 60 równoległych klientów

PowerShell:

```powershell
$env:BASE_URL = "https://localhost:44379"
$env:API_TOKEN = "<TOKEN_API>"
$env:PRINTER_IP = "<OSIAGALNY_ADRES_IP>"
$env:VUS = "60"
$env:DURATION = "30s"

k6 run .\LoadTests\load-test.js
```

Domyślny scenariusz utrzymuje 60 równoległych klientów przez 30 sekund. Można
zmienić `VUS` i `DURATION`, np. najpierw wykonać próbę z 10 klientami, następnie
50, 60 i 100.

## Kryteria oceny

- błędy HTTP poniżej 1%,
- czas odpowiedzi p95 poniżej 5 sekund,
- brak odpowiedzi `5xx`,
- przy 60 klientach brak `429`; przy większym obciążeniu `429` jest poprawnym
  mechanizmem ochronnym po przekroczeniu limitu równoległości lub liczby żądań,
- stabilne użycie CPU i pamięci procesu IIS,
- liczba aktywnych połączeń SQL nie przekracza rozmiaru puli,
- po zakończeniu testu nie pozostają długo wykonywane zapytania SQL.

Każda odpowiedź zawiera `X-Correlation-ID`, który można zestawić z logiem
serwera. Limity są konfigurowane w `Web.config` przez
`MaxConcurrentRequests`, `RateLimitPerSecond`, `ApiRequestTimeoutSeconds`,
`SqlCommandTimeoutSeconds` i parametry puli w connection stringu.

Kompresja dynamiczna wymaga włączenia funkcji IIS **Dynamic Content
Compression** na serwerze. Sam projekt włącza jej użycie w `Web.config`.
