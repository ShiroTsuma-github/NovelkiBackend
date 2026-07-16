# NovelkiBackend

Backend ASP.NET Core dla aplikacji do zarzadzania novelkami/ksiazkami. Projekt jest ulozony warstwowo w stylu Clean
Architecture:

- `Api` - punkt wejscia HTTP, kontrolery, Swagger, Serilog i konfiguracja middleware.
- `Application` - use case'y CQRS/MediatR, walidatory FluentValidation, DTO i mapowania extension methods.
- `Domain` - encje, interfejsy repozytoriow i wyjatki domenowe.
- `Infrastructure` - EF Core/PostgreSQL, ASP.NET Identity, JWT, implementacje repozytoriow, obsluga bledow.
- `Application.UnitTests` - projekt testowy xUnit z testami walidatorow, handlerow i mapowan aplikacyjnych.
- `Infrastructure.IntegrationTests` - testy integracyjne EF Core/repozytoriow na SQLite in-memory.

## Stack

- .NET 10 (`net10.0`)
- ASP.NET Core Web API
- MediatR 13
- FluentValidation 12
- EF Core 9 + Npgsql/PostgreSQL
- ASP.NET Core Identity z kluczem `Guid`
- JWT Bearer auth
- Serilog
- Swagger/Swashbuckle
- xUnit, Moq, coverlet
- SQLite in-memory w testach integracyjnych

## Uruchomienie lokalne

Wymagana jest konfiguracja sekretow, bo `Api/appsettings.json` zawiera placeholdery `IN_SECRETS`.

Typowe ustawienia:

```powershell
dotnet user-secrets set "Jwt:Key" "<dlugi-sekretny-klucz>" --project Api
dotnet user-secrets set "Jwt:Issuer" "NovelkiBackend" --project Api
dotnet user-secrets set "Jwt:Audience" "NovelkiBackend" --project Api
dotnet user-secrets set "ConnectionStrings:DB" "Host=localhost;Port=5432;Database=novelki;Username=postgres;Password=<password>" --project Api
dotnet user-secrets set "Admin:Emails:0" "<admin-email>" --project Api
```

Frontend uruchamiany jako osobny dev server powinien miec origin dopisany w `Cors:AllowedOrigins`. Domyslnie
`Api/appsettings.json` dopuszcza `http://localhost:5173`, `http://localhost:3000` i `http://localhost:4200`.
Rola `Admin` jest tworzona przy starcie API. Uzytkownicy z mailami z `Admin:Emails` dostaja role automatycznie i musza
zalogowac sie ponownie, zeby token JWT zawieral uprawnienia admina.

Start API:

```powershell
dotnet run --project Api --launch-profile https
```

Profile z `Api/Properties/launchSettings.json`:

- HTTP: `http://localhost:5232`
- HTTPS: `https://localhost:7121` oraz `http://localhost:5232`

W trybie `Development` endpoint root przekierowuje do Swaggera pod `/swagger`.

## Lokalny stack Docker + observability

Repo zawiera lokalny stack: Web, API, OpenTelemetry Collector, Loki, Tempo, Prometheus i Grafana. Baza danych jest brana
z connection stringa w `.env`.

1. Skopiuj `.env.example` do `.env` i ustaw przynajmniej `DB_CONNECTION_STRING`, `JWT_KEY` i `ADMIN_EMAIL`.
2. Uruchom:

```powershell
docker compose up --build
```

Domyslne adresy:

- Web: `http://localhost:8080`
- API: `http://localhost:5232`
- API health: `http://localhost:5232/health/ready`
- Grafana: `http://localhost:3000`
- Prometheus: `http://localhost:9090`
- Loki: `http://localhost:3100`
- Tempo: `http://localhost:3200`

Porty Web, API i Grafany mozna zmienic przez `WEB_PORT`, `API_PORT` i `GRAFANA_PORT` w `.env`. W compose API ustawia
`Database:AutoMigrate=true`, wiec migracje EF sa aplikowane przy starcie kontenera. Poza compose domyslnie zostaje
`false`.

