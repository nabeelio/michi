#if NETSTANDARD2_1
// Polyfill for C# 9+ `init` setters on netstandard2.1. Records and init-only properties need the
// compiler to see `System.Runtime.CompilerServices.IsExternalInit`. The type ships in-box on net5+
// but is missing from netstandard2.1 -- declaring it here at any accessibility satisfies the
// compiler. The type is compiled ONLY for the netstandard2.1 TFM to avoid CS0436 collisions with
// the in-box definition on newer targets.

using System.ComponentModel;

// ReSharper disable once CheckNamespace -- polyfill MUST live in the BCL namespace for the compiler to recognize it.
namespace System.Runtime.CompilerServices;

/// <summary>Compiler-required marker for `init` setters; polyfilled on netstandard2.1.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit;
#endif
