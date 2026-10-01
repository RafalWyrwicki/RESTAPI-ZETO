SIETOM REST API - WINDOWS 10 / WINDOWS 11 / WINDOWS SERVER
==========================================================

1. Kliknij ZIP prawym przyciskiem i wybierz "Wyodrębnij wszystkie".
   Nie uruchamiaj instalatora z podglądu ZIP i nie kopiuj samych plików CMD/PS1.
2. Opcjonalnie uzupełnij plik konfiguracja.json.
3. Kliknij dwukrotnie URUCHOM_INSTALACJE.cmd.
4. Potwierdź komunikat Kontroli konta użytkownika (UAC).
5. Jeżeli dane SQL nie są wpisane w konfiguracja.json, podaj je w oknie instalatora.

Przed uruchomieniem muszą istnieć między innymi:
KATALOG_PACZKI\App\Web.config
KATALOG_PACZKI\App\bin\REST_API.dll
KATALOG_PACZKI\App\Templates\etykieta.zpl

Instalator potrafi odnaleźć zawartość paczki zagnieżdżoną maksymalnie dwa
poziomy niżej, ale katalog App musi zostać skopiowany na komputer docelowy.

Instalator:
- rozpoznaje Windows 10, Windows 11 lub Windows Server,
- sprawdza i w razie potrzeby instaluje .NET Framework 4.8,
- włącza IIS, ASP.NET 4.x, ISAPI i wymagane moduły,
- tworzy pulę aplikacji i witrynę REST_API,
- domyślnie otwiera port TCP 8088 dla profili Domena/Prywatny/Publiczny,
- instaluje każdą wersję w osobnym katalogu releases,
- konfiguruje SQL i generuje token API wyłącznie w Web.config,
- nadaje wymagane uprawnienia i uruchamia witrynę,
- zapisuje log w C:\ProgramData\SIETOM\REST_API\Logs.

Token API znajduje się wyłącznie w pliku Web.config aktualnie wdrożonej wersji:
C:\inetpub\REST_API\releases\DATA_I_CZAS\Web.config
Należy odczytać wartość appSettings / ApiToken jako administrator.

Domyślny adres:
http://ADRES_KOMPUTERA:8088/

Endpoint:
POST http://ADRES_KOMPUTERA:8088/api/dane

UWAGA:
- Instalacja wymaga konta administratora.
- Pakiet nie otworzy reguł na zewnętrznych firewallach ani routerach sądu.
- Informatyk sądu musi dopuścić: system źródłowy -> API TCP 8088,
  serwer API -> SQL TCP 1433 oraz serwer API -> drukarki TCP 9100.
- Gdy używane są nazwy DNS, wymagany jest również DNS UDP/TCP 53.
- Portu 8088 nie należy wystawiać bezpośrednio do Internetu; należy ograniczyć
  źródła do wymaganych podsieci sądu.
- Konto SQL musi mieć uprawnienia INSERT do dbo.PrintHistory i dbo.OdebraneDane.
- Po instalacji składników Windows może być wymagany restart.
- Produkcyjnie zalecane jest HTTPS i certyfikat wystawiony dla nazwy serwera.
