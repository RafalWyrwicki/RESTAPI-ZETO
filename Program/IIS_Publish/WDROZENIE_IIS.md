# Instalacja SIETOM REST API

Paczka obsługuje Windows 10, Windows 11 oraz Windows Server 2016, 2019, 2022 i 2025.
Kod źródłowy i Visual Studio nie są potrzebne.

## Instalacja jednym uruchomieniem

1. Kliknij archiwum prawym przyciskiem i wybierz **Wyodrębnij wszystkie**.
   Nie uruchamiaj instalatora z podglądu ZIP i nie kopiuj wyłącznie plików
   `URUCHOM_INSTALACJE.cmd` oraz `Install-REST_API.ps1`.
2. Kliknij dwukrotnie `URUCHOM_INSTALACJE.cmd`.
3. Zaakceptuj żądanie uprawnień administratora.
4. Podaj dane SQL, jeżeli nie zostały wcześniej wpisane do `konfiguracja.json`.
5. Po zakończeniu zachowaj ścieżkę do logu oraz aktualnego `Web.config`.

Domyślna witryna działa pod adresem `http://NAZWA-LUB-IP:8088/`, a endpoint
przyjmuje żądania `POST http://NAZWA-LUB-IP:8088/api/dane`.

Przed uruchomieniem sprawdź, czy istnieją pliki:

- `App\Web.config`,
- `App\Global.asax`,
- `App\bin\REST_API.dll`,
- `App\Templates\etykieta.zpl`.

Instalator rozpoznaje również kompletną zawartość paczki zagnieżdżoną o jeden
lub dwa poziomy niżej. Katalog `App` jest jednak obowiązkowy i musi zostać
przeniesiony razem ze skryptami.

## Działanie instalatora

- sprawdza kompletność paczki i brak gotowych sekretów,
- jawnie rozpoznaje Windows 10, Windows 11 lub Windows Server,
- instaluje lokalnie .NET Framework 4.8, tylko jeśli brakuje wersji 4.7.2 lub nowszej,
- włącza IIS, ASP.NET 4.x, .NET Extensibility, ISAPI i konsolę zarządzania,
- tworzy pulę `REST_API` w trybie .NET CLR v4.0 / Integrated,
- instaluje aplikację w wersjonowanym katalogu `C:\inetpub\REST_API\releases`,
- tworzy lub aktualizuje witrynę i dodaje binding HTTP,
- otwiera lokalny port 8088 dla profili Domena, Prywatny i Publiczny,
- konfiguruje połączenie SQL i generuje losowy token API wyłącznie w `Web.config`,
- uruchamia witrynę i wykonuje bezpieczne testy TCP/HTTP.

Każde wdrożenie trafia do osobnego katalogu. Poprzednie wersje nie są usuwane,
co umożliwia ręczne przywrócenie wcześniejszej ścieżki fizycznej w IIS.

## Konfiguracja

Najważniejsze pola `konfiguracja.json`:

- `InstallRoot` – katalog wersji aplikacji,
- `Port` – port HTTP, domyślnie 8088,
- `SqlServer`, `SqlPort`, `SqlDatabase` – serwer i baza,
- `UseWindowsAuthentication` – logowanie domenowe zamiast konta SQL,
- `SqlUser`, `SqlPassword` – opcjonalne dane konta SQL,
- `PrinterTestAddress` – opcjonalny adres drukarki wyłącznie do testu TCP,
- `DownloadDotNetIfMissing` – pobieranie z Microsoft, gdy brak lokalnego instalatora.

Hasło SQL pozostawione puste zostanie pobrane interaktywnie. Token zostanie
wygenerowany kryptograficznie podczas instalacji i zapisany wyłącznie w
`Web.config` aktualnie wdrożonej wersji. Instalator nie tworzy osobnego pliku
z tokenem.

## Pliki po instalacji

- logi: `C:\ProgramData\SIETOM\REST_API\Logs`,
- aplikacja: `C:\inetpub\REST_API\releases\DATA_I_CZAS`.

Token znajduje się w elemencie `appSettings` o kluczu `ApiToken` w pliku
`C:\inetpub\REST_API\releases\DATA_I_CZAS\Web.config`. Dostęp do niego powinien
mieć wyłącznie administrator i konto puli aplikacji.

## Ograniczenia

Instalator nie zmienia zewnętrznych firewalli, routerów, VLAN-ów ani konfiguracji
SQL Servera. Administrator sieci musi zapewnić dostęp do portu API, SQL oraz
drukarki. Dla środowiska produkcyjnego należy skonfigurować HTTPS i certyfikat.

## Reguły dla firewalli i routerów sądu

Jeżeli po instalacji usługa nie jest dostępna pomiędzy podsieciami, informatyk
sądu powinien dopuścić następujący ruch:

- z systemu wywołującego do serwera REST API: **TCP 8088 przychodzący**,
- z serwera REST API do SQL Servera: **TCP 1433 wychodzący** lub port ustawiony
  w `konfiguracja.json`,
- z serwera REST API do każdej drukarki Zebra: **TCP 9100 wychodzący**,
- do serwerów DNS, jeśli używane są nazwy zamiast adresów IP: **UDP/TCP 53**,
- do usług aktualizacyjnych Microsoft: **HTTPS TCP 443** tylko wtedy, gdy
  brakuje lokalnego instalatora .NET i dozwolone jest pobieranie.

Zapory stanowe powinny automatycznie dopuścić ruch odpowiedzi. Nie należy
publikować portu 8088 w Internecie. Regułę należy ograniczyć do adresów lub
podsieci systemów, które faktycznie wywołują API. NAT lub przekierowanie portów
jest potrzebne tylko wtedy, gdy wymaga tego architektura sieci sądu.

Kontrola z odpowiednich komputerów:

```powershell
Test-NetConnection ADRES_API -Port 8088
Test-NetConnection ADRES_SQL -Port 1433
Test-NetConnection ADRES_DRUKARKI -Port 9100
```

Brak odpowiedzi na `ping` nie przesądza o awarii — ICMP nie jest wymagany do
działania API, SQL ani wydruku ZPL.

## Szablon etykiety

Pakiet zawiera dokładną kopię `Templates\etykieta.zpl` z projektu REST API.
Instalator wymaga znaczników `$BARCODE$`, `$NAZWA$` oraz `$EPC$`. Szablon jest
kopiowany razem z każdą wersją aplikacji i nie jest generowany przez instalator.
