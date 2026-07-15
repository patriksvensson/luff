# Luff - Claude context

> **Working mode: rubber duck, not author.** Do not write, edit, or generate code in this repository. Act as a
> thinking partner and problem solver: reason through the problem, ask clarifying questions, surface trade-offs
> and edge cases, spot bugs, and sketch approaches in prose. When a solution calls for code, describe the
> approach and let the human write it. The human owns all code; your job is to help them think clearly.
> One exception: writing commit messages is allowed.

Luff is a lightweight deployment orchestrator for containerised applications. A ground-up rewrite of the
Drift prototype. **The one sentence:** *CI builds and pushes an image, and Luff pulls it and swaps it into
place, live, across one or more servers, with the old version draining behind it.*

## North stars

1. **Operational simplicity** — as easy to operate as Coolify (one-command install, UI-first, zero-config
   defaults, self-update, few concepts). This is the **tiebreaker for every decision**. "Simplicity" means
   *operator-facing* simplicity, not minimal internal complexity — invisible machinery that earns its keep is
   fine.
2. **Everything testable on a local machine** — no cloud, no second machine. The automated suite is
   **hermetic**: every boundary touching network, disk, or a process is faked. End-to-end behaviour is
   verified by running the real compose stack locally by hand.
3. **Every executable is one self-contained single file** — control plane, agent, and CLI each ship as one
   file with the runtime bundled (plain self-contained single-file publish; **not** Native AOT). Install,
   upgrade, and agent self-update all reduce to moving one file.

## Non-negotiables

- **Luff orchestrates; CI builds.** Luff never builds images. Do not add image-build logic.
- **Zero-downtime by default** — every **web** app deploys as a blue/green container swap. Two kinds are
  exempt and deploy by in-place recreate (stable compose project + stable bare-name network alias), accepting
  brief downtime on an image change: **internal services** (`Kind = Internal`, e.g. a database) are stateful,
  and **direct apps** (`Kind = Direct`) publish a fixed host port that cannot follow a container across a live
  swap. These are the sanctioned exceptions to zero-downtime.
- **API-first.** All capability lives in the application core; the REST API, the Blazor UI, and the CLI are
  projections of it. The CLI is the API's HTTP dogfooder.
- **Single-agent is a degenerate case of multi-agent** — one box and a fleet are the same architecture.
- **Explicit over magic** — config is set via CLI/API and stored in the control-plane DB. No GitOps file
  watching, no registry-webhook parsing.
- **Design for tomorrow, build for today** — environments/replicas/multi-tenancy are out of scope now but
  must not demand a rewrite later.

## Architecture

Four roles. On one host they co-locate; on many hosts the control plane is its own node — nothing about the
architecture changes between those cases.

- **Control plane** — the only authority and only inbound listener. REST API (CLI + webhooks), Blazor
  dashboard, owns the database (all state, all secrets), runs the deploy engine, hosts the agent-link
  listener. **Has zero host access** — no Docker socket, never touches Caddy.
- **Agent** — one per host. Does the Docker work and manages its co-located Caddy. **Dials *out*** to the
  control plane and holds one persistent gRPC stream open; hosts no inbound management port. The single
  privileged component.
- **CLI (`luff`)** — a thin client of the REST API. Never touches Docker or an agent directly. *Planned; not
  yet implemented (no CLI project in the solution today).*
- **Reverse proxy (Caddy, per agent)** — fronts that host's app containers; reconfigured by the agent on
  control-plane instruction. The control plane never talks to a proxy directly.

## Deploy pipeline

Triggered by a webhook (CI: app + image tag) or `luff deploy`. Image tag is grammar-validated. Fans out
across every attached agent, **rolling oldest-first, fail-fast** (first failing agent stops the roll; already
-updated agents keep the new tag, causing tag drift). Per agent, blue/green:

