#if NETSTANDARD2_1
// Polyfill for C# 9+ `init` setters on netstandard2.1.
//
// Reason: records and init-only properties (e.g. MPathOptions) require the
// compiler to reference `System.Runtime.CompilerServices.IsExternalInit`.
// This type ships in-box on net5.0+ but is missing from netstandard2.1.
// Declaring it here with any accessibility satisfies the compiler — the
// runtime only needs the type to exist; it is not used at runtime.
//
// This is the standard polyfill pattern recommended for multi-target
// libraries that want `init` on netstandard2.1. The type is compiled
// ONLY for the netstandard2.1 TFM to avoid colliding with the in-box
// definition on net5.0+ / net8.0 / net10.0 (which would raise CS0436).

using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Compiler-required marker for
/// <c>
/// init
/// </c>
/// setters; polyfilled on netstandard2.1.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit { }
#endif
