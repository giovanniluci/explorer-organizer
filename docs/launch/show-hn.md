# Show HN

**Title** (max 80 chars):

> Show HN: Explorer Organizer – offline IF/THEN file organizer for Windows with undo

**URL:** https://github.com/giovanniluci/explorer-organizer

**First comment** (post right after submitting):

> Hi HN, I built Explorer Organizer because my Downloads folder kept turning into a landfill and the tools I tried
> were either drag-and-drop only (DropIt) or watch-folder only (File Juggler, paid).
>
> It's a small Windows app (C# / .NET 8 / WPF, MIT) where you write rules like
> "IF file type is image THEN move to {pictures}\{taken:yyyy/MM}". Rules can be triggered by dropping files on
> the window or a floating target, from the Explorer context menu, or automatically by watching folders.
>
> Things I cared about:
> - Nothing leaves the PC: no network calls, no telemetry, no account.
> - Preview before anything moves, plus a simulation mode for automatic rules.
> - Every run can be undone (persistent journal, last 50 runs). Deletes and overwrites go to the Recycle Bin, and
>   they're refused where there is no Recycle Bin (network drives).
> - Conditions on EXIF/ID3/document metadata and on text content (Office, ODT, RTF, text PDFs).
>
> Known limitations: Windows only, ZIP is the only archive format it extracts, UI in English/Italian only, and the
> binaries aren't code-signed yet, so SmartScreen will complain. SHA-256 checksums are in the release and CI builds it
> from the tagged source.
>
> I'd love feedback on the rule editor and on which conditions/actions you'd miss.

Notes:
- Before claiming File Juggler is paid, check its current pricing page on posting day. If unsure, drop "paid".
- Don't ask for upvotes anywhere. HN penalizes vote rings.
- Stay online for 3–4 hours after posting and reply to every comment.
