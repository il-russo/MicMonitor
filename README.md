# Mic Monitor

Piccola app Windows per **ascoltare il proprio microfono in tempo reale** nelle cuffie.

Un solo file `.exe` da ~900 KB, nessuna installazione, nessun driver audio virtuale.

![Interfaccia di Mic Monitor](design/screenshot.png)

## Perché non peggiora l'audio che sentono gli altri

Le app di voice changing installano un **driver audio virtuale**: tutti i programmi
(Discord, giochi, OBS) passano attraverso quel driver, quindi qualità e carico CPU
dipendono da lui, anche quando non stai usando nessun effetto.

Mic Monitor non installa niente. Apre il microfono in **modalità WASAPI condivisa**
e manda il segnale all'uscita scelta. Discord e gli altri programmi continuano a
leggere il microfono reale, esattamente come quando l'app è chiusa.

## Download e utilizzo

Scarica lo zip dalla pagina [Releases](../../releases), poi:

1. Tasto destro sullo zip → **Proprietà** → spunta **Annulla blocco** → OK.
   Windows marchia i file scaricati da internet; togliendo il blocco qui
   SmartScreen non avvisa sull'exe estratto.
2. Estrai `MicMonitor.exe` dove vuoi ed eseguilo.
   Se salti il passo 1 compare *"Windows ha protetto il PC"*: è normale per un
   programma senza firma digitale — **Ulteriori informazioni** → **Esegui comunque**.
3. Scegli **Source** (il microfono) e **Destination** (dove ascoltare).
   **Usa le cuffie**: con gli altoparlanti si innesca il fischio da feedback.
4. Premi **AVVIA ASCOLTO**.

### Controlli

| Controllo | Cosa fa |
|---|---|
| Monitor level | 0–200 %. Cambia solo quello che senti tu, non quello che sentono gli altri. |
| Latency | 5–120 ms. Più bassa = più immediato; se senti scricchiolii alzala (25–40 ms va bene quasi sempre). Cambiarla riavvia l'ascolto. |
| Noise gate | Silenzia l'ascolto sotto la soglia indicata, per non sentire il rumore di fondo. `OFF` lo disattiva. |
| Input level | Meter a 24 segmenti con peak hold. Teal e lime OK, ambra alto, rosso sta clippando. |
| Rescan | Rilegge l'elenco dei dispositivi, se colleghi cuffie o microfoni mentre l'app è aperta. |
| Auto-start | Fa partire il monitoraggio all'apertura dell'app. |
| Avvio con Windows | Aggiunge l'app all'avvio, partendo direttamente nell'area di notifica. |

Il tasto **–** minimizza normalmente. Chiudendo con la **X** mentre l'ascolto è attivo
l'app resta nell'area di notifica: doppio clic sull'icona per riaprirla, tasto destro
per fermare l'ascolto o uscire.

Le impostazioni si salvano in `%APPDATA%\MicMonitor\settings.ini`.

## Requisiti

Windows 10 o 11 con .NET Framework 4.x (già incluso in Windows).

## Come funziona

```
WasapiCapture (shared)
  └─ BufferedWaveProvider          coda con protezione da drift dei clock
       └─ MonoDownmixProvider      da N canali a mono
            └─ MonitorProcessor    guadagno, noise gate, misura del picco
                 └─ WdlResampling  solo se le frequenze non coincidono
                      └─ MonoSpread → WasapiOut (shared, event driven)
```

Entrambi i client WASAPI sono in **shared mode**: l'app non prende il possesso
esclusivo del microfono, quindi Discord può usarlo contemporaneamente.

## Compilare

Non serve Visual Studio né il .NET SDK: si usa il compilatore C# incluso in Windows.

```powershell
git clone https://github.com/<utente>/MicMonitor.git
cd MicMonitor
.\build\fetch-naudio.ps1   # scarica NAudio 1.10.0 da NuGet
.\build\build.ps1          # produce MicMonitor.exe nella radice
```

### Struttura

| Percorso | Contenuto |
|---|---|
| `src/MicMonitor.cs` | Tutta l'app: controlli disegnati a mano, motore audio, impostazioni. |
| `src/AssemblyInfo.cs` | Nome, descrizione e versione mostrati da Windows. |
| `src/app.manifest` | DPI awareness e stile dei controlli. |
| `design/skin.html` | La specifica grafica "rack unit": palette, tipografia, stati. Aprila nel browser. |
| `build/make_icon.py` | Genera `build/MicMonitor.ico` senza dipendenze (Python 3). |
| `build/EngineTest.cs` | Test da console della catena audio: elenca i dispositivi, registra 3 s in muto e riporta i byte transitati. |

## Licenza

MIT — vedi [LICENSE](LICENSE). Incorpora [NAudio](https://github.com/naudio/NAudio) 1.10.0, anch'esso MIT.
