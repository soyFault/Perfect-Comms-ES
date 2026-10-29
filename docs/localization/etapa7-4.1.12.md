# Etapa 7 - Port de la localizacion a Perfect Comms 4.1.12

Rama: `4.1.12` (sale de `claude-local`, que tenia las etapas 0-6 sobre 4.1.11, y fusiona `artriy/Perfect-Comms` `main` = v4.1.12, commit `3835ed2`).

## 1. Que trae 4.1.12 que afecta a la localizacion

Upstream rehizo gran parte de la UI (143 archivos, +13k lineas): paneles con cabecera de marca y subtitulo,
navegador de lobbies nuevo, asistente de primera vez reescrito, mezcla de volumen nueva, aviso de antivirus
(`VoiceHelperBlockedNotice`), integracion de grabacion OBS, opcion de host "Ghosts Hear Living During Tasks",
notificador de actualizaciones nuevo. Tambien pasa la mayoria de rotulos de MAYUSCULAS a "Sentence case".

## 2. Como se fusiono

`git merge upstream/main` dio 17 archivos en conflicto. Regla seguida:

* Lineas donde upstream cambio solo el texto: se toma la linea de upstream y se vuelve a envolver con `Loc.T`/`Loc.L`
  reutilizando la clave existente (el ingles de la clave pasa a ser el de upstream).
* UI rediseñada (`VoiceFirstRunSetup`, `VoiceLobbyBrowserUi`, `VoiceUiKit`, `VoiceSettingsPanel`, `VoiceVolumeMenu`):
  se toma upstream y se re-localiza literal por literal. Las claves viejas con el mismo texto reutilizan su traduccion.
* Rotulos que eran MAYUSCULAS (37 claves) pasan a "Sentence case" tambien en español.
* Sin cambios de comportamiento: todo sigue siendo `Loc.T` sobre el ingles de upstream.

## 3. Cambios de codigo propios de este port

* `LiveStatusKind` (`Connecting/Unavailable/Info`) en `VoiceLobbyLiveDirectoryClient`: el navegador decidia el estado
  con `status.StartsWith("Connecting")`, que en español nunca coincidiria. Ahora usa el enum, no el texto.
* `VoiceLobbyBrowserUi.JoinStatusText(listing, state)`: el texto del boton Unirse sale del `JoinState`, no de comparar strings.
* `VoiceLobbyBrowserUi.RefreshLanguage` ahora destruye el `PanelShell` nuevo (el menu del boton se conserva).
* `CommsChipButton.SetLabel(eyebrow, label)`: los chips de menu ahora traducen tambien la linea superior
  ("Para toda la sala · Perfect Comms" / "Personal · Perfect Comms").
* `VoiceUiKit.PanelShell.SetSubtitle`: el subtitulo de cabecera ("Configuracion inicial") sigue al idioma en el asistente.
* `LocalizationRefresh` tambien limpia el aviso de antivirus (`#if WINDOWS`).
* `VoiceUiBranding`: se mantiene el `#if STARLIGHT` que evita `UnityEngine.TextCore*` (Starlight 1.6.3 lo rechaza).
* `Starlight/DependencySanitizer/Program.cs`: se usa el hash de upstream (el nuestro era para 4.1.11).
* `Loc.MarkGameReady()` (etapa 6, seccion 7) se conserva: solo lo llaman `MainMenuManager.Start/LateUpdate`,
  `OptionsMenuBehaviour.Update` y `HudManager.Update`.

## 4. Cobertura

* `Resources/Locale/en_US.xml`: 782 claves (generado). `es_419.xml` y `es_ES.xml`: 782, mismo contenido.
* Nuevas respecto a 4.1.11: ~115 (asistente, navegador de lobbies, mezcla de volumen, OBS, aviso de antivirus,
  fantasmas oyen a vivos, notificador de actualizaciones, estados del navegador).
* Sin localizar a proposito: logs/diagnosticos, nombres de objetos de Unity, `Player` (nombre de archivo para OBS),
  descripciones de `.cfg` (en ingles, igual que antes), textos que vienen del servidor.

## 5. Verificacion

* `dotnet build PerfectComms.csproj`: 0 errores, 0 warnings.
* `dotnet test PerfectComms.Tests`: 1469 pasan, 1 omitido (incluye los tests nuevos de upstream y los de localizacion).
* `dotnet build PerfectComms.Starlight.csproj -c Release` y `scripts/package-starlight.sh`: OK (validacion exacta/estricta).
* `python3 scripts/localization.py --check`: al dia; `--audit`: 0 desajustes de marcadores.
* NO probado en el juego. Lista de comprobacion: la de `etapa6.md`, mas: asistente de primera vez completo en es_419,
  navegador de lobbies (estados "Conectando…", "Directorio no disponible", boton Unirse), mezcla de volumen,
  aviso de antivirus (renombrar `PerfectCommsAudio.exe` para provocarlo), opciones OBS en Ajustes > Audio (Windows).

## 6. Como llevarlo a `new-spanish` (cuando tu fork este en 4.1.12)

```
git fetch origin 4.1.12
git diff upstream/main...origin/4.1.12 --stat      # todo lo que añade la localizacion sobre 4.1.12
git merge origin/4.1.12                             # o recrearlo a mano siguiendo este documento
```

El orden recomendado a mano: `Comms/Loc.cs`, `LocalizationRefresh.cs`, `ConfigHelp.cs`, `Resources/Locale/*.xml`,
`scripts/localization.py` y despues archivo por archivo. `python3 scripts/localization.py` regenera `en_US.xml`.
