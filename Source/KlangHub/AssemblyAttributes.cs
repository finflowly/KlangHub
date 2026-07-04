using System.Runtime.CompilerServices;

// The MSBuild <InternalsVisibleTo> item is ignored here because this project sets
// <GenerateAssemblyInfo>false</GenerateAssemblyInfo>, so declare it in code.
[assembly: InternalsVisibleTo("KlangHub.Tests")]
