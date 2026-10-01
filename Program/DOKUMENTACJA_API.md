# Dokumentacja techniczna REST API

Dokument opisuje endpoint REST API przeznaczony do przekazywania danych sprawy oraz drukarki do aplikacji `REST_API`.

Projekt jest aplikacją ASP.NET Web API 2 uruchamianą na .NET Framework 4.7.2. Główny endpoint został zdefiniowany w `Controllers/DaneController.vb`.

## Środowisko i adres bazowy

Adres bazowy zależy od wdrożenia aplikacji w IIS/IIS Express.

W konfiguracji deweloperskiej Visual Studio projekt ma ustawiony adres:

```text
https://localhost:44379/
```

Dla środowiska testowego lub produkcyjnego należy podstawić właściwy adres serwera, np.:

```text
https://twoj-serwer.example.pl/
```

## Autoryzacja

Endpoint wymaga autoryzacji przez nagłówek HTTP `Authorization` w schemacie Bearer.

```http
Authorization: Bearer <TOKEN_API>
```

Token jest konfigurowany po stronie serwera w `Web.config` pod kluczem `ApiToken`. Nie należy umieszczać rzeczywistego tokenu w publicznej dokumentacji, repozytorium ani logach. Token powinien być przekazany integratorowi osobnym, bezpiecznym kanałem.

Wymagane nagłówki:

| Nagłówek | Wartość | Opis |
|---|---|---|
| `Authorization` | `Bearer <TOKEN_API>` | Token dostępowy API. |
| `Content-Type` | `application/json` | Format danych wysyłanych do API. |
| `Accept` | `application/json` | Oczekiwany format odpowiedzi. |

## Endpoint: przekazanie danych

```http
POST /api/dane
```

Endpoint przyjmuje dane w formacie JSON, tworzy 256-bitowy EPC, wypełnia szablon ZPL, wysyła etykietę bezpośrednio do drukarki przez TCP/9100 oraz zapisuje dane w tabelach `PrintHistory` i `OdebraneDane`.

### Request body

```json
{
  "Identyfikator": 106,
  "Wydzial": "XVI",
  "Sekcja": 2,
  "Repertorium": "CC-5",
  "Nr_Sprawy": 98672,
  "Rok": 1922,
  "Tom": "A12",
  "IP_Drukarka": "192.168.1.50",
  "Sygnatura_Text": "106 XVI.2 CC-5 98672/1922",
  "Sygnatura_Barcode": "106XVI2CC5986721922"
}
```

### Pola requestu

| Pole | Typ JSON | Wymagane | Opis |
|---|---:|:---:|---|
| `Identyfikator` | number | Tak | Identyfikator z zakresu od 1 do 9999. |
| `Wydzial` | string | Tak | Wydział samodzielny (`XC`) albo wydział z sekcją (`XC.2` lub `XC 2`). Akceptowana jest również liczba arabska wydziału. |
| `Sekcja` | number | Nie | Liczba 0-255. Można ją podać osobno albo jako część pola `Wydzial`; brak oznacza `0`. |
| `Repertorium` | string | Tak | Maksymalnie 7 znaków ASCII; `_` jest zamieniane na spację. |
| `Nr_Sprawy` | number | Tak | Numer sprawy z zakresu od 1 do 99999. |
| `Rok` | number | Tak | Rok 0-9999; wartości dwucyfrowe są normalizowane według reguły 00-50 -> 2000-2050, 51-99 -> 1951-1999. |
| `Tom` | string | Nie | Od 0 do 8 znaków ASCII, dopełnienie bajtami `00` z prawej. |
| `IP_Drukarka` | string | Tak | Adres IPv4 lub IPv6, maksymalnie 45 znaków. |
| `Sygnatura_Text` | string | Tak | Sygnatura tekstowa, maksymalnie 200 znaków. |
| `Sygnatura_Barcode` | string | Tak | Sygnatura kodu kreskowego, maksymalnie 200 znaków. |

Uwagi implementacyjne:

- Nazwy pól w JSON muszą odpowiadać nazwom w modelu, np. `Nr_Sprawy`, `IP_Drukarka`, `Sygnatura_Text`.
- `Tom` i `Sekcja` można pominąć; zostaną zakodowane jako zera.
- Pola `Identyfikator`, `Nr_Sprawy` i `Rok` są liczbami całkowitymi i są wymagane. Brak pola nie jest utożsamiany z wartością `0`.
- `Wydzial: "XC.2"`, `Wydzial: "XC 2"` oraz `Wydzial: "XC"` z osobnym `Sekcja: 2` tworzą identyczne pola EPC: wydział `005A`, sekcja `02`.
- Jeśli sekcja występuje w `Wydzial` i równocześnie w polu `Sekcja`, obie wartości muszą być identyczne. W przeciwnym razie API zwróci `400 Bad Request`.
- Wartość `radar` jest wyprowadzana z tego samego zakodowanego EPC, dlatego korzysta z identycznego podziału wydziału i sekcji.

