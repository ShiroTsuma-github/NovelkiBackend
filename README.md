# Novelki

Novelki to self-hosted aplikacja do prowadzenia prywatnej biblioteki powieści internetowych, mang, manhw i innych
serii. Repozytorium zawiera API w ASP.NET Core, frontend w React oraz gotowy stack Docker Compose z monitoringiem.

## Funkcje

- prywatna biblioteka z postępem czytania, oceną, priorytetem, statusem i historią zmian;
- wyszukiwanie PostgreSQL z filtrami pól, wykluczeniami, fuzzy matchingiem, wildcardami i sugestiami;
- import i eksport CSV oraz pełne archiwum ZIP razem z okładkami;
- automatyczne pobieranie i przetwarzanie okładek, zapis lokalny albo w magazynie zgodnym z S3;
- parser metadanych stron NovelUpdates, Royal Road, Scribble Hub i WebNovel;
- publiczne snapshoty książek, katalog odkrywania i kopiowanie pozycji do własnej biblioteki;
- statystyki biblioteki, wykresy aktywności i kontrola kompletności metadanych;
- panel administracyjny do zarządzania kontami, autorami, tagami i słownikami systemowymi;
- logowanie JWT z tokenami odświeżającymi, rate limitingiem i ochroną operacji kosztownych.

## Stos

| Warstwa | Technologie |
| --- | --- |
| API | .NET 10, ASP.NET Core, MediatR, FluentValidation |
| Dane | EF Core 10, PostgreSQL, `pg_trgm`, `fuzzystrmatch`, opcjonalny Redis |
| Tożsamość | ASP.NET Core Identity, JWT Bearer |
| Frontend | React 19, TypeScript 6, Vite 6, Tailwind CSS 4, TanStack Query |
| Testy | xUnit, SQLite, Testcontainers PostgreSQL, Vitest, Testing Library, Playwright |
| Monitoring | OpenTelemetry, Grafana, Prometheus, Loki, Tempo |

## Szybki start z Docker Compose

Potrzebujesz Dockera oraz instancji PostgreSQL dostępnej z kontenera `api`. Compose uruchamia aplikację i monitoring,
ale nie tworzy PostgreSQL ani Redis.

1. Utwórz lokalny plik konfiguracyjny:

   ```powershell
   Copy-Item .env.example .env
   ```

   W systemie Linux lub macOS:

   ```bash
   cp .env.example .env
   ```

2. Uzupełnij w `.env`:

   - `DB_CONNECTION_STRING` z hostem osiągalnym z kontenera;
   - `JWT_KEY`, `JWT_ISSUER` i `JWT_AUDIENCE`;
   - `ADMIN_EMAIL`, jeżeli aplikacja ma nadać rolę administratora przy starcie.

   Redis jest opcjonalny. Ustaw pusty `REDIS_CONNECTION_STRING`, aby użyć cache in-memory. Jeśli nie korzystasz z S3,
   wyczyść wartości `BOOK_COVERS__S3__*`; API zapisze wtedy okładki w `storage/covers`.

3. Uruchom stack:

   ```powershell
   docker compose up --build -d
   ```

   Możesz też użyć `compose-up.ps1`, `compose-up.sh` albo `compose-up.bat`.

4. Otwórz aplikację:

   | Usługa | Adres |
   | --- | --- |
   | Web HTTPS | `https://localhost:8080` |
   | Web HTTP | `http://localhost:8081` |
   | API | `http://localhost:5232` |
   | Swagger | `http://localhost:5232/swagger` |
   | Health | `http://localhost:5232/health/ready` |
   | Grafana | `http://localhost:3000` |
   | Prometheus | `http://localhost:9090` |

Kontener Web tworzy lokalny certyfikat dla `localhost` i `127.0.0.1`. Przeglądarka może poprosić o jego
zaakceptowanie. Do publicznego wdrożenia zamontuj certyfikat wystawiony dla używanej domeny.

Compose ustawia `Database:AutoMigrate=true`, więc API stosuje migracje podczas uruchamiania.

## Uruchomienie bez Dockera

### API

Wymagania:

- .NET SDK zgodny z `global.json`;
- PostgreSQL z uprawnieniami do utworzenia rozszerzeń `pg_trgm` i `fuzzystrmatch`;
- opcjonalnie Redis.

Repozytorium trzyma w `Api/appsettings.json` placeholdery `IN_SECRETS`. Skonfiguruj sekrety lokalnie:

```powershell
dotnet user-secrets set "Jwt:Key" "<dlugi-klucz-lokalny>" --project Api
dotnet user-secrets set "Jwt:Issuer" "Novelki" --project Api
dotnet user-secrets set "Jwt:Audience" "Novelki" --project Api
dotnet user-secrets set "ConnectionStrings:DB" "Host=localhost;Port=5432;Database=novelki;Username=postgres;Password=<haslo>" --project Api
dotnet user-secrets set "Admin:Emails:0" "<email-administratora>" --project Api
```