Observability flow:

```text
API --OTLP logs/traces/metrics--> OpenTelemetry Collector
Collector logs  --> Loki
Collector traces --> Tempo
Collector metrics --> Prometheus scrape endpoint
Grafana czyta Loki, Tempo i Prometheus jako provisioned datasources
```

## Migracje EF Core

Komendy sa zapisane tez w `Migrations.txt`:

```powershell
dotnet ef migrations add <name> --project Infrastructure --startup-project Api
dotnet ef database update --project Infrastructure --startup-project Api
```

`ApplicationDbContext` mieszka w `Infrastructure/Contexts/ApplicationDbContext.cs` i dziedziczy po
`IdentityDbContext<User, IdentityRole<Guid>, Guid>`.

## Flow requestu

Standardowa sciezka HTTP wyglada tak:

1. Request trafia do kontrolera w `Api/Controllers`.
2. Kontroler tworzy lub przyjmuje `Command`/`Query` z `Application/Features/...` i wysyla go przez `IMediator`.
3. MediatR uruchamia `ValidationBehavior<TRequest,TResponse>`.
4. Jesli istnieje validator FluentValidation dla requestu, bledy koncza sie `ValidationException`.
5. Handler w `Application/Features/...` wykonuje use case.
6. Handler korzysta z interfejsow repozytoriow z `Domain/Repositories` oraz extension methods z
   `Application/Common/MappingExtensions.cs`.
7. Implementacja repozytorium w `Infrastructure/Persistence` operuje na `ApplicationDbContext`.
8. `SaveChangesAsync` automatycznie uzupelnia pola audytowe `Created`, `CreatedBy`, `LastModified`, `LastModifiedBy` dla
   `BaseAuditableEntity`.
9. Wyjatki przechwytuje `Infrastructure/Middleware/ErrorHandlingMiddleware.cs`, ktory zwraca JSON z `type`, `title`,
   `status`, `detail`, `instance` i opcjonalnym `errors`.

## Rejestracja zaleznosci

Rejestracja jest rozbita na warstwy:

- `Api/DependencyInjection.cs`
    - kontrolery,
    - Swagger,
    - schemat JWT Bearer w Swagger UI.
- `Application/DependencyInjection.cs`
    - MediatR,
    - FluentValidation,
    - pipeline `ValidationBehavior`.
- `Infrastructure/DependencyInjection.cs`
    - `ApplicationDbContext` na PostgreSQL,
    - repozytoria,
    - `CurrentUser`,
    - JWT,
    - Identity,
    - authentication/authorization.

`CurrentUser` korzysta z `IHttpContextAccessor`, ktory jest rejestrowany w `Infrastructure/DependencyInjection.cs`.

## Autoryzacja i konto

Endpointy konta:

- `POST /api/v1/account/register`
- `POST /api/v1/account/login`

Logowanie i rejestracja ida przez:

- `Application/Features/AccountFeatures/Commands/RegisterUser.cs`
- `Application/Features/AccountFeatures/Commands/LoginUser.cs`
- `Infrastructure/Identity/IdentityService.cs`
- `Infrastructure/Authentication/JwtTokenGenerator.cs`

Pozostale kontrolery sa zasadniczo chronione `[Authorize]`, wiec wymagaja naglowka:

```text
Authorization: Bearer <access_token>
```

Token zawiera m.in. `sub` jako `Guid` uzytkownika oraz email. `CurrentUser` odczytuje identyfikator z
`ClaimTypes.NameIdentifier`; przy problemach z audytem/OwnerId trzeba sprawdzic zgodnosc claimow generowanych w
`JwtTokenGenerator` z tym, czego oczekuje `CurrentUser`.

## Glowne obszary domeny

Glowne encje w `Domain/Entities`:

- `Book` - prywatna pozycja uzytkownika, z primary title, alternatywnymi tytulami, postepem, linkami, tagami, gatunkami
  i historia progresu.
