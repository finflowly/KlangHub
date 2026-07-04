# Tier 2 — Optimization Design (2026-07-05)

**Status:** approved-by-directive (user goal "Erledige Tier2 perfekt, nutze alle Skills" + scope answer
"Alle 4 Punkte"). **Baseline:** HEAD `e798671`, .NET 10, 65 tests green.

Tier 2 = the optimizations deliberately deferred out of the behaviour-preserving net10 bump. Four workstreams.

## A — Audio hot-path allocations (behaviour-preserving, byte-identical)

| # | File:line | Before | After | Why safe |
|---|-----------|--------|-------|----------|
| A1 | `ChromecastAudioSink.cs:50` | `Encode(dataToSendIn.ToArray())` | `Encode(dataToSendIn)` | `dataToSendIn == frame.Data`, a fresh per-frame array read synchronously by the encoder |
| A2 | `Mp3Stream.cs:45` | `writeBuffer = buffer.ToArray(); Writer.Write(writeBuffer…)` | `Writer.Write(buffer, 0, buffer.Length)` | LAME writer reads synchronously inside the `lock`; the defensive copy is dead weight |
| A3 | `LoopbackCaptureEngine.cs:281` | `bufferSend.Data.Take(Used).ToArray()` | `bufferSend.Data.AsSpan(0, Used).ToArray()` | still a fresh array (downstream retains it — see D), just without the LINQ per-byte enumerator; runs up to 1000 Hz |

Net: the MP3 frame goes from **2 redundant copies to 0**; the capture thread loses its LINQ overhead.
**Guarded by** the existing `ChromecastAudioSinkTests` (WAV byte-identical + MP3 == reference encoder).

## D → E — "Buffer pooling": corrected to the safe realization

**Naive ArrayPool is UNSAFE here and is rejected.** `ApplicationBuffer.KeepABuffer` (`ApplicationBuffer.cs:119`)
stores the frame's `byte[]` *reference* in a rolling ~350 KB history list — the array is retained across many
frames. Pooling `frame.Data` and returning it after `IAudioSink.Write` would corrupt that retained history
(use-after-free). Fresh-array-per-frame is therefore *required* for correctness with the current ownership.
A truly pooled design would need an ownership redesign of `ApplicationBuffer`/`StreamingConnection` that, net,
*adds* copies — out of scope for Tier 2 (a separate spike if ever wanted).

**E — the safe, stronger win** (this is what the pooling goal actually points at):
- `StreamingConnection.cs:65-70`: replace `Data.Take(Used).ToArray()` + `List.AddRange` + `bytes.ToArray()`
  (triple allocation per send iteration, up to 1000 Hz/connection) with a **zero-allocation**
  `Socket.Send(bufferSend.Data, 0, count, SocketFlags.None)`.
- Preserve the original "reset `Used` before the send attempt" semantics: capture `count = Used`, set `Used = 0`,
  then send `Data[0..count]`. Byte-identical to the socket. Safe because the buffer is stable between swaps and
  the send is synchronous; the transient was purely local (nothing retains it).
- **Risk:** this lives in the *non-unit-tested* socket send path → transformation is provably equivalent, but it
  belongs in the hardware streaming test.

## B — `Directory.Build.props`

New `Source/Directory.Build.props` hoisting the shared MSBuild props: `<ImplicitUsings>disable`,
`<LangVersion>latest`, `<Deterministic>true`, and (workstream C) `<Nullable>enable`. Remove the now-duplicated
`Nullable`/`ImplicitUsings` lines from the 4 `.csproj`. TFM stays per-project (net10.0 vs net10.0-windows).
Low risk; `dotnet build` + tests confirm.

## C — `<Nullable>enable` project-wide (the bulk of the effort + main risk)

1. Enable globally via `Directory.Build.props`.
2. Drive warnings to **zero**, project by project in reference order **Core → Platform → App → Tests** (Core's
   annotations change Platform's warnings), building + testing after each project.
3. Behaviour-preserving: real annotations (`?`, guards), **no** blanket `!` suppressions; WinForms
   Designer/generated files that emit noise get a `#nullable disable` file header (accepted standard), never
   contort real code.
4. End state: 0 nullable warnings, 65 tests green, build 0 errors.

## Execution order & verification

Order (low-risk first): **A + E** → **B** → **C**. Separate commit per workstream (A+E together as "audio
allocations", B, C) for clean review/revert. Direct to `master` (project convention).

Each workstream: build Release (0 errors) + `dotnet test` (65+ green). At the end: runtime smoke on net10,
`requesting-code-review`, `verification-before-completion` (evidence, not assertions).

## Honest risks
1. **C (Nullable)** is large and churny — the real risk; tests stay green as the safety net, but many files change.
2. **E** is in the untested socket send path → needs the hardware streaming test.
3. **D (real ArrayPool)** deliberately omitted — unsafe without an ownership redesign.
4. A1/A2/A3 are low-risk and test-guarded.
