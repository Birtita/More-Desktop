# Desktop per Desktop

Una cartella Desktop diversa per ogni desktop virtuale di Windows 11.

Windows permette di avere più desktop virtuali, ma tutti mostrano le stesse icone, perché puntano tutti alla stessa cartella `Desktop`. Con questa app puoi assegnare a ogni desktop virtuale una cartella sua. Quando cambi desktop, cambiano anche le icone.

## Funzionalità

- Una cartella per ogni desktop virtuale. I desktop senza una cartella assegnata usano il Desktop originale.
- Le posizioni delle icone vengono salvate e ripristinate separatamente per ogni cartella.
- Il cambio viene anticipato: con **Win+Ctrl+←/→** e con lo **swipe a quattro dita** sul touchpad, la cartella cambia già durante l'animazione.
- Un'icona nell'area di notifica mostra il numero del desktop in cui ti trovi.
- Si installa per l'utente corrente, senza permessi di amministratore. Compare nel menu Start e tra le app installate, e l'avvio automatico si attiva o disattiva da Impostazioni → App → Avvio o da Gestione attività.
- Quando chiudi l'app, il Desktop originale viene ripristinato.

## Requisiti

- Windows 10 o 11
- .NET Framework 4.x, già incluso in Windows. Non serve installare nessun SDK.

## Compilare

Fai doppio clic su `build.bat`. Usa il compilatore C# incluso in Windows (`csc.exe` di .NET Framework) e crea `DesktopPerDesktop.exe`. Se `app.ico` non c'è, lo rigenera con `make-icon.ps1`.

## Installare

Dopo aver compilato, apri un terminale nella cartella ed esegui:

```
DesktopPerDesktop.exe --install
```

L'installazione:
- copia l'app in `%LOCALAPPDATA%\Programs\DesktopPerDesktop`;
- crea il collegamento nel menu Start;
- registra l'app tra le app installate, con la disinstallazione;
- attiva l'avvio automatico;
- avvia l'app.

> **Nota:** esegui l'installazione da un normale Esplora risorse o terminale. Se la lanci da un'app "pacchettizzata" (per esempio da un terminale dentro un'app dello Store), Windows può isolare le voci di registro e l'app non compare tra quelle installate.

Per aggiornare un'installazione esistente, chiudi l'app (tasto destro sull'icona → *Esci*), ricompila ed esegui di nuovo `--install`.

## Usare

- Fai doppio clic sull'icona nell'area di notifica, oppure tasto destro → *Configura cartelle...*.
- Assegna una cartella a ogni desktop con *Scegli...*, oppure usa *Crea cartelle automaticamente*: verranno create in `%USERPROFILE%\Desktops\`.
- Non scegliere cartelle che si trovano dentro il Desktop originale.

## Disinstallare

Vai in Impostazioni → App → App installate → *Desktop per Desktop* → Disinstalla.
In alternativa esegui `DesktopPerDesktop.exe --uninstall`.

La disinstallazione ripristina il Desktop originale. Le cartelle che hai assegnato ai desktop e i file al loro interno non vengono toccati.

## File di dati

Sono in `%APPDATA%\DesktopPerDesktop\`:

| File | Contenuto |
|---|---|
| `config.txt` | Desktop originale, cartella assegnata a ogni desktop (GUID del desktop virtuale) e verso dello swipe del touchpad |
| `layout.txt` | Posizioni delle icone per ogni cartella |
| `debug.log` | Tempi di ogni cambio di desktop, per la diagnostica |

## Come funziona

- **Desktop corrente:** l'app legge `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops` (`CurrentVirtualDesktop`, `VirtualDesktopIDs`) e riceve una notifica quando cambia, tramite `RegNotifyChangeKeyValue`.
- **Cambio di cartella:** usa `SHSetKnownFolderPath(FOLDERID_Desktop)` e poi aggiorna il desktop (`SHChangeNotify` + F5 sulla vista del desktop).
- **Posizioni delle icone:** usa `IShellWindows` → `IShellBrowser` → `IFolderView` (`GetItemPosition` / `SelectAndPositionItems`).
- **Tastiera:** un hook a basso livello (`WH_KEYBOARD_LL`) riconosce solo Win+Ctrl+Freccia.
- **Touchpad:** usa Raw Input HID (Digitizer / Touch Pad, `RIDEV_INPUTSINK`) per riconoscere lo swipe orizzontale a quattro dita.

## Debug

```
DesktopPerDesktop.exe --dump-layout [file]
```

Scrive in un file le posizioni attuali delle icone del desktop, senza cambiare nulla.
