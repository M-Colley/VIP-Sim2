# Release setup

Everything here needs credentials that have to be bought or issued to a named person. The
tooling is written and waiting; these are the steps only the repository owner can take.

Two things are blocked on this, and both are worth doing before VIP-Sim is given to anyone
outside the team:

- **Signing** — unsigned, macOS Gatekeeper blocks the app outright and Windows SmartScreen
  warns on every download.
- **CI builds** — without a Unity licence in Actions, a green CI run means the repository
  invariants passed, **not** that either project compiles.

---

## 1. Unity licence for CI  *(free, ~15 minutes)*

The `build` and `unity-tests` jobs are skipped today. The workflow matrix is already
written; it needs only the secrets.

For a **Personal** licence:

1. Follow <https://game.ci/docs/github/activation> to obtain a `.ulf` licence file.
2. In the repository: **Settings → Secrets and variables → Actions → New repository secret**
3. Add `UNITY_LICENSE` with the entire contents of the `.ulf` file.

For a **Pro** licence, add `UNITY_EMAIL`, `UNITY_PASSWORD` and `UNITY_SERIAL` instead.

The preflight job detects either and enables the rest automatically — no workflow edit
needed. Verify by pushing: the run should no longer print the "Unity jobs skipped" warning.

> Note: Unity Personal has a revenue ceiling. Selling VIP-Sim above it requires Pro, which
> is a licensing question as much as a CI one.

---

## 2. macOS signing and notarisation  *(~99 USD/year)*

1. Join the [Apple Developer Program](https://developer.apple.com/programs/).
2. In Xcode: **Settings → Accounts → Manage Certificates → + → Developer ID Application**.
3. Store a notarisation credential (needs an app-specific password from appleid.apple.com):

   ```bash
   xcrun notarytool store-credentials "VIPSIM_NOTARY" \
     --apple-id "you@example.com" --team-id "YOURTEAMID" --password "app-specific-password"
   ```

4. On a Mac, with the build copied across:

   ```bash
   ./tools/sign-macos.sh /path/to/VIP-Sim.app "Developer ID Application: Your Name (TEAMID)"
   ```

That signs inside-out, notarises, and staples the ticket so the app opens offline. Once
signed, the `chmod`/`xattr` steps in `MACOS_README.md` are no longer needed — that
workaround exists **only** because the build is unsigned.

Find your identity string with `security find-identity -v -p codesigning`.

---

## 3. Windows signing  *(~200–400 USD/year)*

1. Buy a code-signing certificate from a CA (DigiCert, Sectigo, SSL.com and others).
   An **EV** certificate carries SmartScreen reputation immediately; a standard **OV** one
   accumulates it over time, so early downloads may still be warned about.
2. Install the Windows SDK "Signing Tools" component, which provides `signtool.exe`.
3. Run:

   ```powershell
   .\tools\sign-windows.ps1 -BuildDir "C:\...\VIP-Sim-Windows-Build" -CertPath "C:\certs\vipsim.pfx"
   ```

It signs the bundled native plug-ins as well as the executable — a signed `.exe` loading
unsigned DLLs is blocked by some enterprise policies even though the executable itself
verifies — and timestamps with RFC 3161, so signatures stay valid after the certificate
expires.

---

## 4. Before a public release

- **`bundleVersion` is `2.0.0beta`.** Apple expects `CFBundleShortVersionString` to be up
  to three period-separated integers. Fine for local use; change to `2.0.0` before
  notarising and carry the pre-release marker elsewhere.
- **Telemetry ships disabled.** `FirestoreRESTManager.telemetryEnabled` is off by design.
  Turning it on for a study needs a lawful basis, a privacy notice and recorded consent —
  VIP-Sim records captured programs, enabled impairments and free-text feedback, and drives
  a webcam.
- **Presets are uncalibrated.** `VipSimPresets` severities are starting points, not
  validated stimuli. Anything published should state the values used and how they were
  reached.
- **macOS has been run once**, informally. It has not been run since the per-eye refactor
  removed a camera.

## Release asset names must stay stable

Attach the archives as exactly `VIP-Sim-Windows-x64.zip` and
`VIP-Sim-macOS-universal.zip` -- no version number in the file name.

The download buttons on the website use
`https://github.com/M-Colley/VIP-Sim2/releases/latest/download/<name>`, which GitHub
resolves only for an exact asset name. Putting the version in the name would break both
buttons on every release, silently, and the person who notices is a user who cannot
download the software.

The version still travels with the download: it is the release tag, it is in the
`CHANGELOG.md` inside each archive, and the app reports it in the F1 panel.
