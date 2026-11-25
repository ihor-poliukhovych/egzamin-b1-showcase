# Running locally

## Option 1: Docker (one command)

Requirements: Docker with Compose.

```
docker compose up --build
```

Open http://localhost:8080 and use the **Dev login** form on the landing page. Then pick an interface
language and a role (student or teacher).

What runs:

- PostgreSQL 17; the schema is created by EF Core migrations on startup.
- The app in Development mode: Dev login on, Google sign-in off, AI review off.

## Option 2: .NET + Node (no Docker)

Requirements: .NET SDK 10, Node 22+.

```
dotnet run --project src/Api --launch-profile sqlite
```

```
npm ci --prefix src/Web
npm run dev --prefix src/Web
```

The API listens on http://localhost:5080 with a SQLite file `b1.db`. Swagger is at `/swagger`. The front end
runs at http://localhost:5173 and proxies API calls to port 5080. Dev login is on in Development
(`appsettings.Development.json`).

## Exam content

The repository ships no exam sets, so the exam list is empty after a fresh start. The dashboard, error bank,
teacher groups and knowledge base work without content.

To add your own sets, write them in the format described in
[content/arkusze/README.md](../content/arkusze/README.md) and load them:

```
dotnet run --project src/Api -- seed-arkusze --from <folder with *.json>
```

With Docker, put the files into `./content-local/arkusze` (mounted read-only at `/data`) and run:

```
docker compose run --rm app seed-arkusze --from /data/arkusze
```

Audio (`<code>.mp3`) and images (`media/zadania/*`, `media/zdjecia/*`) are served from the content folder
(`Content:LocalPath`; `/data` in Docker).

## Turning on AI review

AI review is off by default (`Review:Provider=none`). In that state, finishing Pisanie or Mówienie returns a
clear "review failed" message and the draft stays saved.

Configuration is read from environment variables with the prefix `EGZAMINB1_` (a double underscore stands for
`:`). Keys never go into files in the repository.

**Claude API.** Run in PowerShell:

```powershell
$env:EGZAMINB1_Review__Provider = "api"
$env:EGZAMINB1_Review__ApiKey = "<your Anthropic API key>"
```

The model is `Review:ApiModel` (default `claude-opus-5-5`). Only Sonnet has been evaluated, see
[evaluation.md](evaluation.md).

**Claude CLI** (local development only): set `EGZAMINB1_Review__Provider=cli`. It runs `claude -p` with your
own login. `Review:Model` (default `sonnet`) is the CLI model alias.

**Limits:** `Review:DailyLimit` (reviews per user per day, default 50), `Review:DailyDialogTurns` (default 200),
`Review:TimeoutSeconds` (default 180).

## Google sign-in (optional)

1. In Google Cloud Console create an OAuth client of type *Web application*.
2. Add the redirect URI `http://localhost:5080/signin-google` (and `http://localhost:5173/signin-google` when
   using the Vite dev server).
3. Set the credentials as environment variables:

   ```powershell
   $env:EGZAMINB1_Auth__Google__ClientId = "<client id>"
   $env:EGZAMINB1_Auth__Google__ClientSecret = "<client secret>"
   ```

Without these settings `/auth/login` returns 501 and only Dev login is available (in Development).
`Auth:TeacherEmails` lists emails that get the Teacher role on first sign-in.

## Tests

```
dotnet test
npm test --prefix src/Web
```

The .NET tests need no database server, network or API key: they use SQLite (in memory or a temporary file)
and the `none` review provider.

## Configuration reference

| Key | Default | Meaning |
|---|---|---|
| `Database:Provider` | `postgres` | `postgres` or `sqlite` |
| `ConnectionStrings:Default` | local Postgres `b1/b1` | Connection string |
| `Database:MigrateOnStartup` | `true` | Apply migrations (Postgres) or create the schema (SQLite) on start |
| `Auth:DevLogin` | `false` (`true` in Development) | Enables `/auth/dev-login`; works only in Development |
| `Auth:Google:ClientId`, `Auth:Google:ClientSecret` | — | Google sign-in |
| `Auth:TeacherEmails` | `[]` | Emails promoted to Teacher on sign-in |
| `Review:Provider` | `none` | `none`, `api` or `cli` |
| `Review:ApiKey` | — | Anthropic API key (environment variable only) |
| `Review:ApiModel` | `claude-opus-5-5` | Model for the API provider |
| `Review:Model` | `sonnet` | Model alias for the CLI provider |
| `Review:DailyLimit`, `Review:DailyDialogTurns` | `50`, `200` | Per-user daily AI caps |
| `Content:LocalPath` | — | Folder with audio and `media/` images |
| `RateLimits:GlobalPerMinute`, `RateLimits:AiPerMinute`, `RateLimits:FeedbackPerHour` | see `RateLimitSetup` | Rate limits |
| `ForwardedHeaders:KnownProxies`, `ForwardedHeaders:KnownNetworks` | empty | Proxies whose `X-Forwarded-*` headers are trusted |
| `Cors:Origins` | `http://localhost:5173` | Allowed origins for the dev front end |
| `Site:Name`, `Site:OwnerName`, `Site:ContactEmail` | placeholders | Shown on the legal pages |
