# DataWedge Scanner POC

A .NET 8 / ASP.NET Core proof of concept that receives barcode scans from a Zebra MC93xx over
DataWedge's **IP Output (TCP)** plugin, matches the barcode against an `Item` in PostgreSQL,
advances its status (`Ready -> Loaded`), records a `ScanEvent` audit row, and shows both on a
live web dashboard.

```
Zebra MC93xx -> DataWedge IP Output (TCP) -> TcpScannerListenerService -> BarcodeScanService
              -> PostgreSQL (Item + ScanEvent) -> Razor Pages dashboard (+ SignalR live update)
```

---

## 1. Project Structure

```
DataWedgeScanner/
  DataWedgeScanner.sln
  src/DataWedgeScanner.Web/
    Program.cs                     Composition root: DI, middleware, startup migrate+seed
    appsettings.json                ConnectionStrings, Scanner:TcpPort, Kestrel LAN binding
    appsettings.Development.json
    Data/
      AppDbContext.cs               EF Core DbContext + Fluent API model configuration
      DbSeeder.cs                   Idempotent demo data (10 items)
    Models/
      Item.cs, ItemStatus.cs
      ScanEvent.cs, ScanResultStatus.cs
    Services/
      IBarcodeScanService.cs / BarcodeScanService.cs   The one place scan business logic lives
      ScanProcessingResult.cs       Result DTO returned to callers + broadcast to the UI
      ItemStatusWorkflow.cs         Ready -> Loaded state machine, isolated and extensible
      IScanNotifier.cs / SignalRScanNotifier.cs         Live-update broadcast, decoupled from SignalR types
    Scanner/
      ScannerOptions.cs             Binds "Scanner:TcpPort" from appsettings.json
      BarcodeFrameReader.cs         TCP byte-stream -> barcode string framing (swappable)
      TcpScannerListenerService.cs  BackgroundService: networking only, no business logic
      ScannerListenerStatus.cs      Shared status flag the dashboard reads
    Hubs/
      ScanHub.cs                    SignalR hub (server -> client push only)
    Pages/
      Index.cshtml(.cs)             Dashboard: status, items, recent scans, manual scan form
      Error.cshtml(.cs)
      Shared/_Layout.cshtml
    wwwroot/
      css/site.css, js/site.js      Manual scan form (plain POST) + SignalR live update client
  tests/DataWedgeScanner.Tests/
    BarcodeScanServiceTests.cs      4 required scenarios, EF Core InMemory, no networking
  README.md                        This file
```

---

## 2. Architecture Explanation

The flow follows the separation of concerns requested:

- **`TcpScannerListenerService`** (networking only) accepts TCP connections, reads bytes via
  `IBarcodeFrameReader`, and passes each decoded barcode string to `IBarcodeScanService`. It never
  touches EF Core or SQL directly.
- **`IBarcodeFrameReader` / `LineDelimitedBarcodeFrameReader`** owns *only* the question of how to
  split a raw byte stream into barcode strings (currently: newline-delimited, NUL/whitespace
  trimmed). This is its own interface specifically so it can be swapped for a different framing
  scheme later without touching the listener -- see section 4.
- **`BarcodeScanService`** (business logic) normalizes the barcode, looks it up, applies
  `ItemStatusWorkflow`, persists the `Item` update and a `ScanEvent` in one atomic
  `SaveChangesAsync`, and notifies the dashboard via `IScanNotifier`. This is the **only** place
  scan logic exists -- both the TCP listener and the manual "Simulate Scan" button call this same
  method.
- **`ItemStatusWorkflow`** is a small, separate state machine (`Ready -> Loaded`, `Loaded ->
  Loaded/AlreadyLoaded`) kept out of `BarcodeScanService` so adding a future transition (e.g.
  `Loaded -> InTransit`) means adding one `switch` case, not restructuring the service.
- **`AppDbContext`** (persistence) is the only place EF Core Fluent API configuration lives:
  unique index on `Item.Barcode`, enum-to-string conversions, and the nullable
  `ScanEvent.ItemId` relationship.