1. Pull the new image (with the matching registry's stored creds, if any).
2. Start a new "green" container, injecting resolved env vars + volume mounts.
3. Health-check green (docker HEALTHCHECK / HTTP probe / **agent-side TCP probe** / none, per app config),
   then a **stabilization** check: the container must stay up and not crash-loop. `docker compose up --wait`
   alone only proves "running", and a restart policy masks a crash loop — stabilization (via `docker inspect`)
   is what makes "healthy" honest, failing the deploy with the container's tail logs otherwise.
4. Atomically point the agent's Caddy route at green.
5. Drain, stop, and remove the old "blue" container; prune orphans.
6. Persist per-agent runtime state + app-level current/previous tags.

While connected, each agent also **pushes periodic per-app runtime health** (from a `docker ps` sweep) up the
link; the control plane stores it on the AppAgent so the dashboard reflects a container that crashes *after* a
deploy. Apps can be **stopped/started** manually (Operator): the control plane sets the desired run-state and
pushes `StopApp`/`StartApp` down the link (the agent does `docker stop`/`start`, keeping containers + volumes);
a stopped app refuses **automatic** deploys (CI webhook) and is not restarted on reconnect, but a **manual**
deploy or rollback clears the stopped state and rolls out, bringing it back up on the chosen version.

Concurrent deploys **queue per app and coalesce to the latest tag**. Rollback redeploys the previous tag
through this exact pipeline. Blue/green renders each release as its own compose project
(`luff-<app>-<deployment>`); the control plane renders the compose, the agent executes it. An **internal
service** skips steps 3-5's route swap: it deploys to a **stable** project (`luff-<app>`) so `docker compose
up` recreates in place, and carries **no domain**, which is exactly how the agent knows to skip the Caddy
route. Sibling apps reach it over the shared network by its **bare name** (`postgres:5432`); reachability is
same-host only (no overlay network), so an app and its datastore must attach to the same agent.

## Domain model

- **App** — the unit of deployment: a `kind` (`Web` | `Internal` | `Direct`), `image`, `internalPort`,
  encrypted write-only `envVars`, `volumes`, a health check (`Docker`/`Http`/`Tcp`/`None`), a `stopped`
  desired run-state, current/previous tags. A **web** app also has a `domain` and `tlsMode` (route
  fronted by Caddy). An **internal** service has **no domain** (not internet-exposed), reachable by sibling
  apps on the same host under its bare name; health limited to `Docker`/`Tcp`/`None` (no HTTP probe), and it
  defaults to the agent-side `Tcp` readiness probe. A **direct** app also has **no domain** and is not fronted
  by Caddy; it publishes one or more loopback-bound host ports (`127.0.0.1:host:container`, Admin-managed on a
  dedicated `/apps/{name}/ports` endpoint) so it is reached directly on `host:port` (front with `tailscale
  serve` or a LAN bind for real exposure). Like an internal service it deploys in place, and its health is
  limited to `Docker`/`Tcp`/`None`. Runs nowhere until attached to an agent.
- **Agent** — a registered host; hashed per-agent enrollment token; status pending | connected | disconnected.
- **AppAgent** - the app-agent attachment; carries the per-agent running tag (may lag, causing tag drift) plus
  the last agent-reported runtime health (healthy | unhealthy | starting | stopped | unknown). No per-agent
  config overrides.
- **Deployment** — one rollout of a tag; ordered state pipeline; records failing agent on failure.
- **WebhookToken / Registry / User (username PK; Admin | Operator; required unique `email`, optional
  `firstName`/`lastName`; optional TOTP 2FA — `twoFactorEnabled` + encrypted `twoFactorSecret`) / RecoveryCode
  (hashed, single-use 2FA backup codes) / ServerSettings** (front-door domain singleton).
- **NotificationChannel** — an Admin-managed alert target (`Discord` | `Generic`; webhook URL stored encrypted,
  never returned). An event — deploy failed, deploy succeeded, agent connected, agent disconnected, app went
  unhealthy (on the transition into unhealthy only), app manually started, app manually stopped — is formatted
  per type (Discord colour-coded embeds with a per-event icon / Generic structured JSON) and POSTed
  **fire-and-forget** through a singleton background `NotificationDispatcher`, so a slow/down endpoint never
  blocks or fails the triggering operation. Agent connect/disconnect fire on every link transition (no
  debounce; a reconnect or control-plane restart alerts).

## Security posture

- Control plane is the sole authority (DB, secrets, authz, business logic). Agents are low-trust: dial out,
  authenticate with a hashed enrollment token.
- Passwords PBKDF2-SHA256 (per-user salt, constant-time, enumeration-safe login). Short-lived JWT access +
  rotating single-use hashed refresh tokens. Webhook/enrollment tokens CSPRNG, stored hashed only.
- **First-run setup wizard** — with no accounts yet, the login page and an `[AllowAnonymous]` `/setup` page hand
  off to a one-time wizard where the operator picks the first admin's username/password/email (`POST
  /api/v1/setup`, gated to an empty user table inside a transaction; closes once any account exists). The
  config seed (`Auth:InitialAdmin`) is the opt-in headless alternative — username + password + a valid
  **required** email, or it fails fast at startup; blank by default so a fresh install lands on the wizard.
  No default `admin`/`changeme`.
- **Optional TOTP two-factor** (RFC 6238, hand-rolled over the BCL; QR via QRCoder). Per-user opt-in from
  Settings; the shared secret is encrypted at rest, backup codes are stored hash-only (single-use), an Admin
  can reset a locked-out user. Login is two-step on both surfaces — the API returns a short-lived signed
  challenge token, the dashboard hands off via a separate short-lived `TwoFactorPending` cookie scheme.
- **Roles:** Operator = app lifecycle (create/deploy/rollback/env/logs/status, attach/detach apps to machines).
  Admin = host- or credential-bearing ops (volumes, registries, agent enroll/remove, users, set-domain). Gate any
  new host-reaching or credential-bearing endpoint behind Admin.
- Secrets (env values, registry passwords, JWT key) encrypted / kept in `keys/` beside the DB. **Back up the
  DB + the keys.** The API never returns plaintext.
- Front door serves **self-signed HTTPS by default** (Caddy `tls internal`); real domains get Let's Encrypt.
  Volume sources denylisted; image tags grammar-validated; the agent keeps a validation backstop on the
  rendered compose (rejects `privileged`/host-mounts/host-net/etc.).

## Conventions & how we work

- **Stack:** C# / .NET 10. ASP.NET Core minimal API (no MVC). Blazor Web App (Interactive Server) dashboard
  hosted in the control plane, consuming the core in-process (cookie auth; JWT/refresh for the API). EF Core +
  SQLite (Postgres-later via provider swap). gRPC bidi agent link, proto-first (h2c). Caddy proxy driven via
  its admin API. Agent shells out to `docker compose`. Planned CLI: Spectre.Console over an NSwag-generated
  client. Secrets via Data Protection.
- **Layering (one-way: adapters -> commands -> handlers -> services).** Only **adapters** (REST endpoints,
  webhook, Blazor components, the agent-link service) hold a sender and translate protocol -> command -> Send.
  **Handlers** are one per operation (API verb, UI action, *and* link event), each a complete use case; they
  never hold a sender or another handler. **Services** are machinery below handlers. Vertical slices under
  `Features/<Area>/`. Hand-rolled `ISender`/`IRequestHandler` seam in `Infrastructure/Messaging` — no mediator
  library. Typed domain exceptions map to `problem+json` via one `IExceptionHandler`.
- **Tests:** hermetic — fake every network/disk/process boundary with **hand-written `Fake*`** behind xUnit
  **state-holding fixtures**. **No mocking frameworks.** EF is tested against real **in-memory SQLite** (the
  one blessed non-faked boundary). `// Given`/`// When`/`// Then`, `Pascal_Snake_Case` names, the When-var
  named `result`, `Record.Exception` (not `Should.Throw`), Shouldly. Tests co-located beside `src/` (no
  `tests/` dir). No integration/e2e automation — that last mile is a manual smoke test on the real stack.
- **Code style:** expression-bodied one-line properties/members OK, methods block-bodied; no XML doc comments;
  no em-dashes; one domain type per file; no redundant named args.
- **OpenAPI:** code-first, emitted to `src/openapi.json` at build and committed; CI drift guard
  (`git diff --exit-code src/openapi.json`). operationIds `Area_Verb`, area tags, `ProblemDetails` errors,
  bearer default. Live logs stream as NDJSON.
- **Commits:** never add `Co-Authored-By` or any Claude attribution.
- **Working with the user:** **propose the approach and wait for an explicit go-ahead before writing code**,
  and **ask about every non-obvious choice** — discuss trade-offs first rather than making unilateral calls.
  Keep this file current as part of each change.

## Build / test / run

```
dotnet tool restore                       # dotnet-ef, verify.tool
dotnet run build.cs                        # Cake pipeline (add `--target <name>`, `--lint`)
dotnet build src/Luff.slnx -c Release      # builds + emits src/openapi.json
dotnet test src/Luff.slnx                  # the hermetic suite
cd eng/server && docker compose up -d --build     # run the real stack locally (CP + agent + Caddy)
```

## Layout

- `src/Luff.Server` — control plane: REST API, Blazor dashboard, deploy engine, agent-link listener,
  EF/SQLite persistence.
- `src/Luff.Agent` — the dial-out agent: `docker compose` runner, Caddy client, log streamer, compose
  validation backstop.
- `src/Luff.Protobuf` — gRPC `link.proto` + shared deploy-phase contracts.
- `src/Luff.Server.Tests`, `src/Luff.Agent.Tests`, `src/Luff.Testing` — hermetic tests + shared fakes.
- `src/openapi.json` — the committed REST contract.
- `eng/server` — the control-plane compose stack (`compose.yaml` + dev `compose.override.yaml` +
  `.env.example`), `install.sh`/`uninstall.sh`/`teardown.sh`; `eng/agent` — the agent stack (`compose.yaml`, `compose.frontdoor.yaml`,
  `.env.example`) and `agent-install.sh` (`--front-door` co-locates Caddy). Releases ship these as `luff-server-docker.tar.gz` and
  `luff-agent-docker.tar.gz`.
- `build.cs` — the single Cake build/CI/release pipeline.
