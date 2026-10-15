# Reddit

Check each subreddit's rules on the day: some allow self-promotion only on certain days or in a weekly thread.
Post natively (upload the GIF, link the repo in the text or first comment), one subreddit per day, and disclose authorship.

---

## r/software

**Title:** I made a free, open-source file organizer for Windows: IF/THEN rules, preview and undo (no telemetry)

**Body:**

> I got tired of cleaning my Downloads folder by hand, so I built **Explorer Organizer**.
>
> You write rules such as *IF file type = image THEN move to Pictures\{year}\{month}*, then either drop files on it,
> right-click in Explorer, or let it watch folders in real time.
>
> - Preview before anything moves, plus undo (even after a restart)
> - Deletes go to the Recycle Bin, never permanent unless you confirm
> - Conditions on name/regex, size, dates, EXIF/ID3 metadata, even text inside PDFs and Office files
> - Installer (no admin) or portable zip, no .NET install needed
> - Offline: no account, no network, no telemetry. MIT licensed.
>
> Download and source: https://github.com/giovanniluci/explorer-organizer
>
> It's v1.0, so feedback and bug reports are very welcome. (Not code-signed yet, so SmartScreen will warn you.)

**Attach:** `assets/tour.gif`

---

## r/Windows11

**Title:** Built a native-looking (Mica, dark mode) file organizer for Windows 11: rules, Explorer context menu, undo

**Body:**

> Explorer Organizer is a small open-source app I made to keep Downloads/Desktop tidy automatically.
> It uses Mica and follows light/dark/high-contrast, and adds "Organize with Explorer Organizer" to the context menu
> (under *Show more options* on Windows 11).
>
> Rules are IF/THEN (file type, name, date, EXIF, text content…) → move / rename / sort / zip… with a preview first
> and one-click undo. Everything stays local.
>
> https://github.com/giovanniluci/explorer-organizer (free, MIT)
>
> Happy to hear what would make it fit better into Windows 11.

**Attach:** `docs/screenshots/render-home-dark.png` + `docs/screenshots/render-home-light.png`

---

## r/opensource

**Title:** Explorer Organizer: MIT-licensed rule-based file organizer for Windows (C# / .NET 8 / WPF), looking for contributors

**Body:**

> I just open-sourced Explorer Organizer, a Windows file organizer with IF/THEN rules, real-time folder watching,
> preview and undo. No telemetry or network access.
>
> Stack: C# 12, .NET 8, WPF, xUnit (engine + UI Automation tests), Inno Setup, GitHub Actions for reproducible releases
> with SHA-256 checksums.
>
> Good places to help: translations (only EN/IT today), 7z/RAR extraction, a winget manifest, more rule presets.
> Issues labelled `good first issue` are the easiest place to start.
>
> https://github.com/giovanniluci/explorer-organizer