- `BookTitle` - primary/alternative titles, np. tytuly chinskie i angielskie.
- `Author` i `AuthorName` - globalny autor oraz aliasy/pen name'y.
- `Genre`
- `ContentType` - dawny `Type`, np. Novel/Manga/Manhwa/Manhua/Other.
- `Status`
- `Tag`
- `BookLink`
- `BookProgressHistory`

Wspolne klasy:

- `BaseEntity`
- `BaseAuditableEntity`

Relacje EF sa konfigurowane fluent API w `ApplicationDbContext.OnModelCreating`.

## Feature'y i konwencje

Feature'y sa grupowane wedlug obszaru:

```text
Application/Features/<Area>Features/
  Commands/
  Queries/
  Validators/
```

Obecne obszary:

- `AccountFeatures`
- `BookFeatures`
- `GenreFeatures`
- `StatusFeatures`
- `TypeFeatures`

Typowy wzorzec dla zasobow slownikowych (`Genre`, `Status`, `Type`):

- `Create*Command` sprawdza duplikat po nazwie przez repozytorium i rzuca `EntityAlreadyExistsException`.
- `Update*Command` pobiera encje, rzuca `EntityNotFoundException` przy braku rekordu, aplikuje zmiany i zapisuje.
- `Delete*Command` usuwa po `Guid`.
- Jawne query, np. `GetGenreQuery`, `GetGenreDetailsQuery`, `GetGenreByNameQuery` i `GetGenreDetailsByNameQuery`,
  zwracaja konkretny DTO bez generycznego `TDto`.
- `GetAll*Query` zwraca `PaginatedResult<TDto>` z `Skip`, `Take`, `Total`, `Data`.

Przy dodawaniu nowego zasobu najlepiej skopiowac ten schemat:

1. Encja w `Domain/Entities`.
2. Interfejs repozytorium w `Domain/Repositories`.
3. Implementacja repozytorium w `Infrastructure/Persistence`.
4. `DbSet` i relacje w `ApplicationDbContext`.
5. DTO w `Application/Common/DTOs/<Area>`.
6. Commands, Queries i Validators w `Application/Features/<Area>Features`.
7. Mapowania DTO/encja jako extension methods w `Application/Common/MappingExtensions.cs`.
8. Automatyczna rejestracja handlerow przez MediatR scan w `Application/DependencyInjection.cs`.
9. Rejestracja repozytorium w `Infrastructure/DependencyInjection.cs`.
10. Kontroler w `Api/Controllers`.
11. Migracja EF.

## API endpoints

Aktualne kontrolery:

- `api/v1/account`
    - `POST register`
    - `POST login`
- `api/v1/book`
    - `POST`
    - `GET`
    - `GET {id:guid}`
    - `PUT {id:guid}`
    - `PATCH {id:guid}/progress`
    - `DELETE {id:guid}`
- `api/v1/author`
    - `GET` autocomplete/search
- `api/v1/tag`
    - `GET` autocomplete/search prywatnych tagow uzytkownika
- `api/v1/genre`
    - `POST`
    - `GET`
    - `GET {id:guid}`
    - `GET {id:guid}/details`
    - `GET by-name/{name}`
    - `GET by-name/{name}/details`
    - `PUT {id:guid}`
    - `DELETE {id:guid}`
- `api/v1/status`
- `api/v1/type`

Przyklady pelnych wywolan HTTP, custom query dla ksiazek i flow dodawania ksiazki sa w `docs/http-examples.md`.

Custom query dla `GET /api/v1/book` przyjmuje parametr `query`, np. `title:"Lord of Mysteries" tag:favorite rating>=8`.
Obslugiwane sa filtry `title`, `author`, `tag`, `genre`, `status`, `type` oraz porownania numeryczne dla `rating`,
`priority`, `current/currentChapter` i `total/totalChapters`. Brakujace metadane mozna filtrowac przez wartosc `none`,
np. `author:none`, `rating:none`, `progress:none`, `cover:none` lub `links:none`.

