# Feature matrix — DropIt vs File Juggler vs Explorer Organizer

Fonti lette con Firecrawl il 2026-10-14:

- DropIt: <https://www.dropitproject.com/>, wiki `howto:filter_files_and_folders`, `howto:customize_destinations`, `howto:automatically_scan_folders` (v8.5.1).
- File Juggler: <https://www.filejuggler.com/>, `/features/`, `/documentation/conditions/`, `/documentation/actions/`, `/documentation/variables/`.

Legenda: ✔ presente · ✖ assente · ~ parziale · **Sì / No** = incluso in Explorer Organizer (EO).

## Condizioni / filtri

| Funzione | DropIt | File Juggler | EO | Note EO |
|---|---|---|---|---|
| Combinazione AND (tutte) | ✔ (filtri aggiuntivi) | ✔ "All of the following" | **Sì** | Gruppo "Tutte" |
| Combinazione OR (almeno una) | ~ (`;` tra regole) | ✔ "Any of the following" | **Sì** | Gruppo "Almeno una" |
| NOT / esclusioni | ✔ (`/` esclusioni) | ✔ (is not / does not contain) | **Sì** | "Non" su ogni condizione e gruppo |
| Gruppi annidati | ✖ | ✔ | **Sì** | |
| Tutti i file | ✔ (`*`) | ✔ "All files" | **Sì** | |
| File senza altre corrispondenze (`#`) | ✔ | ✖ | **No** | Coperto dall'ordine delle regole (prima corrispondenza vince) + regola finale "Tutti i file" |
| Nome: è / contiene / inizia / finisce | ~ | ✔ | **Sì** | |
| Nome con wildcard `*` `?` | ✔ | ✖ | **Sì** | |
| Nome con regex | ✔ | ✔ (variabili regex) | **Sì** | gruppi catturati riusabili come `{match:N}` |
| Estensione | ✔ | ✔ | **Sì** | lista `jpg;png` |
| Percorso file / percorso cartella | ✔ | ✔ | **Sì** | utile per escludere sottocartelle |
| Dimensione (>, <, =, tra) | ✔ | ✔ | **Sì** | B/KB/MB/GB (base 1024) |
| Data creazione | ✔ | ✔ | **Sì** | relativa ("più vecchio di N giorni") o assoluta |
| Data modifica | ✔ | ✔ | **Sì** | |
| Data ultimo accesso | ✔ ("Date Opened") | ✖ | **Sì** | |
| Data scatto (EXIF) | ✔ (abbrev.) | ✖ | **Sì** | |
| Tipo file (categoria) | ✖ | ✖ | **Sì** | Immagini, Video, Audio, Documenti, Archivi, Codice, Eseguibili |
| Attributi (nascosto, sola lettura, sistema, archivio) | ✔ | ✖ | **Sì** | |
| Metadati EXIF / ID3 / proprietà documento | ~ (solo come abbreviazioni) | ✖ | **Sì** | via Windows Property System, qualsiasi `System.*` |
| Contenuto testuale | ✔ | ✔ (docx, pdf, testo) | **Sì** | testo, docx/xlsx/pptx/odt, PDF best-effort |
| Liste di parole / dizionari | ✖ | ✔ | **No** | coperto da regex `a|b|c` + `{match:N}`; superfluo per la v1 |
| "Included files" per cartelle | ✔ | ✖ | **No** | EO lavora sui file; le cartelle trascinate vengono espanse |

## Azioni

| Funzione | DropIt | File Juggler | EO | Note EO |
|---|---|---|---|---|
| Sposta | ✔ | ✔ | **Sì** | crea cartelle mancanti |
| Copia | ✔ | ✔ | **Sì** | |
| Rinomina con variabili | ✔ | ✔ | **Sì** | token `{...}` |
| Cambia estensione | ✖ | ✔ | **Sì** | tramite rinomina (`{name}.nuova`) |
| Elimina nel Cestino | ✔ | ✔ | **Sì (default)** | |
| Elimina definitivamente | ✔ | ✔ | **Sì, con conferma** | in automatico ricade sul Cestino |
| Cancellazione sicura (DoD) | ✔ | ✖ | **No** | non annullabile, fuori scope |
| Rimuovi cartella se vuota | ✖ | ✔ | **Sì** | opzione su sposta/elimina |
| Mantieni struttura sottocartelle | ✔ (`%SubDir%`) | ✔ | **Sì** | token `{subdir}` |
| Comprimi ZIP | ✔ (ZIP/7Z/SFX) | ✖ | **Sì (ZIP)** | |
| Estrai archivio | ✔ (molti formati) | ✔ (zip, rar) | **Sì (ZIP)** | protezione zip-slip |
| Crea collegamento | ✔ | ✖ | **Sì** | `.lnk` |
| Apri con applicazione | ✔ | ~ (Run command) | **Sì** | eseguibile + argomenti con token |
| Ordina in sottocartelle (data/tipo/nome/estensione) | ~ (via abbreviazioni) | ~ (via variabili) | **Sì** | azione dedicata |
| Gestione conflitti rinomina/sovrascrivi/salta | ~ | ✔ | **Sì** | sovrascrivi = il file esistente va nel Cestino |
| Split / Join / Encrypt / Decrypt | ✔ | ✖ | **No** | superfluo |
| Stampa, Upload FTP, Email, Evernote | ✔ / ✖ | ✖ / ✔ | **No** | vincolo: nessun cloud/rete |
| Gallery, liste, playlist, clipboard | ✔ | ✖ | **No** | superfluo |
| Modifica proprietà (date/attributi) | ✔ | ✖ | **No** | superfluo |
| Esegui comando arbitrario | ✖ | ✔ | **No** | rischio sicurezza; "Apri con" copre l'uso tipico |
| Ignora | ✔ | ✖ | **No** | coperto da "nessuna azione"/ordine regole |

