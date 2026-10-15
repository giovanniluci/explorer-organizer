# Launch kit (drafts, not published)

Everything here is a **draft to post by hand**. No automated posting, no vote requests, no fake accounts.
Always disclose that you are the author, follow each community's self-promotion rules, and answer comments yourself.

| Channel | File | When |
|---|---|---|
| Hacker News (Show HN) | [show-hn.md](show-hn.md) | Day 1, Tue–Thu, 14:00–16:00 CET (8–10 am US Eastern) |
| Reddit r/software, r/Windows11, r/opensource | [reddit.md](reddit.md) | Day 2–4, one subreddit per day, 15:00–17:00 CET |
| X / LinkedIn | [social.md](social.md) | Day 1 after the HN post, then again with the first user feedback |
| Product Hunt | [product-hunt.md](product-hunt.md) | Week 2, Tue–Thu, launch at 09:01 CET (00:01 Pacific) |
| First 48 hours | [checklist.md](checklist.md) | From the first post |

Why staggered: you can answer every comment personally, fix the first bugs before the bigger audience arrives,
and quote real feedback in later posts.

## Shared assets

- GIF: `https://raw.githubusercontent.com/giovanniluci/explorer-organizer/main/assets/tour.gif`
- Logo: `assets/logo-512.png`
- Social preview (1280×640): `assets/social-preview.png`
- Repo: https://github.com/giovanniluci/explorer-organizer
- Download: https://github.com/giovanniluci/explorer-organizer/releases/latest

## One-line pitch

> Explorer Organizer: a free, open-source, offline file organizer for Windows with IF/THEN rules, preview and undo.

## Facts you can safely claim

- MIT, C# / .NET 8 / WPF, self-contained exe, installer (per-user, no admin) and portable zip.
- Rules: nested ALL/ANY + NOT; conditions incl. EXIF/ID3/document metadata and text content (Office, PDF).
- Triggers: drag & drop (window or floating target), Explorer context menu, Send To, real-time watch, schedules, CLI.
- Preview before applying, simulation mode, undo of the last 50 runs, deletes go to the Recycle Bin.
- No network access, no telemetry, no account.
- UI in English and Italian.
- Not code-signed yet (SmartScreen warning). Say so up front.

Avoid: "the best", "faster than X", user numbers you don't have, or anything about DropIt/File Juggler that isn't in
`docs/feature-matrix.md`.
