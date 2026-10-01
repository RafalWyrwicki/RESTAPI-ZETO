# RESTAPI ZETO

Katalog zawiera aktualny kod źródłowy REST API, pliki wdrożeniowe IIS,
instalatory aplikacji bez dużych składników systemowych oraz kompletną
dokumentację techniczną i użytkową.

## Układ katalogu

- `Program` — kod źródłowy i pliki wdrożeniowe REST API.
- `Dokumentacja techniczna i użytkowa SIETOM RFID.pdf` — dokumentacja zbiorcza.

## Konfiguracja

Pliki `Web.config` zapisane w repozytorium nie zawierają rzeczywistych haseł ani
tokenów. Przed uruchomieniem należy uzupełnić wartości `UZUPELNIJ_...` albo użyć
instalatora, który generuje token API i pobiera konfigurację środowiska.

## Składniki systemowe przechowywane poza repozytorium

Ze względu na limit rozmiaru pojedynczego pliku w GitHub nie zapisano lokalnych
kopii instalatorów:

- `NDP48-x86-x64-AllOS-ENU.exe`,
- `SQLEXPR_x64_ENU_2017.exe`,
- `SQLEXPR_x64_ENU_2022.exe`.

W razie wdrożenia bez dostępu do Internetu należy umieścić wymagane instalatory
w odpowiednich katalogach `Prerequisites` przed uruchomieniem instalacji.
Katalogi `bin`, `obj`, `packages`, `.vs` i `tmp` są odtwarzalne i nie są częścią
kopii repozytorium.
