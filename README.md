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
