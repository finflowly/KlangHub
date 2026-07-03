// Project-wide global usings for the KlangHub layered namespaces.
// Keeps the incremental namespace migration (Phase 2) low-churn: moving a type
// into one of these namespaces needs no per-consumer 'using' edits.
// Extend this list as new Core.* layers are introduced (Abstractions, Configuration, ...).
global using KlangHub.Core.Models;
