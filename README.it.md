<p align="center">
  <img src="assets/logo-256.png" width="128" height="128" alt="Logo di Explorer Organizer">
</p>

<h1 align="center">Explorer Organizer</h1>

<p align="center"><b>Cartelle in ordine da sole: regole SE … ALLORA per i tuoi file, con anteprima e annulla in un clic.</b></p>

<p align="center">
  <a href="https://github.com/giovanniluci/explorer-organizer/releases/latest"><img src="https://img.shields.io/badge/%E2%AC%87%20Scarica-Windows%20x64-1565C0?style=for-the-badge" alt="Scarica per Windows"></a>
</p>

<p align="center"><a href="README.md">English</a> · Italiano</p>

Organizzatore di file per Windows 10/11 basato su regole **SE … ALLORA**. Unisce le funzioni migliori di
DropIt (drag & drop su icona flottante, profili, abbreviazioni) e File Juggler (monitoraggio in tempo reale,
condizioni annidate, contenuto dei documenti), con in più anteprima, annulla e cestino di default.

Tutto resta sul PC: nessun account, nessun cloud, nessuna telemetria. Confronto dettagliato delle funzioni:
[docs/feature-matrix.md](docs/feature-matrix.md).

---

## Installazione

| Pacchetto | Uso |
|---|---|
| `ExplorerOrganizer-Setup.exe` | Installer. Di default installa **solo per l'utente corrente** (nessun permesso di amministratore); nella prima schermata si può scegliere "per tutti gli utenti". Opzioni: icona sul desktop, menu contestuale di Explorer, "Invia a", avvio con Windows. |
| `ExplorerOrganizer-Portable.zip` | Versione portable: estrai e avvia `ExplorerOrganizer.exe`. Il file `portable.txt` fa salvare le impostazioni nella sottocartella `data\` accanto all'exe. |
| `SHA256SUMS.txt` | Checksum SHA-256. Verifica con `Get-FileHash <file> -Algorithm SHA256`. |

L'exe è *self-contained*: non serve installare .NET. Requisiti: Windows 10 1809+ o Windows 11, x64.

**Avviso SmartScreen:** gli eseguibili non sono ancora firmati digitalmente, quindi al primo avvio Windows può mostrare
"PC protetto da Windows". Clicca **Ulteriori informazioni → Esegui comunque**, oppure verifica prima il checksum.

**Disinstallazione:** da *Impostazioni → App*. Rimuove programma, scorciatoie, voci del menu di Explorer e
dell'avvio automatico; chiede se eliminare anche impostazioni/profili/registri (`%AppData%\ExplorerOrganizer`).
I file organizzati non vengono mai toccati.

---

## Interfaccia

Finestra compatta (450×660, ridimensionabile) con barra del titolo personalizzata, angoli arrotondati e materiale
Mica su Windows 11 (su Windows 10 sfondo azzurro tenue). A sinistra il **rail di navigazione** (icona + etichetta,
voce attiva con barra blu): **Home, Regole, Cartelle, Cronologia, Toolbox** in alto; **Ripristina, Impostazioni,
Feedback** in basso. Scorciatoie `Ctrl+1` … `Ctrl+8`.

- **Home**: banner con il suggerimento del momento (file in attesa, monitoraggio in pausa, simulazione attiva…),
  card **Organizza** con gli indicatori *file in attesa* e *regole attive*, link *Imposta modo automatico* e il
  pulsante **Organizza ora**; sotto, quattro card: Regole, Monitor cartelle, Simulazione (anteprima), Cronologia.
- **Regole** / **Cartelle**: le regole e le cartelle monitorate del profilo attivo (interruttore per attivarle,
  "Quando" modificabile direttamente nella card della cartella).
- **Cronologia**: il registro attività filtrabile ed esportabile. **Ripristina**: il registro degli annullamenti.
- **Toolbox**: profili, preset pronti, importazione/esportazione JSON, icona flottante.
- **Feedback**: nessun dato viene inviato; il messaggio si salva in un file o si copia negli appunti.

Tema chiaro/scuro automatico (o scelto in Impostazioni), italiano/inglese. Schermate: [docs/screenshots](docs/screenshots).

## Guida rapida

1. **Primo avvio** – la procedura guidata propone lingua (IT/EN), tema (chiaro/scuro/sistema) e i preset
   *Download*, *Foto*, *Documenti*, *Musica*. Il livello di automazione consigliato è "su richiesta con anteprima".
2. **Organizzare** – in uno di questi modi:
   - trascina file o cartelle sulla finestra o sull'**icona flottante** (sempre in primo piano);
   - clic destro in Explorer → **Organizza con Explorer Organizer** (su Windows 11: *Mostra altre opzioni*), oppure *Invia a*;
   - **Organizza ora** (Home, pagina Cartelle o icona di notifica);
   - automaticamente: cartelle monitorate **in tempo reale**, **a intervalli** o **ogni giorno** a un orario.
3. **Anteprima** – prima di applicare vedi esattamente cosa succederà (origine → destinazione, regola, note) e puoi
   escludere singoli file.
4. **Annulla** – ogni esecuzione è annullabile (pagina Ripristina o menu dell'icona di notifica), anche dopo un riavvio.

### Profili e regole

Un **profilo** contiene regole e cartelle monitorate; il *profilo attivo* si usa per drag & drop e menu di Explorer.
Le regole sono valutate **dall'alto verso il basso: vince la prima che corrisponde**. Profili importabili/esportabili
in JSON (*Profili e regole → Importa/Esporta*). Un profilo importato non diventa attivo da solo e le sue cartelle e regole partono disattivate.

**Condizioni** (combinabili con gruppi TUTTE/ALMENO UNA annidati e NON su ogni condizione):
nome/percorso (è, contiene, inizia, finisce, wildcard `*` `?`, regex), estensione, tipo di file (immagini, video,
musica, documenti, archivi, codice, programmi), dimensione, data di creazione/modifica/accesso/scatto (relativa o
assoluta), attributi, metadati (EXIF, ID3, proprietà documento, qualsiasi proprietà `System.*` di Windows),
contenuto testuale (txt e simili, docx/xlsx/pptx, odt, rtf, PDF testuali).

**Azioni** (eseguite in sequenza): sposta, copia, rinomina, ordina in sottocartelle (per data/tipo/estensione/
iniziale), elimina (Cestino; *definitivo* solo con conferma), comprimi ZIP, estrai ZIP, crea collegamento, apri con.

**Conflitti** se la destinazione esiste: *tieni entrambi* (`nome (2).ext`, default), *sostituisci* (il vecchio va nel
Cestino), *salta*.

### Token per nomi e percorsi

| Token | Significato |
|---|---|
| `{name}` `{ext}` `{fullname}` | nome senza estensione, estensione, nome completo |
| `{folder}` `{parent}` `{subdir}` `{drive}` | cartella del file, nome cartella padre, sottocartella relativa alla cartella monitorata, unità |
| `{date:yyyy-MM-dd}` `{time}` | data/ora corrente (formati .NET) |
| `{created:…}` `{modified:…}` `{accessed:…}` `{taken:…}` | date del file; `taken` = data di scatto EXIF o, in mancanza, modifica |
| `{counter}` `{counter:3}` | contatore progressivo (con zeri iniziali) |
| `{size}` `{bytes}` `{category}` | dimensione leggibile, in byte, categoria del tipo |
| `{exif:CameraModel}` `{id3:Artist}` `{doc:Author}` `{prop:System.Photo.FNumber}` | metadati |
| `{match:1}` `{match:nome}` | gruppo catturato da una condizione regex su nome o contenuto |
| `{hash:md5}` `{hash:sha1}` `{hash:sha256}` | impronta del file |
| `{desktop}` `{documents}` `{downloads}` `{pictures}` `{music}` `{videos}` `{home}` `{env:VAR}` `%VAR%` | cartelle e variabili di sistema |

Filtri: `{name|upper}`, `|lower`, `|title`, `|trim`, `|left:N`, `|default:testo`. Nei formati data `/` crea
sottocartelle: `{taken:yyyy/MM}` → `2026\10`. `{{` e `}}` producono graffe letterali. I percorsi relativi partono
dalla cartella del file.

Esempi: `{pictures}\{taken:yyyy/MM}` · `{modified:yyyy-MM-dd} {name}.{ext}` ·
`{music}\{id3:Artist|default:Sconosciuto}\{id3:Album}` · `Fattura {match:1}.{ext}`

### Sicurezza dei file

- **Anteprima/simulazione**: anteprima prima di ogni azione manuale; *modalità simulazione* per le regole automatiche
  (registrano solo cosa farebbero).
- **Mai perdite silenziose**: l'eliminazione va nel Cestino; "sostituisci" sposta nel Cestino il file esistente;
  dove il Cestino non esiste (rete, unità rimovibili) eliminazione e sostituzione vengono **rifiutate**.
  L'eliminazione definitiva richiede conferma esplicita; le esecuzioni automatiche usano sempre il Cestino.
- **Annulla** basato su un registro persistente (ultime 50 esecuzioni); non rimuove copie che hai modificato dopo.
- **File in uso / download in corso**: il monitoraggio attende che il file sia stabile e non bloccato; i file in uso
  vengono saltati e segnalati; `*.crdownload`, `*.part`, `~$*`… sono ignorati (configurabile).
- Percorsi lunghi (> 260 caratteri), Unicode, permessi negati e UNC gestiti; protezione *zip-slip* nell'estrazione.
- **Registro attività** filtrabile ed esportabile in CSV/TXT (`%AppData%\ExplorerOrganizer\logs`, 60 giorni).

### Riga di comando

```
ExplorerOrganizer.exe [--minimized] [--show] [--scan] [--process] [percorsi...]
```

Una seconda istanza inoltra gli argomenti a quella già in esecuzione (più file selezionati in Explorer vengono
elaborati insieme).

### Accessibilità

Navigazione completa da tastiera (Tab, frecce nel rail, `Ctrl+1…8`, Invio/Esc nei dialoghi), nomi e AutomationId
accessibili su rail, card e pulsanti a icona, contrasto dei colori WCAG AA verificato dai test in entrambi i temi,
contrasto elevato di Windows rispettato automaticamente.

---

## Build

Struttura: `src/ExplorerOrganizer.Core` (motore, nessuna UI), `src/ExplorerOrganizer.App` (WPF), `tests/` (xUnit:
`ExplorerOrganizer.Tests` per il motore, `ExplorerOrganizer.UiTests` per l'interfaccia), `installer/setup.iss`
(Inno Setup 6), `docs/` (matrice funzioni, matrice test, schermate), `assets/` (logo e immagini).

Requisiti: Windows 10/11 x64, PowerShell 5.1+. Il .NET 8 SDK e Inno Setup 6 possono essere installati **localmente
nella cartella `.tools\`** (nessuna modifica di sistema) con `-BootstrapTools`:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -BootstrapTools
```

