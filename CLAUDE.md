# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

PancrasERP: a Vietnamese-language ERP (sanitary ware & furniture trading) on .NET 10, EF Core 10 (Npgsql/PostgreSQL, snake_case naming), Razor Pages with cookie auth. The UI text is Vietnamese; feature URLs are English (`/inventory`, `/purchasing`, `/employees`, `/catalog`, `/suppliers`). Modules not built yet (`/customers`, `/sales`, `/quotes`, `/receivables`, `/warranty`, `/locations`, `/audit`, ...) route to a shared `ComingSoon` page (see `ComingSoonModel.Routes`).

## Commands

Solution file is `Pancras.slnx`.

```bash
dotnet build Pancras.slnx --warnaserror            # zero warnings is required (Nullable on)
dotnet test Pancras.slnx --warnaserror --nologo    # all tests
dotnet test tests/Pancras.Domain.Tests --filter "FullyQualifiedName~SomeTest"   # single test/class
dotnet run --project src/Pancras.Web --launch-profile http   # http://localhost:5010
```

- Run `dotnet test` with one project path per invocation (passing two csproj paths fails with MSB1008).
- The connection string (`ConnectionStrings:DefaultConnection`) lives in user-secrets (id `pancras-web-7c1a9e2b`), not in git. The app throws on startup without it, and only reads secrets when `ASPNETCORE_ENVIRONMENT=Development` — use the `http` launch profile, not `--no-launch-profile`.
- A running `Pancras.Web.exe` locks build output; stop the process on port 5010 before rebuilding.
- EF migrations: use Infrastructure as both project and startup project (Web's `app.Run()` hangs design-time tooling):
  `dotnet ef migrations add <Name> --project src/Pancras.Infrastructure --startup-project src/Pancras.Infrastructure`
  (`PancrasDbContextFactory` supplies the design-time context.)

## Architecture

Clean Architecture, dependencies point inward: `Domain` (no deps) ← `Application` (Domain only) ← `Infrastructure` (EF, repositories, migrations, PBKDF2 hasher) ← `Web` (Razor Pages). Tests: `Domain.Tests`, `Application.Tests` (use hand-written fakes such as `FakeEmployeeStore`), `Web.Tests` (page tests with test doubles for handlers/repositories — they must not depend on a real database).

Conventions that span files:
- **One use case = one handler class** in `Application/<Module>/`, named `<Action><Entity>CommandHandler` (commands) or `<Verb><Entity>Handler` (queries). Every handler must be registered by hand in `src/Pancras.Web/Program.cs` (scoped), along with repositories (`I*Repository` in Application, implemented in `Infrastructure/Persistence`).
- **Results, not exceptions, for business outcomes.** Handlers return `Result`-style values with per-module error enums defined in `Application/Common/Result.cs` (e.g. `LoginError`, `EmployeeError`, `SupplierError`). Domain/Application never format user-facing text, money, dates or HTML; the Razor PageModel maps error codes to Vietnamese messages. Page handlers contain no business try/catch; unhandled exceptions go through the `UseExceptionHandler` in `Program.cs`.
- **Web never calls Infrastructure** except for DI composition in `Program.cs`. `.cshtml` files only render; PageModels call Application handlers. Authorization: a fallback policy requires an authenticated user on every page; role checks (Admin, Giám đốc, Kế toán, Thủ kho, Trưởng Showroom/Kho, Sales, CSKH) are enforced both in the UI (buttons hidden by role) and inside handlers. `EmployeePrincipalValidator` revalidates the cookie principal on each request.
- Read queries use `AsNoTracking()`; avoid N+1. All I/O is `async` with `Async` suffix; services inject `ILogger<T>`; never log passwords/tokens/PII.
- No seeded accounts in source (the old `AdminEmployeeSeed` was removed) — do not reintroduce credentials in code, specs or tracked files.

Domain rules that are easy to break (details in module code/tests):
- Inventory: `StockBalance.Version` is a concurrency token (losing writes are rejected, not retried); stock never goes below 0; creating a transfer slip and shipping it happen in one transaction. Cost price on receipt is a weighted average over total on-hand across all locations, rounded to đồng with `MidpointRounding.AwayFromZero`; adjustments and disposals do not change cost. `FobUsd` on a purchase line is the line total in USD, not unit price; VAT is excluded from landed cost.
- Employee codes are generated server-side from the name (e.g. `AnNK`, with a numeric suffix on collision).
- Purchasing is restricted to Admin, Giám đốc, Kế toán. A purchasing page test forbids the literal substring `Nhập kho` on the receive button (current text: "Nhập Kho & Cập Nhật Giá Vốn" — keep it that way).

## UI rules (from `.cursorrules` / `.cursor/rules`)

- Razor UI mirrors the original React demo: Tailwind-style classes, slate/sky palette, fixed sidebar groups order, VND shown as `vi-VN` grouping plus `đ` suffix (never "VND/VNĐ"). Don't restyle to default Bootstrap or add new dashboards.
- Create/edit/allocate actions open a `<dialog>` via `showModal()` (not the bare `open` attribute). Modal frame is a max-height flex column with `overflow-hidden`; the header (title + close button) is `shrink-0` outside the scroll area; only the form body scrolls (`min-h-0 flex-1 overflow-y-auto`).
- Don't invent business rules for specs marked `[NEEDS CLARIFICATION]`; specs live in `specs/` (gitignored, along with `.cursor/`, `.specify/`, `ARCHITECTURE.md`). `.cursor/handoff.md` has the latest session handoff if present.