`StatusController` i `TypeController` sa analogiczne do `GenreController`; `TypeController` operuje pod spodem na encji
`ContentType`.

## Obsluga bledow

Globalny middleware:

- `Infrastructure/Middleware/ErrorHandlingMiddleware.cs`
- wlaczany w `Api/Program.cs` przez `app.UseErrorHandlingMiddleware()`.

Aktualnie rozpoznaje m.in.:

- `FluentValidation.ValidationException` -> 400
- `WrongPasswordException` -> 401
- `UsernameTakenException` / `EmailInUseException` -> 409
- `IdentityOperationFailedException` -> 400
- `EntityAlreadyExistsException<Genre|Status|ContentType, Guid>` -> 409
- `EntityNotFoundException<Genre|Status|ContentType|Book, Guid>` -> 404
- `EntityNotFoundException<User, string>` -> 404
- inne wyjatki -> 500

Jesli dodajesz nowe wyjatki domenowe albo nowy typ encji, trzeba dopisac mapowanie w middleware, inaczej klient dostanie

500.

## Testy

Uruchomienie:

```powershell
dotnet test NovelkiBackend.sln -c Release --no-restore
```

Raport coverage z HTML:

```powershell
.\tools\coverage.ps1
```

Skrypt uruchamia testy z `coverage.runsettings`, generuje HTML w `artifacts/coverage/html/index.html` i zapisuje krotki
raport najnizszego pokrycia w `artifacts/coverage/least-covered-files.txt`.

Struktura:

- `Application.UnitTests` - szybkie testy jednostkowe bez bazy: walidatory account, flow handlerow `Genre`, tworzenie
  ksiazki z aliasami tytulow/autorem/tagami/linkami/progresem, update progresu, autocomplete autorow i tagow, mapowania
  DTO.
- `Infrastructure.IntegrationTests` - SQLite in-memory z prawdziwym EF Core: seed slownikow systemowych, audit fields,
  unikalnosc autorow/tagow, multi-tenant scope tagow i ksiazek, kaskady, restrict FK autora, eager loading repozytorium
  ksiazek, wyszukiwanie aliasow autora, paginacja/count gatunkow.

Stan po ostatniej weryfikacji: 238 testow, wszystkie zielone (`164` unit + `74` integration). Kazdy nowy feature
powinien miec test jednostkowy albo integracyjny; dla zachowan zaleznych od EF/relacji uzywamy SQLite in-memory zamiast
providera `InMemory`.

## Znane miejsca wymagajace uwagi

W repo istnieje tez `Analysis.md` z szerszym audytem architektury. Najbardziej praktyczne punkty do sprawdzenia przy
kolejnych pracach:

- Middleware obslugi bledow ma twardo wypisane typy encji, wiec nowe encje wymagaja aktualizacji.
- Testow API/end-to-end jeszcze nie ma; obecny zakres konczy sie na warstwie Application i integracji Infrastructure/EF.

## Szybka mapa plikow

- Start aplikacji: `Api/Program.cs`
- Konfiguracja web/Swagger: `Api/DependencyInjection.cs`
- Kontrolery: `Api/Controllers`
- Rejestracja Application: `Application/DependencyInjection.cs`
- Pipeline walidacji: `Application/ValidationBehavior.cs`
- DTO: `Application/Common/DTOs`
- Komendy/zapytania: `Application/Features`
- Mapowania DTO/encja: `Application/Common/MappingExtensions.cs`
- Encje: `Domain/Entities`
- Repozytoria - kontrakty: `Domain/Repositories`
- DbContext: `Infrastructure/Contexts/ApplicationDbContext.cs`
- Repozytoria - EF: `Infrastructure/Persistence`
- Identity: `Infrastructure/Identity`
- JWT: `Infrastructure/Authentication`
- Aktualny uzytkownik: `Infrastructure/Services/CurrentUser.cs`
- Globalne bledy: `Infrastructure/Middleware/ErrorHandlingMiddleware.cs`
- Migracje: `Infrastructure/Migrations`
