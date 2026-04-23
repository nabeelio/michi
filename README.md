[![Michi NuGet Package](https://img.shields.io/nuget/v/Michi.svg)](https://www.nuget.org/packages/Michi/) [![Michi NuGet Package Downloads](https://img.shields.io/nuget/dt/Michi)](https://www.nuget.org/packages/Michi) [![GitHub Actions Status](https://github.com/nabeelio/Michi/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/nabeelio/Michi/actions/workflows/ci.yml)

# Michi

Michi (ミチ) means path in Japanese. The core type is `MPath`:

- **M**odern Path
- **M**anaged Path
- **M**ichi Path

Stop passing raw strings around for filesystem paths. `MPath` is a strongly-typed, immutable,
absolute path that normalizes at construction and behaves consistently across Windows, macOS, and
Linux.

After not finding anything on NuGet, I pulled `MPath` out of a larger project where I had been 
developing it and had use for years in production, gave it a name, and it became Michi :)

## Install

```bash
dotnet add package Michi
```

Targets `netstandard2.1`, `net8.0`, `net10.0`, and works across Windows, macOS and Linux

## The Basics

```csharp
using Michi;

var logs = MPath.From("/var/log/myapp");
var file = logs / "2026-04-20.log";           // /var/log/myapp/2026-04-20.log
var parent = file.Parent;                     // /var/log/myapp
var renamed = file.WithName("today.log");     // /var/log/myapp/today.log
var md = file.WithExtension("md");            // /var/log/myapp/2026-04-20.md
```

`MPath` is a sealed class. Construct it via `From`, derive new values by combining, mutating, or navigating. Nothing mutates in place.

## Construction

```csharp
MPath.From("/var/log");                       // Unix absolute path
MPath.From(@"C:\Logs\app");                   // Windows-only absolute path
MPath.From("logs/today.log", "/var");         // relative + explicit base
string? maybePath = null;
MPath.TryFrom(maybePath, out var p);           // false, non-throwing
MPath.Format("{0}/{1}", MPath.Home.ToUnixString(), "docs");
```

Relative paths without a base resolve against `AppContext.BaseDirectory`. That's deliberate. 
`Directory.GetCurrentDirectory()` varies by launch context and is a mutable process property, 
so Michi stays away from it by default.

Null input to `From` throws `ArgumentNullException`. Empty string throws `InvalidPathException`. 
`TryFrom` swallows both and returns `false`.

## Joining

```csharp
// use the short hand
var config = MPath.Home / ".config" / "myapp" / "settings.json";

// multiple segments at once
var installRoot = MPath.InstalledDirectory;
var log = installRoot.Join("logs", "2026", "04", "today.log");
var cache = MPath.LocalApplicationData / "myapp" / "cache";
```

The right side is always treated as relative. Leading `/` or `\` gets stripped, so
`installRoot / "/etc"` gives you `{installRoot}/etc`, not `/etc`. If you want to escape the base,
use `..` explicitly.

One reasonable way to wrap application-specific roots is to build your own container type:

```csharp
public sealed class AppPaths
{
    public AppPaths(MPath root)
    {
        Data = root / "data";
        Logs = root / "logs";
        Cache = MPath.LocalApplicationData / "myapp" / "cache";
    }

    public MPath Data { get; }
    public MPath Logs { get; }
    public MPath Cache { get; }

    public MPath UserDatabase(string userName) => Cache / userName / "data.sqlite";
}

var paths = new AppPaths(MPath.InstalledDirectory);
```

## Normalization

Every `MPath` goes through the same pipeline:

```csharp
MPath.From("/foo/../bar//baz/").ToUnixString();     // "/bar/baz" on Unix
MPath.From(@"C:\foo\..\bar\\baz\").ToUnixString(); // Windows-only input -> "C:/bar/baz"
```

`..` resolves up, `.` drops out, repeated separators collapse, trailing slashes get stripped, 
and everything ends up in forward-slash canonical form internally. Backslash input works on any OS.

## Equality that actually works

```csharp
var a = MPath.From("/Foo/Bar");
var b = MPath.From("/foo/bar");

a.Equals(b);                                   // true on Windows/macOS, false on Linux
a.GetHashCode() == b.GetHashCode();            // same -- consistent with Equals
```

Hashing and equality use the same host-OS comparison rules, so they can't drift apart. If two
paths are equal, their hashes match.

Need explicit behavior regardless of host?

```csharp
var seen = new HashSet<MPath>(MPathComparer.Ordinal);            // always case-sensitive
var lookup = new Dictionary<MPath, int>(MPathComparer.OrdinalIgnoreCase); // always case-insensitive
```

## String output

```csharp
// Windows-only example
var p = MPath.From(@"C:\Users\alice");

p.ToString();         // "C:\Users\alice"   -- OS-native separators (Windows here)
p.Path;               // "C:\Users\alice"   -- same as ToString(), ergonomic property
p.ToUnixString();     // "C:/Users/alice"   -- always forward slash, platform-independent
p.ToWindowsString();  // "C:\Users\alice"   -- always backslash
```

`ToString()` and `.Path` return the OS-native form so an `MPath` drops into `File.ReadAllText(p.Path)`,
`new StreamReader(p.Path)`, and any other string-typed API without extra conversion. When you need a
deterministic string that reads the same on every OS (logs you diff across platforms, JSON payloads,
cache keys), use `ToUnixString()`.

## Navigation

```csharp
var file = MPath.From("/var/log/myapp/today.log");

file.Parent;                          // /var/log/myapp
file.Up(2);                           // /var/log
file.TryGetParent(out var parent);    // non-throwing at root
file.Root;                            // "/"  -- or "C:/", or "//server/share"
file.Depth;                           // 4
file.Segments;                        // ["var", "log", "myapp", "today.log"]
```

Walking past the root throws `NoParentException`. Use `TryGetParent` if you want to handle that without a try/catch.

## Mutation (returns new instances)

```csharp
var file = MPath.From("/a/b/c.txt");

file.WithName("d.md");       // /a/b/d.md
file.WithExtension("json");  // /a/b/c.json    -- dot optional
file.WithExtension(".json"); // /a/b/c.json    -- same thing
file.WithExtension(null);    // /a/b/c         -- removes extension
file.WithoutExtension();     // /a/b/c
```

`WithName` accepts exactly one valid path segment: non-empty, no separators, not `.` or `..`, and still valid for the current platform (for example Windows also rejects reserved device names and trailing `.` or space). If you want to change the name AND the directory, that's `file.Parent / "newdir" / "newname"`.

## Well-known paths

Common roots are built in:

```csharp
var install = MPath.InstalledDirectory;                 // AppContext.BaseDirectory
var config = MPath.ApplicationData / "myapp" / "config.json";
var cache = MPath.LocalApplicationData / "myapp" / "cache";
var tempFile = MPath.Temp / "myapp" / "import.tmp";
var cwd = MPath.CurrentDirectory;                       // evaluated on every access
```

`Home`, `Temp`, `InstalledDirectory`, `ApplicationData`, `LocalApplicationData`, and
`CommonApplicationData` are lazy singletons. `CurrentDirectory` never caches because
`Directory.SetCurrentDirectory` exists and would silently break you.

There's also:

- `MPath.Home`: current user's profile directory
- `MPath.CommonApplicationData`: machine-wide shared app data (`%ProgramData%` on Windows,
  `/usr/share` on macOS/Linux)

## Tilde and env vars

Off by default. Opt in per-call:

```csharp
var opts = MPathOptions.Default with { ExpandTilde = true };
var config = MPath.From("~/.config/app", opts);   // expands under the current user's home directory

var opts2 = MPathOptions.Default with { ExpandEnvironmentVariables = true };
var data = MPath.From("$HOME/data", opts2);       // Unix: $VAR, ${VAR}
var data2 = MPath.From("%APPDATA%/app", opts2);   // Windows: %VAR%
```

Unix env-var expansion throws `InvalidPathException` on undefined variables. Windows delegates to 
`Environment.ExpandEnvironmentVariables`, which leaves unknown `%VAR%` references literal.

## Options

`MPathOptions.Default` is read-only. No mutable global state.

```csharp
var opts = MPathOptions.Default with
{
    BaseDirectory = MPath.InstalledDirectory.Path,
    ExpandTilde = true,
};

var file = MPath.From("data/settings.json", opts);
var homeConfig = MPath.From("~/.config/myapp/settings.json", opts);
```

The record's `with` expression is the ergonomic way to override per-call or wrap a static accessor 
for process-wide defaults.

## Serialization

Built-in. The `[JsonConverter]` and `[TypeConverter]` attributes on `MPath` wire up automatically. 
No registration required.

```csharp
using System.Text.Json;
using Michi;

// System.Text.Json (Windows host example; on Unix use "/work/logs/today.log")
var original = MPath.From(@"C:\work\logs\today.log");
var json = JsonSerializer.Serialize(original);             // "\"C:/work/logs/today.log\""
var back = JsonSerializer.Deserialize<MPath>(json);        // same-host round-trip

// IConfiguration, ASP.NET model binding, WPF PropertyGrid -- all work via TypeConverter
```

JSON always writes the canonical forward-slash form, so payloads are deterministic across hosts.
Deserialization still uses the current OS rules. A foreign-root payload such as
`C:/work/logs/today.log` can succeed or fail depending on the host, so it is not a supported
cross-OS portability mechanism.

**COMING** An `MPathRelative` that can also be used for serializing, and it has to joined
with an `MPath`, so your config files (if you save them back) don't all turn into absolute paths

## Security

### Untrusted input

When a path segment comes from outside your process -- a ZIP archive entry, an HTTP request, a
config value, an environment variable -- you need containment, not just normalization. `MPath.Format`
and the `/` operator happily normalize `../` segments, which means user input can escape the intended
base directory (CWE-22 / ZIP-slip). `ResolveContained` is the opposite: it normalizes AND verifies
the result stays under the base.

```csharp
var uploads = MPath.From("/var/www/uploads");

// ❌ DANGEROUS -- user input can traverse. Normalization happily resolves `../`.
var target = MPath.Format("/var/www/uploads/{0}", httpFilename);
// if httpFilename = "../../etc/passwd", target = "/var/etc/passwd" with no warning.

// ✅ SAFE -- escape attempts throw.
var safeTarget = uploads.ResolveContained(httpFilename);
// throws InvalidPathException when httpFilename escapes the uploads directory.

// ✅ SAFE, non-throwing for invalid non-null fragments -- useful in per-request or per-archive-entry hot paths.
if (uploads.TryResolveContained(httpFilename, out var safe))
{
    File.WriteAllBytes(safe.Path, bytes);
}
else
{
    return Results.BadRequest("filename must stay under the uploads directory");
}
```

### What `ResolveContained` does and doesn't

✅ Rejects lexical escape -- `../../etc/passwd`, `subdir/../../escape`, and every `..`-traversal
variant throws `InvalidPathException`.
✅ Rejects sibling-directory false positives -- `/var/www-evil` is NOT contained in `/var/www`, even
though one is a string prefix of the other. The guard is a segment boundary, not a `StartsWith`.
✅ Strips leading separators -- `base.ResolveContained("/etc/passwd")` joins to `base/etc/passwd`,
never to `/etc/passwd`. User input can't accidentally escape by starting with `/`.
✅ Is pure string/path processing -- no filesystem I/O, and `TryResolveContained` gives you a
non-throwing API surface for invalid non-null fragments in per-request sanitization loops. Null
input still throws `ArgumentNullException`.

❌ Does NOT resolve symlinks -- if the filesystem contains attacker-placed symlinks (multi-tenant
servers, user-supplied archive extraction, containers mounting untrusted volumes), a "contained"
`MPath` can still read or write a target outside the base. That is CWE-59 territory and needs
filesystem-layer mitigation, not path math.
❌ Does NOT prevent TOCTOU races -- even after a pre-check, an attacker can swap the path for
a symlink between your check and your I/O call.
❌ Does NOT validate against case-sensitivity mismatches -- NTFS directories with per-directory
case-sensitivity flags (Windows Subsystem for Linux interop) are not inspected; `ResolveContained`
uses the host-OS default.

### Adversarial filesystems

If your threat model includes attacker-placed symlinks on the filesystem you're reading from
(multi-tenant file servers, user-uploaded archive extraction, containers mounting untrusted
volumes), `ResolveContained` is not sufficient on its own. The mitigations live at the I/O layer,
not in path math:

- **Keep `ResolveContained` lexical-only:** use it to reject `..` traversal and sibling-prefix
  escapes before you touch the filesystem.
- **Enforce symlink policy where the open/create happens:** use the I/O layer or platform API that
  actually opens the file to decide whether symlinks are allowed.
- **Race-free sandboxing is OS-specific:** on Unix, that usually means `openat`-style flows with
  no-follow semantics; on Windows, `CreateFileW` with `FILE_FLAG_OPEN_REPARSE_POINT` or related
  handle-based APIs. Michi does not wrap these because the correct abstraction depends on your host
  setup.

## What it doesn't do

- No implicit `string` to `MPath` or `MPath` to `string` conversions. Use `MPath.From(s)` to construct and `p.Path` (or `p.ToString()`) to extract the string. Implicit conversions make it too easy to lose the type and start mixing raw strings back into your code.
- No filesystem I/O in this package. `MPath` is pure-string. Use `File.*` or `Directory.*` with `mpath.Path` (or `mpath.ToString()`) for that.
- No symbolic link resolution. Symlink-aware enforcement belongs in the I/O layer or platform APIs,
  not in `MPath`.
- Construction is strict. Empty paths, null chars, and unresolvable relatives throw. Catch `InvalidPathException` or use `TryFrom`.

## License

MIT.

## Inspiration

This library was inspired by the `Nuke.AbsolutePath` library.
