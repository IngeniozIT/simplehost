# SimpleHost

**A Windows 11 app to manage your `hosts` file, organized by category.**

SimpleHost is a native Windows 11 desktop application for editing the Windows `hosts`
file (`C:\Windows\System32\drivers\etc\hosts`) with full create / read / update / delete
support. Instead of a flat, comment-cluttered text file, your entries are grouped under
**categories you define** — with an always-on-top **"No category"** group for anything
uncategorized.

Because SimpleHost modifies a protected system file, it runs **elevated (as
administrator)**.

---

## Features

### Core

- **CRUD entries** in the Windows `hosts` file.
- **Optional per-entry Description** — attach a free-text note to any entry. It is stored
  as the trailing `#` comment on the entry's line, so the file stays a completely valid,
  portable `hosts` file.
- **User-defined categories** — group entries however you like. Uncategorized entries
  appear in a **"No category"** group pinned to the top of the list.
- **Drag-and-drop** to reorder entries and move an entry from one category to another.
- **Enable / disable** an entry without deleting it (toggles the leading `#`).
- **Automatic backup** of the `hosts` file before every write.
- **Optional DNS cache flush** (`ipconfig /flushdns`) after saving, so changes take
  effect immediately.
- **Deep-link automation** via the `simplehost://` URL protocol (see below).

### Managing entries at scale

- **Search / filter** by hostname, IP address, or category.
- **Profiles / bulk category toggle** — enable or disable an entire category (or a named
  profile) in a single click, e.g. switch between "Work" and "Home" setups.
- **Import / export** a single category or the whole file, to and from other `hosts`
  files.
- **Remote subscriptions** — subscribe to a remote `hosts` list (e.g. an ad-block list)
  that auto-refreshes into its own category.
- **Bulk multi-select operations** — move, toggle, delete, or re-categorize many entries
  at once.

### Safety

- **Pending-changes diff + explicit Apply** — preview exactly what will be written to the
  system file before it is modified.
- **Conflict / duplicate detection** — warns when the same hostname maps to multiple IPs,
  or when a line is duplicated.
- **Inline IP / hostname validation** as you type, backed by the domain value objects.

### Polish

- **Follows the system light/dark theme** (Fluent, Windows 11).
- **System tray** quick toggles for profiles and categories, without opening the main
  window.
- **CLI companion** mirroring the `simplehost://` actions — handy from a terminal or WSL.

---

## Requirements

- **Windows 11**
- **.NET 10 Desktop Runtime** (for framework-dependent builds)
- **Administrator rights** — required to modify the system `hosts` file

---

## How categories are stored

Categories are stored **inside the `hosts` file itself**, as structured `#` comment
markers. This keeps the file a completely valid, portable, standalone `hosts` file — there
is no separate database or config that can drift out of sync, and the file still works if
you copy it to another machine or edit it by hand.

A category is introduced by a marker comment; every entry line below it (until the next
marker) belongs to that category:

```text
# Managed by SimpleHost — https://github.com/…/simplehost
# Editing the category markers by hand is fine; keep them on their own line.

127.0.0.1        localhost                 # (no category — always listed first)
::1              localhost

# [SimpleHost:Category] Development
127.0.0.1        api.myapp.test
127.0.0.1        web.myapp.test            # local frontend
# 127.0.0.1      staging.myapp.test        # disabled entry (kept, not active)

# [SimpleHost:Category] Ad blocking
0.0.0.0          ads.example.com
0.0.0.0          tracker.example.net
```

- Entries **not** under any `# [SimpleHost:Category]` marker are shown under **"No
  category"**, at the top of the list.
- An entry's optional **Description** is the free text after a trailing `#` on its line
  (e.g. `# local frontend` above). It is shown next to the entry throughout the app and
  preserved verbatim in the file.
- A **disabled** entry is just a commented-out (`#`) `hosts` line — it stays in place but
  does not resolve.
- **Entry order and category membership map directly to line order** within each marked
  block. Drag-and-drop reordering or moving an entry between categories is persisted
  simply by rewriting the file in the new order — no separate ordering metadata is needed.

---

## Administrator rights / elevation

The `hosts` file lives under `C:\Windows\System32\drivers\etc\`, which only administrators
can modify. SimpleHost therefore:

- Ships a **UAC application manifest** requesting `requireAdministrator`, so Windows
  prompts for elevation when the app launches.
- Writes an **automatic backup** of the current `hosts` file before applying any change,
  so you can always roll back.

---

## Running against a custom `hosts` file (no elevation)

By default SimpleHost targets the protected Windows system `hosts` file, which requires
elevation to write. For local testing — or just to explore the app without touching your
system configuration — point it at **any custom file** instead. A custom file is read and
written **without administrator elevation**, so the app never prompts for UAC and your real
`hosts` file is left completely untouched.

The repository ships a ready-made sample at
[`samples/hosts.sample`](samples/hosts.sample) that already contains a realistic mix:
a couple of uncategorized entries, three `# [SimpleHost:Category]` groups, several inline
`#` comments, and a disabled (commented-out) entry.

Select the file in either of two ways — checked in this order of precedence:

1. **`--hosts-file <path>` command-line argument** (highest precedence):

   ```bash
   ./src/SimpleHost.Presentation/bin/Debug/net10.0-windows/SimpleHost.exe --hosts-file samples/hosts.sample
   ```

