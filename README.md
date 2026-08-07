# Xpense

Personal expense tracking. The .NET API and its browser client live here together.

| Tree | What it is | Read next |
| --- | --- | --- |
| [`api/`](api/) | ASP.NET Core API, Postgres, a notifications worker | [`api/README.md`](api/README.md) |
| [`web/`](web/) | React 19 and MUI 9 on Vite | [`web/README.md`](web/README.md) |

Conventions for both live in [`AGENTS.md`](AGENTS.md). Architecture decisions are in
[`api/docs/adr/`](api/docs/adr/), and the shared vocabulary is in
[`api/UBIQUITOUS_LANGUAGE.md`](api/UBIQUITOUS_LANGUAGE.md).

## Running it

The API and its dependencies run in containers:

```bash
cd api
cp .env.example .env
docker compose up -d --build
```

That serves the API on `http://localhost:4000`, with Postgres, a one-shot migrations container and
the notifications worker behind it. Then the client:

```bash
cd web
npm install
npm run dev
```

The client is served on `http://localhost:5173` and expects the API on port 4000.

## The client does not talk to the API yet

Every endpoint `web/` calls was removed when the API reset to its v1 contract, so none of its
requests currently resolve. Migrating those six call sites is the next piece of work.

## CI

`api/**` and `web/**` have separate workflows and each runs only when its own tree changes. The API
job builds, tests, reports coverage and smoke-tests the whole compose stack; the web job installs
from the lockfile, builds, and audits.