### EPC i szablon ZPL

EPC ma zawsze 64 znaki HEX (256 bitów). Dla powyższego przykładu:

```text
00006A00100200000043432D3500000001817007824131320000000000000000
```

Do radaru zwracany jest ten sam EPC bez końcowych pełnych bajtów `00`:

```text
00006A00100200000043432D350000000181700782413132
```

Edytowalny szablon znajduje się w `Templates/etykieta.zpl`. Obsługiwane znaczniki to `$BARCODE$`, `$NAZWA$` i `$EPC$`. Zmiana układu etykiety nie wymaga rekompilacji aplikacji. Zgodnie z wymaganiem aplikacja nie oblicza ani nie dopisuje słowa PC.

## Odpowiedź sukcesu

Status HTTP:

```http
200 OK
```

Body:

```json
{
  "status": "OK",
  "komunikat": "Etykieta została wysłana do drukarki i dane zapisano w bazie."
}
```

## Odpowiedzi błędów

### Brak lub niepoprawna autoryzacja

Status HTTP:

```http
401 Unauthorized
```

Możliwe komunikaty:

| Sytuacja | Komunikat |
|---|---|
| Brak nagłówka `Authorization` | `Brak nagłówka Authorization.` |
| Schemat inny niż `Bearer` | `Nieprawidłowy typ autoryzacji. Wymagany Bearer.` |
| Pusty token Bearer | `Brak tokenu Bearer.` |
| Token inny niż skonfigurowany na serwerze | `Nieprawidłowy token.` |

Odpowiedzi autoryzacji są tworzone jako prosta wartość tekstowa. Przy negocjacji JSON mogą zostać zwrócone jako JSON string, np.:

```json
"Nieprawidłowy token."
```

### Niepoprawne dane wejściowe

Status HTTP:

```http
400 Bad Request
```

Możliwe komunikaty walidacyjne:

| Sytuacja | Komunikat |
|---|---|
| Brak body requestu | `Brak danych w żądaniu.` |
| Brak pola `Identyfikator` lub wartość spoza zakresu 1–9999 | `Identyfikator - błędny format danych` |
| Brak lub puste pole `Wydzial` | `Pole Wydział jest wymagane.` |
| Brak lub puste pole `Repertorium` | `Pole Repertorium jest wymagane.` |
| Brak pola `Nr_Sprawy` lub liczba spoza zakresu 1–99999 | `Nr_Sprawy - błędny format danych - dozwolone liczby od 1 do 99999` |
| Brak pola `Rok` lub wartość spoza zakresu 0-9999 | `Rok - błędny format danych` |
| `Sekcja` poza zakresem 0-255 | `Sekcja - dozwolone liczby od 0 do 255.` |
| `Tom` dłuższy niż 8 znaków lub spoza ASCII | Informacja o nieprawidłowym polu `Tom`. |
| Brak lub puste pole `Sygnatura_Text` | `Pole Sygnatura_Text jest wymagane.` |
| Brak lub puste pole `Sygnatura_Barcode` | `Pole Sygnatura_Barcode jest wymagane.` |
| Brak lub puste pole `IP_Drukarka` | `Pole IP_Drukarka jest wymagane.` |
| Nieprawidłowy format pola `IP_Drukarka` | `Adres IP - nieprawidłowy format` |
| Brak połączenia TCP z drukarką | `Nie można połączyć się z drukarką na porcie 9100.` |

W ASP.NET Web API odpowiedzi `BadRequest("...")` są zwykle serializowane jako obiekt JSON z polem `Message`, np.:

```json
{
  "Message": "Pole Wydział jest wymagane."
}
```

### Pozostałe odpowiedzi ochronne

| Status | Znaczenie |
|---|---|
| `408 Request Timeout` | Klient anulował żądanie lub przekroczono 15-sekundowy timeout endpointu. |
| `413 Request Entity Too Large` | Body przekracza limit 64 KB. IIS może odrzucić takie żądanie przed wejściem do aplikacji. |
| `429 Too Many Requests` | Przekroczono limit 64 równoległych żądań albo 500 żądań na sekundę. Odpowiedź zawiera `Retry-After`. |
| `500 Internal Server Error` | Nieoczekiwany błąd serwera; odpowiedź nie ujawnia szczegółów wyjątku. |
| `503 Service Unavailable` | Timeout zapisu SQL; klient może ponowić żądanie zgodnie ze swoją polityką retry. |

Każda odpowiedź aplikacji zawiera `X-Correlation-ID`, który służy do powiązania
zgłoszenia klienta z logiem serwera. Instrukcja testu dla 50+ równoległych
klientów znajduje się w `TEST_OBCIAZENIOWY.md`.

## Przykłady integracji

### cURL