- **Web UI** (`Pages/Index.cshtml`) only displays data and posts to the same
  `IBarcodeScanService` -- no duplicate logic.

Dependency injection lifetimes: `AppDbContext`/`BarcodeScanService` are **scoped**;
`TcpScannerListenerService` is a **singleton** `BackgroundService`, so it resolves a fresh DI scope
(`IServiceScopeFactory.CreateAsyncScope()`) per received barcode rather than holding a long-lived
`AppDbContext`.

---

## 3. PostgreSQL Configuration Location

`src/DataWedgeScanner.Web/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "PASTE_CONNECTION_STRING_HERE"
}
```

Paste your real connection string there (or, better for anything beyond local testing, into
`appsettings.Development.json`, a user-secret, or the `ConnectionStrings__DefaultConnection`
environment variable -- all of which override `appsettings.json` and are not committed to source
control). No credentials are hardcoded anywhere else in the codebase.

Example format:
```
Host=192.168.1.20;Port=5432;Database=datawedge_scanner;Username=scanner_app;Password=your_password
```

---

## 4. TCP Listener Implementation

`TcpScannerListenerService` (a `BackgroundService`, started automatically by the ASP.NET Core
host) does:

1. `new TcpListener(IPAddress.Any, port).Start()` -- binds to all interfaces so the Zebra device
   can reach it over the LAN.
2. Loops `AcceptTcpClientAsync(stoppingToken)` forever, so it accepts multiple connections over
   the app's lifetime (including reconnects).
3. Each accepted client is handled on its own fire-and-forget task, so one slow/misbehaving
   connection never blocks the accept loop.
4. Bytes are decoded into barcode strings by `IBarcodeFrameReader` (default:
   `LineDelimitedBarcodeFrameReader`, splitting on CR/LF, trimming NUL/whitespace, skipping empty
   frames, flushing any trailing unterminated data when the connection closes).
5. Each barcode is logged, then handed to `IBarcodeScanService.ProcessScanAsync(barcode, sourceIp)`
   via a fresh DI scope.

**If the real MC93xx sends delimiters differently** (e.g. no trailing CR/LF, or a different
suffix), you do not need to touch the listener or the business logic. Write a new
`IBarcodeFrameReader` implementation and change one line in `Program.cs`:

```csharp
builder.Services.AddSingleton<IBarcodeFrameReader, YourCustomFrameReader>();
```

