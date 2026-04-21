# Development Guide

Contributor-facing guide for working on Michi. Complements `AGENTS.md` (which is written for AI coding agents); this doc is written for humans reading linearly.

## Prerequisites

- **.NET SDK**: version pinned in `global.json` (currently `10.0.101`). The repo uses `rollForward: latestMinor`, so any 10.0.x SDK works. Install from https://dotnet.microsoft.com/download.
- **Git** — any recent version.
- **Optional: JetBrains Rider or ReSharper** — the repo includes `.editorconfig` (universally supported) and `Michi.sln.DotSettings` (ReSharper/Rider-specific). Inspection and cleanup run via the `jb` CLI tool regardless; IDE integration is nice-to-have, not required.

## Getting started

```bash
git clone git@github.com:nabeelio/Michi.git
cd Michi

# Restore the JetBrains CLI tools pinned in dotnet-tools.json
dotnet tool restore

# Restore NuGet dependencies
dotnet restore Michi.slnx

# Build + test
dotnet build Michi.slnx -c Release
dotnet test tests/Michi.Tests/Michi.Tests.csproj -c Release --no-build
```

Expected: 0 errors, 0 warnings (enforced by `TreatWarningsAsErrors=true` in `Directory.Build.props`), 0 test failures.

## Daily workflow

The `AGENTS.md` pre-commit checklist defines five commands that must pass before any commit. Humans run them in the same order:

```bash
# 1. Restore tools (idempotent, safe to run repeatedly).
dotnet tool restore

# 2. Build — must show "0 errors, 0 warnings" across all three target frameworks.
dotnet build Michi.slnx -c Release --nologo

# 3. Test — must show "0 failed".
dotnet test tests/Michi.Tests/Michi.Tests.csproj -c Release --no-build --nologo

# 4. Cleanup — applies .editorconfig + ReSharper formatting.
#    Re-run build and test after if cleanup modified anything.
dotnet tool run jb -- cleanupcode Michi.slnx --profile="Built-in: Full Cleanup"

# 5. Inspect — must report "Issues />" (self-closing tag = zero issues).
dotnet tool run jb -- inspectcode Michi.slnx --output=/tmp/inspect.xml --format=Xml --severity=WARNING --no-build
grep -c '<Issue ' /tmp/inspect.xml   # must print 0
```

All five must succeed with zero errors, zero warnings, zero inspection issues before committing.

## Linter philosophy: inspect output is not automatic truth

Each `jb cleanupcode` run is a **proposal**, not a verdict. Before accepting cleanup output, read the diff and evaluate it:

1. **Does the change improve the code?** Keep it and move on.
2. **Does the change make the code worse** (ugly wrapping, broken logic, invalid syntax)? The rule that produced it is wrong for this case. Fix the rule; don't contort the code to appease a bad rule.

**Options when a rule is wrong, ordered by preference:**

1. **Adjust the rule in `.editorconfig`** — change the setting globally. Add an inline comment explaining why, and document the case under "Known formatter quirks" in `AGENTS.md`.
2. **Guard the block with `// @formatter:off` / `// @formatter:on`** — already enabled via `resharper_formatter_tags_enabled = true`. Use when the rule is right globally but wrong for one spot.
3. **Suppress a ReSharper inspection in `Michi.sln.DotSettings`** — for inspection warnings, not formatter output. Each entry requires an inline XML comment.
4. **`#pragma warning disable` in a `.cs` file** — last resort. Requires a matching justification comment on the line above the pragma.

Known formatter quirks are documented in `AGENTS.md` under "Known formatter quirks" — read those before adding new suppressions.

## Continuous Integration

Two workflows in `.github/workflows/`:

- **`ci.yml`** — runs on every PR and push to `main`. Builds and tests on Ubuntu, Windows, and macOS in parallel. Zero-config: inherits .NET SDK version from `global.json`. Expected runtime: ~3-5 min cold, ~2 min warm cache.

- **`release.yml`** — runs only on pushes of tags matching `v*`. Validates, packs, signs via Azure Key Vault, publishes to GitHub Packages + NuGet.org, generates a CycloneDX SBOM, and creates a GitHub Release with all artifacts attached. Expected runtime: ~5-7 min per release.

## Releasing

Michi uses MinVer to derive the package version from git tags. Tag `v1.2.3` produces NuGet package version `1.2.3`. Tag `v1.0.0-beta.1` produces `1.0.0-beta.1` (marked as a pre-release).

**Release procedure:**

1. Ensure `main` is green in CI (check the Actions tab).
2. Verify all intended changes are on `main` — `git log origin/main --oneline` to cross-check.
3. Create the tag:
   ```bash
   git tag v1.2.3
   # Or for pre-release:
   git tag v1.0.0-beta.1
   ```
4. Push the tag:
   ```bash
   git push origin v1.2.3
   ```
5. Watch the release workflow in the Actions tab. Expected: `validate` job passes in ~2 min, then `pack-sign-publish` runs for ~5 min.
6. After success, verify:
   - A new GitHub Release appears at `https://github.com/nabeelio/Michi/releases` with `.nupkg`, `.snupkg`, and `.cyclonedx.json` attached.
   - The package appears on NuGet.org at `https://www.nuget.org/packages/Michi/` (allow ~10 min for NuGet indexing).
   - The package appears on GitHub Packages at `https://github.com/nabeelio/Michi/packages`.

**If something fails partway:**