```bash
curl -X POST "https://localhost:44379/api/dane" \
  -H "Authorization: Bearer <TOKEN_API>" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -d '{
    "Identyfikator": 106,
    "Wydzial": "XVI",
    "Sekcja": 2,
    "Repertorium": "CC-5",
    "Nr_Sprawy": 98672,
    "Rok": 1922,
    "Tom": "A12",
    "IP_Drukarka": "192.168.1.50",
    "Sygnatura_Text": "106 XVI.2 CC-5 98672/1922",
    "Sygnatura_Barcode": "106XVI2CC5986721922"
  }'
```

### PowerShell

```powershell
$headers = @{
  Authorization = "Bearer <TOKEN_API>"
  Accept = "application/json"
}

$body = @{
  Identyfikator = 106
  Wydzial = "XVI"
  Sekcja = 2
  Repertorium = "CC-5"
  Nr_Sprawy = 98672
  Rok = 1922
  Tom = "A12"
  IP_Drukarka = "192.168.1.50"
  Sygnatura_Text = "106 XVI.2 CC-5 98672/1922"
  Sygnatura_Barcode = "106XVI2CC5986721922"
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "https://localhost:44379/api/dane" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body $body
```

### JavaScript

```javascript
const response = await fetch("https://localhost:44379/api/dane", {
  method: "POST",
  headers: {
    "Authorization": "Bearer <TOKEN_API>",
    "Content-Type": "application/json",
    "Accept": "application/json"
  },
  body: JSON.stringify({
    Identyfikator: 106,
    Wydzial: "XVI",
    Sekcja: 2,
    Repertorium: "CC-5",
    Nr_Sprawy: 98672,
    Rok: 1922,
    Tom: "A12",
    IP_Drukarka: "192.168.1.50",
    Sygnatura_Text: "106 XVI.2 CC-5 98672/1922",
    Sygnatura_Barcode: "106XVI2CC5986721922"
  })
});

const data = await response.json();
```

## Efekt po stronie serwera

Po poprawnym przyjęciu requestu aplikacja wysyła ZPL do drukarki, zapisuje historię z wynikiem `OK`, a następnie wykonuje dotychczasowy zapis do tabeli `OdebraneDane`. Jeśli połączenie TCP z drukarką nie powiedzie się, historia otrzymuje status `ERROR` i komunikat w `ErrorMessage`.

Mapowanie do `dbo.PrintHistory`:

| Kolumna | Zapisywana wartość |
|---|---|
| `DocumentType` | `Akta Sądowe` (konfigurowalne przez `PrintHistoryDocumentType`) |
| `SourceSystemId` | `Identyfikator` z requestu |
| `DocumentName` | `Sygnatura_Text` |
| `BarcodeValue` | `Sygnatura_Barcode` |
| `EpcValue` | Pełny EPC 256-bit, 64 znaki HEX |
| `TemplateId` | `0` (konfigurowalne przez `PrintHistoryTemplateId`) |
| `PrinterAddress` | `IP_Drukarka` |
| `Timestamp` | Czas lokalny SQL Servera (`GETDATE()`), zgodny z `OdebraneDane.Data` |
| `Status` | `OK` lub `ERROR` |
| `ErrorMessage` | `NULL` dla sukcesu albo komunikat błędu |
| `RawZplSent` | Pełny ZPL przygotowany do wysłania |

Status `OK` oznacza, że dane ZPL zostały zapisane do połączenia TCP drukarki. Protokół RAW/9100 nie zwraca potwierdzenia fizycznego wydrukowania etykiety.

Mapowanie pól requestu na kolumny:

| Request JSON | Kolumna SQL |
|---|---|
| `Identyfikator` | `Identyfikator` |
| `Wydzial` | `Wydzial` |
| `Repertorium` | `Repertorium` |
| `Nr_Sprawy` | `Nr_Sprawy` |
| `Rok` | `Rok` |
| `Tom` | `Tom` |
| `IP_Drukarka` | `IP_Drukarka` |
| `Sygnatura_Text` | `Sygnatura_Text` |
| `Sygnatura_Barcode` | `Sygnatura_Barcode` |

## Endpointy szablonowe w projekcie

W projekcie istnieje również `Controllers/ValuesController.vb`, czyli standardowy kontroler przykładowy z szablonu ASP.NET Web API. Ze względu na domyślną trasę `api/{controller}/{id}` może on technicznie udostępniać adresy typu:

- `GET /api/values`
- `GET /api/values/{id}`
- `POST /api/values`
- `PUT /api/values/{id}`
- `DELETE /api/values/{id}`

Nie wygląda on jednak na element docelowej integracji. Przed wdrożeniem produkcyjnym warto go usunąć albo zablokować, żeby publicznie dostępne API zawierało tylko potrzebne endpointy.

## Źródła w kodzie

- `Controllers/DaneController.vb` - definicja endpointu `POST /api/dane`, walidacja i zapis do bazy.
- `Models/DaneRequest.vb` - model danych wejściowych.
- `Filters/ApiTokenAuthorizeAttribute.vb` - autoryzacja Bearer Token.
- `App_Start/WebApiConfig.vb` - konfiguracja tras Web API.
- `Web.config` - konfiguracja tokenu API i połączenia SQL Server.
