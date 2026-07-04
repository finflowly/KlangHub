// Project-wide global usings for the neutral KlangHub.Core layer, so its own sub-namespaces
// (Models, Audio, Casting) resolve each other without per-file usings. Mirrors the app-side
// GlobalUsings.cs (which imports these same namespaces from the referenced KlangHub.Core assembly).
global using KlangHub.Core.Models;
global using KlangHub.Core.Audio;
global using KlangHub.Core.Casting;
global using KlangHub.Core.Diagnostics;