Error handling: per-client `IOException`/`SocketException` are caught and logged without
crashing the listener; a database error while processing one barcode is caught in
`TcpScannerListenerService.ProcessBarcodeAsync` and logged, without closing the client connection;
a failure to bind the port at all is caught in `ExecuteAsync` and logged, and the rest of the web
app still starts (so you can see the error on the dashboard's Scanner Status panel).

---

## 5. Zebra DataWedge Configuration Values

On the MC93xx, in the DataWedge profile for this app:

- **Output plugin:** IP Output (disable Keystroke/Intent output for this profile, or make sure IP
  Output is what actually reaches the network -- having multiple output plugins active at once is
  a common source of confusion when testing).
- **Protocol:** TCP
- **IP Address:** the LAN IP of the PC/server running this app, e.g. `192.168.1.50`
- **Port:** `58627` (matches `Scanner:TcpPort` in `appsettings.json`)
- **Remote Wedge:** Disabled (this app is a one-way TCP receiver, not a Remote Wedge peer)

Find your PC's LAN IP with `ipconfig` (Windows) or `ip addr` / `ifconfig` (Linux/macOS). If it's
`192.168.1.50`, the DataWedge profile should point at `192.168.1.50:58627`.

---

## 6. How Barcode Mapping Works

`BarcodeScanService.ProcessScanAsync`:
1. Normalizes the incoming string (trim whitespace and stray `\0`/`\r`/`\n` -- defense in depth,
   independent of what the TCP frame reader already stripped).
2. Looks up `Item` with `SingleOrDefaultAsync(i => i.Barcode == normalized)` -- an **exact**,
   case-sensitive match against the unique-indexed `Barcode` column.
3. No match -> `ScanResultStatus.UnknownBarcode`, `ScanEvent.ItemId` left null.

---

## 7. How Item Status Updates Work

`ItemStatusWorkflow.Evaluate(currentStatus)`:
- `Ready` -> `Loaded`, result `Success`.
- `Loaded` -> stays `Loaded`, result `AlreadyLoaded` (no incorrect duplicate transition).
- Any other status (`Pending`, `InTransit`, `Delivered`, `Cancelled` -- not reachable from seed
  data today) -> left unchanged, also reported as `AlreadyLoaded` for now. This is the documented
  extension point: add a `case` in `ItemStatusWorkflow.Evaluate` for the next transition you need
  (e.g. `Loaded -> InTransit`) without touching `BarcodeScanService`.

The `Item.Status` update and the new `ScanEvent` row are written in a single `SaveChangesAsync`
call so they commit together.

---

## 8. How Seeding Works

`DbSeeder.SeedAsync` runs once at startup (from `Program.cs`, right after migrations are applied).
It checks `await context.Items.AnyAsync()` first and does nothing if the table already has rows --
safe to restart the app or redeploy without creating duplicates. It seeds exactly the 10 items
below, all starting `Ready`:

| Barcode | Name | Quantity |
|---|---|---|
| LOAD-000001 | Medical Supplies | 20 |
| LOAD-000002 | Steel Pipes | 50 |
| LOAD-000003 | Cement Bags | 100 |
| LOAD-000004 | Electrical Equipment | 12 |
| LOAD-000005 | Warehouse Tools | 30 |
| LOAD-000006 | Bottled Water | 200 |
| LOAD-000007 | Office Furniture | 15 |
| LOAD-000008 | Automotive Parts | 75 |
| LOAD-000009 | Packaged Foods | 60 |
| LOAD-000010 | Safety Equipment | 40 |

---

## 9. How to Manually Simulate a Scan

On the dashboard's **Manual Test Input** panel, type a barcode (e.g. `LOAD-000001`) and click
**Simulate Scan**. This posts to `IndexModel.OnPostSimulateScanAsync`, which calls
`IBarcodeScanService.ProcessScanAsync(barcode, sourceIp: null)` -- the *exact* method the TCP
listener calls, just with `sourceIp` left null so manual scans are distinguishable from real
network scans in the `ScanEvent.SourceIp` column. Use this to validate the whole
database/business-logic flow before your MC93xx is configured.

---

## 10. How to Test Using the Real MC93xx

1. Get this app running and reachable on your LAN (see section 11/13).
2. On the MC93xx, configure a DataWedge profile as described in section 5, pointed at your PC's
   LAN IP and port 58627.
3. Open the dashboard from a browser on the same network: `http://<your-pc-ip>:5000`.
4. Scan a barcode that matches a seeded item, e.g. print or display `LOAD-000001` as a barcode and
   scan it.
5. Watch the **Scanner Status** panel (should already show "Listening"), then the **Recent Scans**
   table for a new row, and the **Items** table for `LOAD-000001` flipping from `Ready` to
   `Loaded`. With SignalR connected, this happens live without a manual refresh; otherwise, reload
   the page.
6. Scan the same barcode again -- it should show `AlreadyLoaded` in Recent Scans and the item
   should stay `Loaded`.
7. Scan a barcode that doesn't match any seeded item -- it should show `UnknownBarcode`.

---

## 11. Exact Commands to Run the Project

From the repository root (where `DataWedgeScanner.sln` is), on a machine with the .NET 8 SDK and
network access to NuGet:

```bash
# Restore and build everything
dotnet restore
dotnet build

# Run the web app (also starts the TCP listener automatically)
dotnet run --project src/DataWedgeScanner.Web
```

By default it listens on `http://0.0.0.0:5000` for the web UI (configured in
`appsettings.json` under `Kestrel:Endpoints:Http:Url` -- see section 13) and TCP port `58627` for
scans. Override the web port with `--urls` if needed:

```bash
dotnet run --project src/DataWedgeScanner.Web --urls "http://0.0.0.0:8080"
```

Run the unit tests:

```bash
dotnet test
```

---

## 12. Exact EF Core Migration Commands

**This sandbox could not run these commands** -- see "Known assumptions and limitations" below for
why. Run them yourself once you've pasted a real connection string into `appsettings.json`:

```bash
# One-time: install the EF Core CLI tool if you don't already have it
dotnet tool install --global dotnet-ef

# From the repository root:
dotnet ef migrations add InitialCreate --project src/DataWedgeScanner.Web
dotnet ef database update --project src/DataWedgeScanner.Web
```

`dotnet ef migrations add InitialCreate` generates the migration files (table/index/FK creation)
from `AppDbContext`'s model into `src/DataWedgeScanner.Web/Migrations/`.
`dotnet ef database update` applies them to the database named in your connection string.

You don't strictly have to run `database update` yourself: `Program.cs` calls
`db.Database.MigrateAsync()` automatically on startup, so `dotnet run` will apply any pending
migrations for you. You do still need to run `migrations add InitialCreate` once, since that step
requires the EF Core design-time tooling and generates files that need to exist before the app can
apply them.

---

## 13. Firewall / Network Configuration

- The app binds the web UI to `0.0.0.0:5000` and the TCP scanner listener to `0.0.0.0:58627`
  (both via `IPAddress.Any` / the Kestrel config in `appsettings.json`), so both are reachable from
  other devices on the LAN, not just `localhost`.
- **Windows:** allow inbound TCP on both ports, e.g.:
  ```powershell
  New-NetFirewallRule -DisplayName "DataWedge Scanner TCP" -Direction Inbound -Protocol TCP -LocalPort 58627 -Action Allow
  New-NetFirewallRule -DisplayName "DataWedge Scanner Web" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow
  ```
- **Linux (ufw):**
  ```bash
  sudo ufw allow 58627/tcp
  sudo ufw allow 5000/tcp
  ```
- Make sure the PC and the MC93xx are on the same subnet/VLAN and that no client-isolation setting
  on the Wi-Fi access point is blocking device-to-device traffic.

---

## 14. Known Assumptions and Limitations

- **This solution was written and reviewed, but not compiled, in the sandbox that produced it.**
  That sandbox's network egress allowlist blocks `api.nuget.org` and Microsoft's package feeds
  (confirmed directly -- `apt` access to Ubuntu's own archives works, NuGet does not), so
  `dotnet restore`/`build`/`test`/`ef` could not be run there. This was also never going to be a
  meaningful limitation in practice: this app has to run on a PC/server on the same LAN as the
  Zebra device and reachable from your PostgreSQL instance, which that sandbox never was either.
  **Run `dotnet build` and `dotnet test` yourself as a first step** (section 11) before wiring up
  the real scanner.
- **No authentication/authorization** on the web UI or the TCP listener -- anyone who can reach
  the LAN can view the dashboard or send scans. Fine for a trusted-LAN POC; add auth before any
  wider deployment.
- **Plain HTTP, no TLS** for the web UI, to keep LAN access from other devices simple during the
  POC. Do not expose this configuration to an untrusted network as-is.
- **TCP framing is a best guess** (newline-delimited) until validated against a real MC93xx +
  DataWedge profile. Section 4 explains exactly how to swap it if testing shows otherwise.
- **Status workflow only implements `Ready -> Loaded`.** Any item seeded/created in another status
  (`Pending`, `InTransit`, `Delivered`, `Cancelled`) is left unchanged when scanned and currently
  reported as `AlreadyLoaded`, which is a slight misnomer for those states -- see section 7 for
  where to extend this.
- **Barcode matching is case-sensitive** and exact-match only; no fuzzy/partial matching.
- **Migrations are applied automatically on every app startup** (`db.Database.MigrateAsync()` in
  `Program.cs`) for POC convenience. A production deployment would typically run migrations as a
  separate, explicit release step instead.
- **Unit tests cover business logic only** (EF Core InMemory, no networking) per the four required
  scenarios. There are no integration tests driving an actual TCP socket in this POC; if you want
  that later, a loopback `TcpClient` connecting to a listener bound to `127.0.0.1:0` (dynamic port)
  is the natural way to add one without needing a real Zebra device.
