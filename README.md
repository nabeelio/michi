[![Michi NuGet Package](https://img.shields.io/nuget/v/Michi.svg)](https://www.nuget.org/packages/Michi/) [![Michi NuGet Package Downloads](https://img.shields.io/nuget/dt/Michi)](https://www.nuget.org/packages/Michi) [![GitHub Actions Status](https://github.com/nabeelio/Michi/workflows/Build/badge.svg?branch=main)](https://github.com/nabeelio/Michi/actions)

# Michi

Stop passing raw strings around for filesystem paths. `MPath` is a strongly-typed, immutable, 
absolute path that normalizes at construction and works on Windows, macOS, and Linux. After not
finding anything on nuget, I created and have used this library as part of a larger project for 
years, I'm now extracting it into a full-fledged library

Michi (ミチ) means path in Japanese. The core type is `MPath`:

- **M**odern Path
- **M**anaged Path
- **M**ichi Path
- 
## Install

```
dotnet add package Michi
```

Targets `netstandard2.1`, `net8.0`, `net10.0`.

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
MPath.From("/var/log");                       // absolute Unix
MPath.From(@"C:\Logs\app");                   // absolute Windows
MPath.From("logs/today.log", "/var");         // relative + explicit base
MPath.TryFrom("maybe-a-path?", out var p);    // non-throwing
MPath.Format("/home/{0}/docs", "alice");      // template + args
```

Relative paths without a base resolve against `AppContext.BaseDirectory`. That's deliberate. 
`Directory.GetCurrentDirectory()` varies by launch context and is a mutable process property, 
so Michi stays away from it by default.

Null input to `From` throws `ArgumentNullException`. Empty string throws `InvalidPathException`. 
`TryFrom` swallows both and returns `false`.

## Joining

```csharp
var config = MPath.Home / ".config" / "myapp" / "settings.json";

// multiple segments at once
var log = base.Join("logs", "2026", "04", "today.log");
```

The right side is always treated as relative. Leading `/` or `\` gets stripped, so `base / "/etc"`
gives you `{base}/etc`, not `/etc`. If you want to escape the base, use `..` explicitly.

This is also how you can chain (yeah...contrived...):

```csharp

public class AppPaths 
{
    private static string _appName = "myapp";
    ...
    public static MPath BaseDirectory { get; set }
    public static AbsolutePath CacheDirectory { get; set; } = null!;
    public static AbsolutePath BlobCacheDirectory { get; set; } = null!;
    
    public void Configure(MPath installedDirectory) {
        BaseDirectory = installedDirectory;
        CacheDirectory = BaseDirectory / "cache";
        BlobCacheDirectory = BaseDirectory / Cache / "blobcache"
        
        // This will create the directory if it doesn't exist, clean it out if it does
        CacheDirectory.Create(clean: true)
        
        // Create the this directory, don't exist because the above is missing
        BlobCacheDirectory.Create()
    }
}
```


## Usage in an `AppPaths` container

This is another pattern I use it:

```csharp
// Wrap your own app-wide defaults if you need them:
public static class AppPaths
{
    public required MPath Data { get; init; }
    public required MPath Logs { get; init; }
    public required MPath Cache { get; init; }

    public static AppPaths Default { get; } = Build(MPath.From(AppContext.BaseDirectory, Options));

    public static AppPaths Build(MPath root) => new()
    {
        Data  = root / "data",
        Logs  = root / "logs",
        Cache = root / "cache",
    };
}
```

You can also use `MPathScoped` (though this example is a bit contrived). In my app, I set different
paths for some roots, depending on if the application was installed through an installer, or from
a ZIP file and sitting in a random directory.

```csharp
public static class AppPaths
{
    public MPath Data { get; set; }
    public MPath Logs { get; set; }
    public MPath Cache { get; set; }
    
    // Dynamically get a path
    public MPath UserDatabase(string userName) => Cache.Format($"{userName}/data.sqlite")

    public static AppPaths Default { get; } = Build(MPath.From(AppContext.BaseDirectory, Options));

    public static AppPaths Build(MPath root) {
    {
        var scoped = new MPathScoped(MPath.LocalApplicationData / "myapp");
        
        return new AppPaths() {
            Data  = root / "data",
            Logs  = root / "logs",
        
            // We want to place the cache in %LocalAppData%/myappname/cache
            Cache = scoped / "cache"
        };
    }
}
```

## Normalization

Every `MPath` goes through the same pipeline:

```csharp
MPath.From("/foo/../bar//baz/").ToString();   // "/bar/baz"
MPath.From("/foo\\bar").ToUnixString();       // "/foo/bar"  (backslash -> forward)
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

Hash and equality derive from one source (`HostOs.PathComparer`), so they can't drift apart. 
If two paths are equal, their hashes match. Always.

Need explicit behavior regardless of host?

```csharp
var seen = new HashSet<MPath>(MPathComparer.Ordinal);            // always case-sensitive
var lookup = new Dictionary<MPath, int>(MPathComparer.OrdinalIgnoreCase); // always case-insensitive
```

## String output

```csharp
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

`WithName` rejects anything with a separator in it. If you want to change the name AND the directory, that's `file.Parent / "newdir" / "newname"`.

## Well-known paths

A bunch of well-known paths/commonly used are included

```csharp
MPath.Home;              // user profile -- cached singleton
MPath.Temp;              // system temp -- cached singleton
MPath.InstallDirectory   // where the exe is installed to
MPath.CurrentDirectory;  // evaluated on every access, NOT cached
```

`Home` and `Temp` are stable for the process lifetime so they're lazy singletons. `CurrentDirectory` 
never caches because `Directory.SetCurrentDirectory` exists and would silently break you.

There's also:

- **`MPath.ApplicationData`**
  - Windows: `%APPDATA%`, e.g. `C:\Users\{user}\AppData\Roaming`
  - macOS: `~/Library/Application Support`
  - Linux: `$XDG_CONFIG_HOME`, else `~/.config`
- **`MPath.LocalApplicationData`**

## Tilde and env vars

Off by default. Opt in per-call:

```csharp
var opts = MPathOptions.Default with { ExpandTilde = true };
var config = MPath.From("~/.config/app", opts);   // expands to /home/alice/.config/app

var opts2 = MPathOptions.Default with { ExpandEnvironmentVariables = true };
var data = MPath.From("$HOME/data", opts2);       // Unix: $VAR, ${VAR}
var data2 = MPath.From("%APPDATA%/app", opts2);   // Windows: %VAR%
```

Unix env-var expansion throws `InvalidPathException` on undefined variables. Windows delegates to 
`Environment.ExpandEnvironmentVariables`, which leaves unknown `%VAR%` references literal.

## Options

`MPathOptions.Default` is read-only. No mutable global state.

```csharp
// Wrap your own app-wide defaults if you need them:
public static class AppPaths
{
    public static MPathOptions Options { get; } = MPathOptions.Default with
    {
        BaseDirectory = "/opt/myapp",
        ExpandTilde = true,
    };
    
    public required MPath Data { get; init; }
    public required MPath Logs { get; init; }
    public required MPath Cache { get; init; }

    public static AppPaths Default { get; } = Build(MPath.From(AppContext.BaseDirectory, Options));

    public static AppPaths Build(MPath root) => new()
    {
        Data  = root / "data",
        Logs  = root / "logs",
        Cache = root / "cache",
    };
}

var file = MPath.From("data/settings.json", AppPaths.Options);
```

The record's `with` expression is the ergonomic way to override per-call or wrap a static accessor 
for process-wide defaults.

## Serialization

Built-in. The `[JsonConverter]` and `[TypeConverter]` attributes on `MPath` wire up automatically. 
No registration required.

```csharp
// System.Text.Json
var json = JsonSerializer.Serialize(MPath.From("/a/b"));  // "\"/a/b\""
var back = JsonSerializer.Deserialize<MPath>(json);        // round-trips

// IConfiguration, ASP.NET model binding, WPF PropertyGrid -- all work via TypeConverter
```

Round-trip uses the canonical forward-slash form, so a Windows-serialized path reads correctly on Linux.

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
// if httpFilename = "../../etc/passwd", target = "/etc/passwd" with no warning.

// ✅ SAFE -- escape attempts throw.
var target = uploads.ResolveContained(httpFilename);
// throws InvalidPathException when httpFilename escapes the uploads directory.

// ✅ SAFE, non-throwing -- useful in per-request or per-archive-entry hot paths.
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
✅ Is pure and fast -- no filesystem I/O, no exception allocated on the `TryResolveContained` path,
safe to call from async hot paths and per-request sanitization loops.

❌ Does NOT resolve symlinks -- if the filesystem contains attacker-placed symlinks (multi-tenant
servers, user-supplied archive extraction, containers mounting untrusted volumes), a "contained"
`MPath` can still read or write a target outside the base. That is CWE-59 territory and needs
filesystem-layer mitigation, not path math.
❌ Does NOT prevent TOCTOU races -- even after canonicalization, an attacker can swap the path for
a symlink between your check and your I/O call.
❌ Does NOT validate against case-sensitivity mismatches -- NTFS directories with per-directory
case-sensitivity flags (Windows Subsystem for Linux interop) are not inspected; `ResolveContained`
uses the host-OS default.

### Adversarial filesystems

If your threat model includes attacker-placed symlinks on the filesystem you're reading from
(multi-tenant file servers, user-uploaded archive extraction, containers mounting untrusted
volumes), `ResolveContained` is not sufficient on its own. The mitigations live at the I/O layer,
not in path math:

- **Canonicalize before checking:** call `FileInfo.ResolveLinkTarget(returnFinalTarget: true)` to
  resolve symlinks to their final target, then run `ResolveContained` against the canonicalized
  result. Still subject to TOCTOU -- the link can change between the check and the open.
- **Atomic no-follow file opens:** on .NET 10+, pass `FileOptions.NoLinkFollow` to `FileStream`/
  `File.Open` to refuse to open a path if any component is a symlink.
- **Race-free sandboxing requires P/Invoke:** on Unix, `openat` + `O_NOFOLLOW`; on Windows,
  `CreateFileW` with `FILE_FLAG_OPEN_REPARSE_POINT`. Michi does not wrap these -- they are
  OS-specific and the correct abstraction depends on your host setup.

A future `Michi.FileSystem` package may offer a `ResolveContainedCanonical` variant that combines
canonicalization with containment once the async / cancellation-token story lands in the core
library. Until then, security-aware consumers compose `ResolveContained` with the I/O-layer
mitigations above.

## What it doesn't do

- No implicit `string` to `MPath` or `MPath` to `string` conversions. Use `MPath.From(s)` to construct and `p.Path` (or `p.ToString()`) to extract the string. Implicit conversions make it too easy to lose the type and start mixing raw strings back into your code.
- No filesystem I/O in this package. `MPath` is pure-string. Use `File.*` or `Directory.*` with `mpath.Path` (or `mpath.ToString()`) for that.
- No symbolic link resolution. Use `FileInfo.ResolveLinkTarget` if you need it.
- Construction is strict. Empty paths, null chars, and unresolvable relatives throw. Catch `InvalidPathException` or use `TryFrom`.

## Contributing

### Formatter quirks

The repo enforces ReSharper's `cleanupcode` + `inspectcode` via `dotnet tool run jb` on every
commit (see `AGENTS.md` for the full pre-commit checklist). Along the way we've hit a few
formatter edge cases where the default cleanup behaviour was wrong. Each one is documented
here so nobody has to rediscover them.

#### Trailing commas in single-line collection expressions

**Rule changed:** `resharper_trailing_comma_in_singleline_lists = false` (was `true`).

**Why:** When `resharper_max_array_initializer_elements_on_line = 1` and
`resharper_trailing_comma_in_singleline_lists = true` are both set, cleanupcode mis-composes
the two rules on multi-element `char[]` collection expressions. A declaration like
`private static readonly char[] X = ['/', '\\'];` gets rewritten to `['/', '\\',,,]` —
stray triple-commas that produce `CS1525: Invalid expression term ','` and fail the build.
The bug reproduces with any 2+ element `char[]`; `string[]` and other element types are
unaffected. Setting the single-line trailing-comma rule to `false` breaks the composition.
Multi-line trailing commas (the setting that actually matters for diff hygiene) stay on.

**If you hit a similar case:** always read the diff between the pre-cleanup and post-cleanup
file before accepting it. If cleanup is making the code worse, fix the rule (preferred) or
guard the block with `// @formatter:off` / `// @formatter:on` tags (already enabled in
`.editorconfig` via `resharper_formatter_tags_enabled = true`). Add a comment explaining why,
and write the case up in this section.

## License

MIT.