2. **`SIMPLEHOST_HOSTS_FILE` environment variable** (used when the argument is absent):

   ```bash
   SIMPLEHOST_HOSTS_FILE=samples/hosts.sample ./src/SimpleHost.Presentation/bin/Debug/net10.0-windows/SimpleHost.exe
   ```

When neither is set, SimpleHost falls back to the system file
`C:\Windows\System32\drivers\etc\hosts` (which *does* require elevation). Either a relative
or an absolute path works; copy the sample somewhere writable and edit it freely — because
it is a completely valid, standalone `hosts` file, you can re-run against it as often as you
like without ever affecting your real system configuration.

---

## URL protocol (deep links)

SimpleHost registers a custom `simplehost://` URL scheme. Opening such a link (from a
browser, a script, or the CLI companion) launches the app and performs an action.

| Action  | Example |
|---------|---------|
| Add     | `simplehost://add?ip=127.0.0.1&hostname=example.test&category=Development&comment=demo` |
| Update  | `simplehost://update?hostname=example.test&ip=127.0.0.2&category=Staging` |
| Delete  | `simplehost://delete?hostname=example.test` |
| Enable  | `simplehost://enable?hostname=example.test` |
| Disable | `simplehost://disable?hostname=example.test` |

- **`update`, `delete`, and `disable` always prompt for confirmation** before touching the
  `hosts` file. Any web page can trigger a link, so destructive actions are never silent.
- Read/focus links simply open the app scrolled to the target entry.
- On `add`, the optional `comment` parameter sets the new entry's **Description** (the
  trailing `#` note on its line).

---

## Architecture

SimpleHost follows **Clean Architecture** — one project per layer, with all dependencies
pointing **inward**. The domain knows nothing about WPF, the file system, or the registry,
which keeps the core fully unit-testable without Windows.

```text
┌─────────────────────────────────────────────┐
│ Presentation   WPF (MVVM) · simplehost:// ·  │  →  depends on Application
│                CLI companion                 │
├─────────────────────────────────────────────┤
│ Infrastructure  hosts reader/writer · backup │  →  implements Application ports
│                 · DNS flush · URL registration│
├─────────────────────────────────────────────┤
│ Application     use cases (add/update/delete/ │  →  depends on Domain
│                 reorder/move) · ports         │
├─────────────────────────────────────────────┤
│ Domain          HostEntry · Category ·        │  →  no dependencies
│                 IP / Hostname value objects   │
└─────────────────────────────────────────────┘
```

- **Domain** — entities (`HostEntry`, `Category`), value objects (IP address, hostname),
  and domain rules. No external dependencies.
- **Application** — use cases for the CRUD / reorder / move operations, defined against
  interfaces (ports) such as `IHostsFileRepository`.
- **Infrastructure** — adapters: the `hosts`-file reader/writer that parses and serializes
  the `#`-marker format, backup, DNS flush, and Windows registry registration of the
  `simplehost://` scheme.
- **Presentation** — the WPF UI (MVVM), the `simplehost://` deep-link entry point, and the
  CLI companion.

---

## Testing & quality

Quality bars are **non-negotiable** and enforced in CI:

- **Detroit-style (classicist) TDD** — tests assert on observable **state / output** using
  **real** domain and application objects. Test doubles are used only at the true external
  boundaries (file system, registry, DNS). The `#`-marker **parse ⇄ serialize round-trip**
  is a prime state-based test target.
- **100% line + branch coverage**, enforced with **coverlet**.
- **100% mutation score**, enforced with **Stryker.NET**.

```bash
# Run the test suite with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run mutation testing
dotnet stryker
```

Test stack: **xUnit** + a fluent assertion library.

---

## Development (on Debian WSL)

SimpleHost is developed on Debian under WSL2 and built with the cross-platform .NET SDK.
The app itself runs on the **Windows host** (WSL2 interop can launch the built `.exe`
directly).

**Prerequisites:** install the **.NET 10 SDK** on Debian.

```bash
# Restore and build from the repo root
dotnet restore
dotnet build

# Launch the built executable on the Windows host (WSL2 interop)
./src/SimpleHost.Presentation/bin/Debug/net10.0-windows/SimpleHost.exe
```

Notes for WSL development:

- The real Windows `hosts` file is reachable read-only at
  `/mnt/c/Windows/System32/drivers/etc/hosts` for inspection during development.
- **Do not point the app at the real file while developing.** Use the bundled
  [`samples/hosts.sample`](samples/hosts.sample) via `--hosts-file` (or
  `SIMPLEHOST_HOSTS_FILE`) so tests and manual runs never touch your system configuration —
  see [Running against a custom `hosts` file](#running-against-a-custom-hosts-file-no-elevation)
  above. Writing the real file also requires elevation, which only happens when running the
  app on the Windows host.

---

## Building a release

```bash
# Framework-dependent (smaller; requires the .NET 10 Desktop Runtime on the target machine)
dotnet publish src/SimpleHost.Presentation -c Release

# Self-contained (larger; bundles the runtime, no install needed on the target)
dotnet publish src/SimpleHost.Presentation -c Release --self-contained -r win-x64
```

Choose **framework-dependent** for the smallest artifact when the runtime is already
present, or **self-contained** to ship a single portable build with no runtime
prerequisite.

---

## Project status

**Early / planned.** This README documents the intended design; implementation is just
beginning.

---

## License

Released under the [MIT License](LICENSE). © 2026 Ingenioz IT.