Se .NET SDK 8+ e Inno Setup 6 sono già installati nel sistema:

```powershell
.\build.ps1
```

Output:

- `artifacts\publish\ExplorerOrganizer.exe` – single-file self-contained win-x64
- `artifacts\ExplorerOrganizer-Portable.zip`
- `installer\Output\ExplorerOrganizer-Setup.exe`
- `docs\test-report.txt` (output di tutti i test, non versionato) e `docs\screenshots\*.png`

Ordine: test del motore → exe → test UI sull'exe pubblicato (avviano l'app con una cartella dati isolata e la
pilotano con UI Automation: serve un desktop interattivo) → zip portable → installer.
Opzioni: `-SkipTests`, `-SkipUiTests`, `-SkipInstaller`, `-Configuration Debug`, `-Verify` (installa e disinstalla
in silenzio in una cartella temporanea e controlla che non resti nulla; avvia la versione portable senza installarla).
`-Verify` si ferma se Explorer Organizer è già installato per l'utente, per non sostituire quell'installazione.
Solo test: `dotnet test tests\ExplorerOrganizer.Tests` e `dotnet test tests\ExplorerOrganizer.UiTests`.
Mappa caso → test: [docs/test-matrix.md](docs/test-matrix.md).

La versione è in `Directory.Build.props`. L'icona si rigenera con `src\ExplorerOrganizer.App\Assets\make-icon.ps1`.
Gli eseguibili non sono firmati digitalmente: per distribuirli senza avvisi SmartScreen firmali con il tuo certificato
(`signtool sign /fd SHA256 /tr <timestamp-url> /td SHA256 <file>`) prima e dopo la compilazione dell'installer.

## Contribuire

Segnalazioni, idee per regole e pull request sono benvenute: vedi [CONTRIBUTING.md](CONTRIBUTING.md).
Le vulnerabilità vanno segnalate in privato: vedi [SECURITY.md](SECURITY.md).

## Licenza

MIT – vedi [LICENSE](LICENSE).
