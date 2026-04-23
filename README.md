[![Michi NuGet Package](https://img.shields.io/nuget/v/Michi.svg)](https://www.nuget.org/packages/Michi/) [![Michi NuGet Package Downloads](https://img.shields.io/nuget/dt/Michi)](https://www.nuget.org/packages/Michi) [![GitHub Actions Status](https://github.com/nabeelio/Michi/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/nabeelio/Michi/actions/workflows/ci.yml)

# Michi

Michi (ミチ) means "path" in Japanese. The core type is `MPath`: a strongly-typed, immutable, absolute 
path that normalizes at construction and behaves the same on Windows, macOS, and Linux.

- **M**odern Path
- **M**anaged Path
- **M**ichi Path

Stop passing raw strings around for filesystem paths.

I pulled this out of a larger project after looking for something like it on NuGet and coming up 
empty. It had been running in production for years called "MPath"

## Install

```bash
dotnet add package Michi
```

Targets `netstandard2.1`, `net8.0`, `net10.0`.

## Basics

```csharp
using Michi;

var logs = MPath.From("/var/log/myapp");
var file = logs / "2026-04-20.log";           // /var/log/myapp/2026-04-20.log
var parent = file.Parent;                     // /var/log/myapp
var renamed = file.WithName("today.log");     // /var/log/myapp/today.log
var md = file.WithExtension("md");            // /var/log/myapp/2026-04-20.md
```

Sealed class. Construct with `From`. Every derived path is a new instance. Nothing mutates.

## Construction

The usage is pretty simple:

```csharp
MPath.From("/var/log");                       // Unix absolute
MPath.From(@"C:\Logs\app");                   // Windows absolute
MPath.From("logs/today.log", "/var");         // relative + explicit base
MPath.TryFrom(maybePath, out var p);          // non-throwing
MPath.Format("{0}/{1}", MPath.Home.ToUnixString(), "docs");
```

Relative paths without a base resolve against `AppContext.BaseDirectory`. That's intentional. 
`Directory.GetCurrentDirectory()` is process-mutable and varies by launch context, so Michi 
stays away from it by default.

`From(null)` throws `ArgumentNullException`. Empty string throws `InvalidPathException`. `TryFrom` 
returns `false` for both.

## Joining Paths

```csharp
var config = MPath.Home / ".config" / "myapp" / "settings.json";

var installRoot = MPath.InstalledDirectory;
var log = installRoot.Join("logs", "2026", "04", "today.log");
var cache = MPath.LocalApplicationData / "myapp" / "cache";
```

The right side is always treated as relative. Leading `/` or `\` gets stripped, so 
`installRoot / "/etc"` gives you `{installRoot}/etc`, not `/etc`. 
If you want to escape the base, use `..`.

The main pattern I use: wrap app roots in your own type. My WPF application uses a lot of
different paths - for cache, database, profiles, scripts, etc.

```csharp
public sealed class AppPaths
{
    public AppPaths(MPath root)
    {
        Data = root / "data";
        Logs = root / "logs";
        Cache = MPath.LocalApplicationData / "myapp" / "cache";
        
        // TODO: Add examples of ensuring the above dirs exist
        Cache.CreateOrEmpty();
    }

    public MPath Data { get; }
    public MPath Logs { get; }
    public MPath Cache { get; }

    // Get a path for someone's storage
    public MPath UserDatabase(string profile) => Cache / profile / "data.sqlite";
}

var paths = new AppPaths(MPath.InstalledDirectory);

// Or AppPaths with DI
```

## Normalization

```csharp
MPath.From("/foo/../bar//baz/").ToUnixString();     // "/bar/baz"
MPath.From(@"C:\foo\..\bar\\baz\").ToUnixString();  // "C:/bar/baz" (Windows-only input)
```

`..` resolves up. `.` drops out. Repeated separators collapse. Trailing slashes go away. Internally everything ends up in forward-slash canonical form. Backslash input works on any OS.

## Equality

```csharp
var a = MPath.From("/Foo/Bar");
var b = MPath.From("/foo/bar");

a.Equals(b);                            // true on Windows/macOS, false on Linux
a.GetHashCode() == b.GetHashCode();     // always consistent with Equals
```

Hashing and equality both go through the host-OS comparer, so they can't drift apart. 
Equal paths always hash the same.

Need explicit behavior regardless of host?

```csharp
var seen = new HashSet<MPath>(MPathComparer.Ordinal);
var lookup = new Dictionary<MPath, int>(MPathComparer.OrdinalIgnoreCase);
```

## String output

```csharp
var p = MPath.From(@"C:\Users\alice");

p.ToString();         // "C:\Users\alice"   OS-native
p.Path;               // "C:\Users\alice"   same thing, as a property
p.ToUnixString();     // "C:/Users/alice"   always forward slash
p.ToWindowsString();  // "C:\Users\alice"   always backslash
```

`ToString()` and `.Path` return the OS-native form, so `MPath` drops into `File.ReadAllText(p.Path)` and any other string API without conversion. For logs, JSON, cache keys, or anything else you want identical across platforms, use `ToUnixString()`.

## Navigation

```csharp
var file = MPath.From("/var/log/myapp/today.log");

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
var file = MPath.From("/a/b/c.txt");

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
MPath.InstalledDirectory;     // AppContext.BaseDirectory
MPath.ApplicationData;        // per-user app data
MPath.LocalApplicationData;   // per-user, machine-local
MPath.CommonApplicationData;  // machine-wide: %ProgramData% on Windows, /usr/share on Unix
MPath.Home;                   // user profile
MPath.Temp;                   // temp directory
MPath.CurrentDirectory;       // re-evaluated every access, never cached
```

All lazy singletons except `CurrentDirectory`. That one never caches, because 
`Directory.SetCurrentDirectory` exists and would silently break you.

## Tilde and env vars

Off by default. Opt in per-call:

```csharp
var opts = MPathOptions.Default with { ExpandTilde = true };
var config = MPath.From("~/.config/app", opts);

var opts2 = MPathOptions.Default with { ExpandEnvironmentVariables = true };
var data = MPath.From("$HOME/data", opts2);       // Unix: $VAR, ${VAR}
var data2 = MPath.From("%APPDATA%/app", opts2);   // Windows: %VAR%
```

Unix expansion throws `InvalidPathException` on undefined variables. Windows delegates to 
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

## Serialization

`[JsonConverter]` and `[TypeConverter]` are already on `MPath`. Nothing to register.

```csharp
using System.Text.Json;
using Michi;

var original = MPath.From(@"C:\work\logs\today.log");
var json = JsonSerializer.Serialize(original);       // "\"C:/work/logs/today.log\""
var back = JsonSerializer.Deserialize<MPath>(json);  // same-host round-trip

// IConfiguration, ASP.NET model binding, WPF PropertyGrid all work via TypeConverter
```

JSON always writes canonical forward-slash form, so payloads stay identical across hosts. 
Deserialization still uses the current OS rules, so a foreign-root payload like 
`C:/work/logs/today.log` may or may not parse depending on where you run it. Not a supported 
cross-OS portability mechanism.

**Coming:** `MPathRelative` for serialization. Has to be joined with an `MPath` to resolve, 
so config files stay relative after round-trip instead of turning absolute.

## Security

### Untrusted input

Path segments from outside your process (ZIP archive entries, HTTP requests, config values, 
env variables) need containment, not just normalization. The `/` operator and `MPath.Format` 
happily resolve `../` segments, which means user input can escape the intended base 
directory (CWE-22 / ZIP-slip).

`ResolveContained` normalizes AND verifies the result stays under the base.

```csharp
var uploads = MPath.From("/var/www/uploads");

// DANGEROUS: user input can traverse
var target = MPath.Format("/var/www/uploads/{0}", httpFilename);
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
  `MPath` can still read or write outside the base. That's CWE-59 and needs filesystem-layer 
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
  Michi doesn't wrap these because the right abstraction depends on your host setup

## License

MIT.

## Inspiration

Inspired by `Nuke.AbsolutePath`.