Przygotuj narzędzia, bazę i uruchom API:

```powershell
dotnet tool restore
dotnet restore
dotnet ef database update --project Infrastructure --startup-project Api
dotnet run --project Api --launch-profile https
```

Profile deweloperskie wystawiają API pod `https://localhost:7121` i `http://localhost:5232`. Swagger działa w
środowisku `Development` pod `/swagger`.

### Web

```powershell
Set-Location Web
npm ci
npm run dev
```

Vite uruchamia frontend pod `http://localhost:5173` i przekazuje `/api` do `https://localhost:7121`. Inny adres API
ustawisz przez `VITE_API_PROXY_TARGET`.

## Konfiguracja

Najczęściej używane zmienne:

| Zmienna | Wymagana | Opis |
| --- | --- | --- |
| `DB_CONNECTION_STRING` | tak | Connection string PostgreSQL |
| `JWT_KEY` | tak | Klucz podpisujący tokeny |
| `JWT_ISSUER`, `JWT_AUDIENCE` | tak | Issuer i audience JWT |
| `ADMIN_EMAIL` | nie | Konto, które dostanie rolę `Admin` |
| `REDIS_CONNECTION_STRING` | nie | Redis dla cache listy książek |
| `WEB_PUBLIC_ORIGIN` | nie | Publiczny origin dodawany do CORS |
| `API_PORT`, `WEB_PORT`, `WEB_HTTP_PORT` | nie | Porty wystawiane przez Compose |
| `BOOK_COVERS__S3__*` | nie | Endpoint, dane dostępowe i bucket storage S3 |
| `GRAFANA_ADMIN_USER`, `GRAFANA_ADMIN_PASSWORD` | nie | Konto lokalnej Grafany |

Pełny zestaw wartości znajduje się w `.env.example`.

## Wyszukiwanie

Pole `query` w `GET /api/v1/book` łączy zwykły tekst z filtrami:

```text
martial title:"Lord *" author:"Er Gen" tag:favorite -status:Dropped rating:>=8
```

Obsługiwane pola obejmują tytuł, autora, tag, gatunek, status, typ, opis, ocenę, priorytet, postęp, liczbę rozdziałów
oraz daty utworzenia i modyfikacji. Prefiks `-` wyklucza wynik. Wartość `none` znajduje rekordy bez danej metadanej,
na przykład `cover:none`. Cudzysłowy pozwalają używać spacji, a `*` działa jako wildcard w filtrach tekstowych.

Zapytanie bez pola korzysta z indeksu PostgreSQL i fuzzy matchingu. Interfejs podpowiada składnię oraz wartości autora,
tagu, gatunku, statusu i typu na podstawie biblioteki użytkownika.

## Architektura

```text
Web ──HTTP──> Api ──MediatR──> Application ──> Domain
                                   │
                                   └── kontrakty <── Infrastructure
                                                      │
                                                      ├── PostgreSQL / Redis
                                                      ├── storage lokalny / S3
                                                      └── background services
```

- `Api` konfiguruje HTTP, uwierzytelnianie, Swagger, rate limiting i monitoring.
- `Application` zawiera use case'y, DTO, walidację i kontrakty usług.
- `Domain` przechowuje encje, modele wyszukiwania, wyjątki i interfejsy repozytoriów.
- `Infrastructure` implementuje dostęp do danych, Identity, okładki, import, cache i zadania w tle.
- `Web` zawiera aplikację React oraz testy komponentowe i layoutowe.

API używa prefiksu `/api/v1`. Główne grupy endpointów to `account`, `book`, `public-book`, `author`, `tag`, `genre`,
`status`, `type` i `admin`. Swagger w trybie deweloperskim pokazuje pełną, aktualną listę operacji.

## Testy

Backend:

```powershell
dotnet build NovelkiBackend.sln
dotnet test NovelkiBackend.sln --no-build
```

Testy integracyjne korzystają z SQLite oraz kontenera PostgreSQL. Docker musi działać przed ich uruchomieniem.

Frontend:

```powershell
Set-Location Web
npm ci
npx playwright install chromium
npm run test:all
npm run build
```

`test:all` uruchamia sprawdzenie TypeScript, Vitest i testy layoutu Playwright dla widoku desktopowego oraz mobilnego.

## Monitoring

API wysyła logi, metryki i trace'y przez OTLP do OpenTelemetry Collector. Grafana korzysta z przygotowanych źródeł
Prometheus, Loki i Tempo oraz dashboardu `Novelki overview`.

Endpointy diagnostyczne:

- `/health/live` sprawdza proces API;
- `/health/ready` sprawdza gotowość aplikacji i zależności.

## Licencja

Autor projektu nie wybrał jeszcze licencji.