## Token di rinomina / variabili

| Funzione | DropIt | File Juggler | EO |
|---|---|---|---|
| Nome, estensione, nome completo | ✔ | ✔ | **Sì** `{name}` `{ext}` `{fullname}` |
| Cartella / nome cartella padre / unità | ✔ | ✔ | **Sì** `{folder}` `{parent}` `{drive}` |
| Sottocartella relativa | ✔ | ✔ | **Sì** `{subdir}` |
| Data corrente con formato | ✔ | ✔ | **Sì** `{date:yyyy-MM-dd}` |
| Data creazione/modifica/accesso | ✔ | ✔ | **Sì** `{created:fmt}` `{modified:fmt}` `{accessed:fmt}` |
| Data scatto foto | ✔ | ✖ | **Sì** `{taken:fmt}` (ripiega su data modifica) |
| Contatore | ✔ | ~ (`_2`) | **Sì** `{counter}` `{counter:3}` |
| Dimensione | ✔ | ✖ | **Sì** `{size}` `{bytes}` |
| EXIF (fotocamera, ISO, dimensioni…) | ✔ | ✖ | **Sì** `{exif:CameraModel}` … |
| ID3 (artista, album, titolo…) | ✔ | ✖ | **Sì** `{id3:Artist}` … |
| Proprietà documento (autore, titolo…) | ✔ | ✖ | **Sì** `{doc:Author}` … |
| Qualsiasi proprietà Windows | ✖ | ✖ | **Sì** `{prop:System.Photo.FNumber}` |
| Testo estratto dal contenuto (regex) | ✔ (match 1-9) | ✔ | **Sì** `{match:N}` (da regex su nome/contenuto) |
| Hash (MD5/SHA1) | ✔ | ✖ | **Sì** `{hash:md5}` `{hash:sha1}` `{hash:sha256}` |
| Variabili d'ambiente / cartelle di sistema | ✔ | ✖ | **Sì** `{env:X}` `{desktop}` `{documents}` `{downloads}` `{pictures}` `{music}` `{videos}` |
| Categoria tipo file | ✖ | ✖ | **Sì** `{category}` |
| Filtri maiuscolo/minuscolo/default | ✖ | ✖ | **Sì** `{name|upper}` `{id3:Artist|default:Sconosciuto}` |
| Input utente durante il processo | ✔ | ✖ | **No** | incompatibile con l'automazione in background |
| Abbreviazioni personalizzate | ✔ | ✖ | **No** | superfluo |

## Trigger

| Funzione | DropIt | File Juggler | EO |
|---|---|---|---|
| Drag & drop su icona flottante | ✔ | ✖ | **Sì** |
| Drag & drop sulla finestra principale | ✖ | ✖ | **Sì** |
| Monitoraggio cartelle in tempo reale | ~ (scansione a intervallo) | ✔ | **Sì** (FileSystemWatcher + attesa file stabile) |
| Scansione a intervallo | ✔ | ✖ | **Sì** |
| Pianificazione giornaliera a orario | ✖ | ✖ | **Sì** |
| Scansione su richiesta | ✔ | ✖ | **Sì** |
| Includi sottocartelle / escludi cartelle | ~ | ✔ | **Sì** |
| Menu contestuale Explorer | ✖ | ✖ | **Sì** |
| Invia a (SendTo) | ✔ | ✖ | **Sì** (installer) |
| Riga di comando | ✔ | ✖ | **Sì** |
| Ignora file in download | ✔ | ✔ | **Sì** (`.crdownload`, `.part`, `~$*` …) |

## Profili, sicurezza, UI

| Funzione | DropIt | File Juggler | EO |
|---|---|---|---|
| Profili di regole | ✔ | ✖ | **Sì** |
| Import / export | ✔ | ✖ | **Sì** (JSON) |
| Ordine / priorità regole | ✔ | ✔ | **Sì** |
| Crittografia profili con password | ✔ | ✖ | **No** | nessuna credenziale |
| Anteprima / simulazione | ✖ | ~ (preview variabili) | **Sì** |
| Annulla ultima operazione | ✖ | ✖ | **Sì** |
| Log attività | ✔ | ✔ | **Sì** (esportabile CSV/TXT) |
| Tray icon | ✔ | ✔ | **Sì** |
| Avvio con Windows | ✔ | ✔ | **Sì** |
| Notifiche | ~ | ✔ | **Sì** |
| Multilingua | ✔ (20+) | ✖ | **Sì** (IT/EN) |
| Tema chiaro/scuro | ✖ | ✖ | **Sì** (+ alto contrasto di sistema) |
| Editor visuale SE…ALLORA | ✖ | ✔ | **Sì** |
| Wizard primo avvio con preset | ✖ | ✖ | **Sì** (Download, Foto, Documenti, Musica) |
| Statistiche | ✖ | ✖ | **Sì** |
| Aggiornamento automatico | ✔ | ✔ | **No** | vincolo: nessuna rete |
| Versione portable | ✔ | ✖ | **Sì** (zip) |
| Unicode / percorsi lunghi / UNC | ✔ | ✔ | **Sì** |
