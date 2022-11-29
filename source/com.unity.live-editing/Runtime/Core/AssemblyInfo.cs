using System.Runtime.CompilerServices;

// Internal access needed for runtime assemblies

// Internal access needed for editor assemblies
[assembly: InternalsVisibleTo("Unity.LiveEditing.Editor")]

// Internal access needed for testing
[assembly: InternalsVisibleTo("InternalsVisible.ToDynamicProxyGenAssembly2")]
[assembly: InternalsVisibleTo("Unity.LiveEditing.Tests.Editor")]
