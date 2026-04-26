[![NuGet Version](https://img.shields.io/nuget/v/Segments.svg)](https://www.nuget.org/packages/Segments/) [![NuGet Downloads](https://img.shields.io/nuget/dt/Segments)](https://www.nuget.org/packages/Segments) [![GitHub Actions Status](https://github.com/nabeelio/Segments/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/nabeelio/Segments/actions/workflows/ci.yml)

# Segments

Strongly-typed absolute paths for .NET. Compose paths by joining validated string segments —
construct, normalize, navigate, and mutate, all immutably. The core type is `SPath`: a typed,
value object representing an absolute path that normalizes at construction. Works on Windows,
macOS, and Linux.

Stop passing raw strings around for paths. I pulled this out of a larger project after looking
for something like it on NuGet and coming up empty. I've been using it in a WPF app that's been
out for years, because it needs/creates paths for cache, database, profiles, scripts, etc.

## Install

```bash
dotnet add package Segments
```

Targets `netstandard2.1`, `net8.0`, `net10.0`.

## Basics

```csharp
using Segments;

var logs = SPath.From("/var/log/myapp");
var file = logs / "2026-04-20.log";           // /var/log/myapp/2026-04-20.log
var parent = file.Parent;                     // /var/log/myapp
var renamed = file.WithName("today.log");     // /var/log/myapp/today.log
var md = file.WithExtension("md");            // /var/log/myapp/2026-04-20.md
```

Sealed class. Construct with `From`. Every derived path is a new instance. Nothing mutates.

## Construction

The usage is pretty simple:

```csharp
SPath.From("/var/log");                       // Unix absolute
SPath.From(@"C:\Logs\app");                   // Windows absolute
SPath.From("logs/today.log", "/var");         // relative + explicit base
SPath.TryFrom(maybePath, out var p);          // non-throwing
SPath.Format("{0}/{1}", SPath.Home.ToUnixString(), "docs");
```

Relative paths without a base resolve against `AppContext.BaseDirectory`. That's intentional. 
`Directory.GetCurrentDirectory()` is process-mutable and varies by launch context, so Segments 
stays away from it by default.

`From(null)` throws `ArgumentNullException`. Empty string throws `InvalidPathException`. `TryFrom` 
returns `false` for both.

## Joining Paths

```csharp
var config = SPath.Home / ".config" / "myapp" / "settings.json";

var installRoot = SPath.InstalledDirectory;
var log = installRoot.Join("logs", "2026", "04", "today.log");
var cache = SPath.LocalApplicationData / "myapp" / "cache";
```

The right side is always treated as relative. Leading `/` or `\` gets stripped, so 
`installRoot / "/etc"` gives you `{installRoot}/etc`, not `/etc`. 
If you want to escape the base, use `..`.

The main pattern I use: wrap app roots in its own type, which you can use statically, or DI it.

```csharp
public sealed class AppPaths
{
    public AppPaths(SPath root)
    {
        Data = root / "data";
        Logs = root / "logs";
        Cache = SPath.LocalApplicationData / "myapp" / "cache";

        // Ensure the directories exist before any consumer calls UserDatabase().
        // Requires `using Segments.FileSystem;` — the FS extensions live there.
        Data.CreateDirectory();
        Logs.CreateDirectory();
        Cache.CreateDirectory();
    }

    public SPath Data { get; }
    public SPath Logs { get; }
    public SPath Cache { get; }

    // Build a per-profile DB path; EnsureParentExists creates the
    // profile-scoped Cache subdir if a new profile shows up.
    public SPath UserDatabase(string profile) =>
        (Cache / profile / "data.sqlite").EnsureParentExists();
}

var paths = new AppPaths(SPath.InstalledDirectory);
```

## Normalization

```csharp
SPath.From("/foo/../bar//baz/").ToUnixString();     // "/bar/baz"
SPath.From(@"C:\foo\..\bar\\baz\").ToUnixString();  // "C:/bar/baz" (Windows-only input)
```

`..` resolves up. `.` drops out. Repeated separators collapse. Trailing slashes go away. Internally everything ends up in forward-slash canonical form. Backslash input works on any OS.

## Equality

```csharp
var a = SPath.From("/Foo/Bar");
var b = SPath.From("/foo/bar");

a.Equals(b);                            // true on Windows/macOS, false on Linux
a.GetHashCode() == b.GetHashCode();     // always consistent with Equals
```

Hashing and equality both go through the host-OS comparer, so they can't drift apart. 
Equal paths always hash the same.

Need explicit behavior regardless of host?

```csharp
var seen = new HashSet<SPath>(SPathComparer.Ordinal);
var lookup = new Dictionary<SPath, int>(SPathComparer.OrdinalIgnoreCase);
```

## String output

```csharp
var p = SPath.From(@"C:\Users\alice");

p.ToString();         // "C:\Users\alice"   OS-native
p.Value;              // "C:\Users\alice"   primary property form
p.Path;               // "C:\Users\alice"   compatibility alias
p.ToUnixString();     // "C:/Users/alice"   always forward slash
p.ToWindowsString();  // "C:\Users\alice"   always backslash
```

`ToString()` and `.Value` return the OS-native form, so `SPath` drops into `File.ReadAllText(p.Value)` and any other string API without conversion. `.Path` is still there as a compatibility alias. For logs, JSON, cache keys, or anything else you want identical across platforms, use `ToUnixString()`.

## Navigation

```csharp
var file = SPath.From("/var/log/myapp/today.log");

file.Parent;                          // /var/log/myapp
file.Up(2);                           // /var/log
file.TryGetParent(out var parent);    // non-throwing
file.Root;                            // "/"  or "C:/", or "//server/share"
file.Depth;                           // 4
file.Segments;                        // ["var", "log", "myapp", "today.log"]
```

Walking past the root throws `NoParentException`. Use `TryGetParent` if you want to skip the try/catch.

## Mutation (returns new instances)

```csharp
var file = SPath.From("/a/b/c.txt");

file.WithName("d.md");       // /a/b/d.md
file.WithExtension("json");  // /a/b/c.json    dot optional
file.WithExtension(".json"); // /a/b/c.json    same thing
file.WithExtension(null);    // /a/b/c         removes extension
file.WithoutExtension();     // /a/b/c
```

`WithName` accepts exactly one valid segment. Non-empty, no separators, not `.` or `..`, and valid 
for the platform. Windows additionally rejects reserved device names and trailing `.` or space. 
To change the name AND the directory, use `file.Parent / "newdir" / "newname"`.

## Well-known paths

```csharp
SPath.InstalledDirectory;     // AppContext.BaseDirectory
SPath.ApplicationData;        // per-user app data
SPath.LocalApplicationData;   // per-user, machine-local
SPath.CommonApplicationData;  // machine-wide: %ProgramData% on Windows, /usr/share on Unix
SPath.Home;                   // user profile
SPath.Temp;                   // temp directory
SPath.CurrentDirectory;       // re-evaluated every access, never cached
```

All lazy singletons except `CurrentDirectory`. That one never caches, because 
`Directory.SetCurrentDirectory` exists and would silently break you.

## Tilde and env vars

Off by default. Opt in per-call:

```csharp
var opts = SPathOptions.Default with { ExpandTilde = true };
var config = SPath.From("~/.config/app", opts);

var opts2 = SPathOptions.Default with { ExpandEnvironmentVariables = true };
var data = SPath.From("$HOME/data", opts2);       // Unix: $VAR, ${VAR}
var data2 = SPath.From("%APPDATA%/app", opts2);   // Windows: %VAR%
```

Unix expansion throws `InvalidPathException` on undefined variables. Windows delegates to 
`Environment.ExpandEnvironmentVariables`, which leaves unknown `%VAR%` references literal.

## Options

`SPathOptions.Default` is read-only. No mutable global state.

```csharp
var opts = SPathOptions.Default with
{
    BaseDirectory = SPath.InstalledDirectory.Path,
    ExpandTilde = true,
};

var file = SPath.From("data/settings.json", opts);
var homeConfig = SPath.From("~/.config/myapp/settings.json", opts);
```

## Filesystem operations

Opt in with `using Segments.FileSystem;` for filesystem extension methods on `SPath`.

### Existence

```csharp
using Segments.FileSystem;

var config = SPath.Home / ".config" / "myapp.json";

config.FileExists();       // true only if a FILE exists at this path
config.DirectoryExists();  // true only if a DIRECTORY exists at this path
```

There is no bare `Exists()`. Asking "is there a file here?" and "is there a directory
here?" are different questions, and Segments makes you pick at the call site — kind-mismatch
bugs surface at write-time instead of runtime.

### Creation

```csharp
var cache = SPath.LocalApplicationData / "myapp" / "cache";

cache.CreateDirectory();       // idempotent, creates intermediates
cache.EnsureParentExists();    // creates missing parent only, returns self for chaining
```

### Enumeration (lazy)

```csharp
foreach (var log in logs.EnumerateFiles("*.log", SearchOption.AllDirectories))
{
    Console.WriteLine(log);
}

foreach (var sub in projectRoot.EnumerateDirectories("src*", SearchOption.TopDirectoryOnly))
{
    Console.WriteLine(sub);
}
```

Returns `IEnumerable<SPath>`, so LINQ works and you can break early without materializing
the full list.

### Move, copy, and conflict policy

```csharp
src.MoveTo(dst, ExistsPolicy.Fail);                     // default: throw on collision
src.MoveTo(dst, ExistsPolicy.MergeAndSkip);             // keep target on collision
src.MoveTo(dst, ExistsPolicy.MergeAndOverwrite);        // target wins
src.CopyTo(dst, ExistsPolicy.MergeAndOverwriteIfNewer); // newest wins
```

`MoveTo` and `CopyTo` auto-create the target parent directory if missing. `DeleteFile`
and `DeleteDirectory` are idempotent no-ops if the target is already gone.

### Advanced

Advanced usage lives in the test suite — tests are the executable cookbook,
for filesystem operations and for the lexical / equality machinery that
underpins them:

- `MoveTo` / `CopyTo` overload matrix, dir-into-dir resolution, same-path no-op →
  `tests/Segments.Tests/SPathFileSystemMoveTests.cs` and `SPathFileSystemCopyTests.cs`
- Deletion edge cases (recursive, wrong-kind errors, already-gone idempotency) →
  `tests/Segments.Tests/SPathFileSystemDeletionTests.cs`
- `System.IO` interop (`ToFileInfo`, `ToDirectoryInfo`) →
  `tests/Segments.Tests/SPathFileSystemInteropTests.cs`
- Containment / `ResolveContained` and `TryResolveContained` for ZIP-slip
  defense, lexical-escape rejection, drive-letter and UNC traversal blocks →
  `tests/Segments.Tests/SPathContainmentTests.cs`
- Platform case-sensitivity — Windows/macOS case-insensitive equality and
  hashing vs Linux case-sensitive equality, with `PlatformTestHelpers` gating →
  `tests/Segments.Tests/SPathEqualityTests.cs`

## Serialization

JSON, `IConfiguration` binding, and ASP.NET model binding are deferred until
`SPathRelative` lands. The shape of those converters depends on whether config
files express relative paths or absolute paths in an `SPath`-typed field, and
locking that choice now without `SPathRelative` would force a SemVer break later.

For now, round-trip through `string` manually:

```csharp
using System.Text.Json;
using Segments;

var p = SPath.From(@"C:\work\logs\today.log");
var json = JsonSerializer.Serialize(p.ToUnixString());        // "C:/work/logs/today.log"
var back = SPath.From(JsonSerializer.Deserialize<string>(json)!);
```

Use `ToUnixString()` for the JSON form so payloads stay identical across Windows
and Unix hosts. Deserialization still uses the current OS's path rules.

**Coming:** `SPathRelative` plus `[JsonConverter]` and `[TypeConverter]` designed
around the relative/absolute distinction. Config files stay relative after
round-trip instead of turning absolute.

## Security

### Untrusted input

Path segments from outside your process (ZIP archive entries, HTTP requests, config values, 
env variables) need containment, not just normalization. The `/` operator and `SPath.Format` 
happily resolve `../` segments, which means user input can escape the intended base 
directory (CWE-22 / ZIP-slip).

`ResolveContained` normalizes AND verifies the result stays under the base.

```csharp
var uploads = SPath.From("/var/www/uploads");

// DANGEROUS: user input can traverse
var target = SPath.Format("/var/www/uploads/{0}", httpFilename);
// httpFilename = "../../etc/passwd" gives you /var/etc/passwd with no warning

// SAFE: throws when the user tries to escape
var safeTarget = uploads.ResolveContained(httpFilename);

// SAFE: non-throwing, good for per-request hot paths
if (uploads.TryResolveContained(httpFilename, out var safe)) {
    File.WriteAllBytes(safe.Path, bytes);
}
else {
    return Results.BadRequest("filename must stay under the uploads directory");
}
```

### What ResolveContained does

- Rejects `../../etc/passwd`, `subdir/../../escape`, and every other `..` traversal
- Rejects sibling-directory false positives. `/var/www-evil` is NOT contained in `/var/www`. 
  The guard is a segment boundary, not `StartsWith`
- Strips leading separators. `base.ResolveContained("/etc/passwd")` joins to `base/etc/passwd`,
  never `/etc/passwd`
- Pure string/path processing. No filesystem I/O. `TryResolveContained` gives you a non-throwing 
  surface for per-request sanitization. Null input still throws `ArgumentNullException`

### What it doesn't do

- Does NOT resolve symlinks. If attackers can place symlinks on your filesystem (multi-tenant 
  servers, user-uploaded archive extraction, containers mounting untrusted volumes), a contained
  `SPath` can still read or write outside the base. That's CWE-59 and needs filesystem-layer 
  mitigation
- Does NOT prevent TOCTOU races. An attacker can swap a path for a symlink between your
  check and your I/O call
- Does NOT inspect case-sensitivity mismatches. WSL/NTFS per-directory case flags aren't 
  checked; the host-OS default applies

### Adversarial filesystems

If your threat model includes attacker-placed symlinks on the filesystem you're reading from,
`ResolveContained` is not enough on its own. Mitigations live at the I/O layer:

- Use `ResolveContained` lexical-only to reject `..` and sibling-prefix escapes before you touch 
  the filesystem
- Enforce symlink policy at the I/O call that actually opens the file
- Race-free sandboxing is OS-specific. On Unix that's usually `openat` with no-follow semantics. 
  On Windows it's `CreateFileW` with `FILE_FLAG_OPEN_REPARSE_POINT` or similar handle-based APIs. 
  Segments doesn't wrap these because the right abstraction depends on your host setup

## License

MIT.

## Inspiration

Inspired by `Nuke.AbsolutePath`.