The workflow is idempotent. Both `dotnet nuget push` commands use `--skip-duplicate`, so re-running against the same tag is safe:

1. Navigate to the failed workflow run in the Actions tab.
2. Click "Re-run failed jobs."

**If a bad package reaches NuGet.org:**

NuGet.org packages are immutable — they cannot be deleted. You can only **unlist** them:

1. Sign in to NuGet.org.
2. Navigate to your account → Manage Packages → Listed.
3. Click the bad version → Manage Package → Unlist.

Unlisting hides the package from search but does not break existing consumers. To ship a fix, cut a new patch version (`v1.2.4`) with the correction.

## First-time setup: Azure Key Vault + GitHub OIDC

**This is a bootstrap procedure for a fresh repo or new signing identity. Skip if an existing federated credential already works.**

The release workflow authenticates to Azure Key Vault via OpenID Connect workload identity federation. No long-lived secrets are stored in GitHub.

**Prerequisites:**
- An Azure subscription with a Key Vault containing a code-signing certificate (RSA-2048 or RSA-3072; Code Signing EKU).
- The certificate was created as an `RSA-HSM` key in the Key Vault (non-exportable). This is the default for new code-signing certs per CA/Browser Forum requirements.
- Premium SKU Key Vault is required for EV certs and any cert issued after June 2023.

**Azure setup:**

1. **Create or identify a service principal** in Entra ID (App registrations).
2. **Add a federated credential** on the SP:
   - Scenario: **GitHub Actions deploying Azure resources**
   - Organization: `nabeelio`, Repository: `Michi`
   - Entity type: **Environment**, environment name: `release` (matches the `environment: release` declaration in `release.yml`)
   - Name the credential something like `michi-release`
3. **Grant the SP Key Vault roles** on the vault containing the signing cert:
   - `Key Vault Reader`
   - `Key Vault Crypto User`

**GitHub setup:**

1. **Create the `release` GitHub Environment** at Settings → Environments → New environment. Required reviewers are optional for a solo maintainer.
2. **Add repository secrets** (Settings → Secrets and variables → Actions → Secrets tab):
   - `AZURE_TENANT_ID` — from Entra ID Properties
   - `AZURE_SUBSCRIPTION_ID` — from Subscriptions
   - `AZURE_CLIENT_ID` — the SP's App ID
   - `KEY_VAULT_URL` — full URL like `https://kv-nabeel.vault.azure.net/`
   - `KEY_VAULT_CERT_NAME` — the certificate name inside the vault
   - `NUGET_API_KEY` — API key from https://www.nuget.org/account/apikeys (scope: "Push new packages and package versions", packages: `Michi*`)

   All values are stored as secrets rather than variables. This matches Microsoft's `dotnet/sign` sample workflow — the Azure identifiers don't technically require secrecy, but using secrets uniformly means they're auto-masked in logs.

**Verification:** the first successful release cut validates the entire chain. If the first release fails at `Azure OIDC login`, the federated credential is misconfigured — re-check the entity type and environment name in Azure.

## Dependency updates

Two bots, disjoint ownership:

- **Dependabot** (`.github/dependabot.yml`) — GitHub Actions version pins only. Weekly Monday schedule.
- **Renovate** (`renovate.json`) — all .NET dependencies: NuGet packages in csproj and `Directory.Packages.props`, .NET SDK in `global.json`, tools in `dotnet-tools.json`. Weekly Monday schedule.

**Installing Renovate:**

One-time setup by the repo owner:

1. Visit https://github.com/apps/renovate
2. Click Configure → select `nabeelio/Michi`
3. Renovate opens an onboarding PR within ~15 min — merge it to activate.

**Reviewing update PRs:**

For both bots, CI runs on every PR. Merging protocol:

1. Wait for CI to finish on the PR.
2. If CI is green and the change is a minor or patch version bump of a dependency we already trust, merge.
3. For major version bumps, read the upstream CHANGELOG before merging.
4. For .NET SDK bumps (`global.json`), consider whether to update the CI runner's SDK version expectation (no action needed — `actions/setup-dotnet@v4` installs whatever `global.json` requests).

## Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| `dotnet build` works locally but CI fails on Linux only | Path-case sensitivity bug: Linux is case-sensitive, Windows and macOS default to case-insensitive. See `MPath` platform-case behavior in `.planning/PROJECT.md`. |
| `MinVer` reports version `0.0.0-alpha.0.0+unknown` in CI | `fetch-depth: 0` is missing from the `actions/checkout` step. MinVer needs tag history. |
| Release workflow succeeds but no package appears on NuGet.org | NuGet.org indexing can take 10-30 minutes after a successful push. If still missing after 30 min, check the workflow logs for the `Publish to NuGet.org` step. |
| `Azure OIDC login` step fails with "AADSTS70025: The client is configured..." | Federated credential entity type mismatch. The credential must be scoped to `environment:release`, not `branch:main` or `tag:v*`. |
| `Sign packages` step fails with "certificate not found" | `KEY_VAULT_CERT_NAME` variable value does not match the cert name in the vault. Case-sensitive. |

## References

- `AGENTS.md` — rules for AI coding agents (authoritative for pre-commit discipline, C# idioms, pitfalls)
- `.planning/PROJECT.md` — project scope, value proposition, key decisions
- `.planning/ROADMAP.md` — phase breakdown
- Design spec for this CI pipeline: `docs/superpowers/specs/2026-04-21-github-actions-release-pipeline-design.md`
