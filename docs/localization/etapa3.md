# Etapa 3 — Paneles (ajustes, host, asistente de primer arranque, mezclador)

**Estado: hecha en `claude-local`** (5 commits: 3a `432639c`, 3b–d `971192a`, 3e `2deae38`, 3f `92ae618`).
Verificado (.NET 10): `PerfectComms.csproj` Release 0 warnings / 0 errores · `PerfectComms.Tests` **1302/1302** (eran 1297; +5) ·
`PerfectComms.Starlight.Tests` 107/107 y build 0 warnings (compila los bloques `#if ANDROID` de estos archivos) ·
`scripts/localization.py --check` al día · `verify-release-assets.py --self-test` y `package-release.sh --validate-source` OK.
XML: **460 claves** en total (`en_US`/`es_419`/`es_ES`), 0 sin traducir. Probado con pruebas unitarias; **no dentro del juego** (Etapa 6).

## Qué se localizó

| Sub-etapa | Archivos | Notas |
|---|---|---|
| 3a | `Comms/VoiceSettingsPanel.cs`, `Comms/VoiceSettingsCategory.cs` | Filas, secciones, ayudas por fila, mensajes de la prueba de micrófono, captions del rail. |
| 3b | `Comms/HostSettingsPanel.cs` | Pestañas, sección "MOD BEHAVIOUR", título, aviso "solo anfitrión". |
| 3c | `Comms/VoiceUiKit.cs` | Cabecera/pie, títulos y subtítulos de cada página, fila de modo de micrófono, botones y textos de captura de atajos, tooltip "usar con el chat abierto". |
| 3d | `Comms/VoiceVolumeMenu.cs`, `Comms/VoiceHostMenuEntry.cs`, `Comms/VoiceOptionsMenuEntry.cs`, `Comms/FirstRunSetupDraft.cs` | Mezclador de volumen, chips de los menús del juego, los 10 presets de HUD, "Predeterminado del sistema". |
| 3e | `Comms/FirstRunAudioPreview.cs` | ~65 mensajes de estado/errores de la prueba de micrófono y de altavoz. |
| 3f | `Comms/VoiceFirstRunSetup.cs` | Todo el asistente (bienvenida, audio, controles, HUD, revisión, final, diálogo de salida). |

## Mecanismo nuevo: `Loc.L` / `Loc.LocText` (para etiquetas que se declaran antes de construir la UI)
`Loc.L("clave", "English")` devuelve un `LocText` que **se resuelve al usarlo** (`.Text`), y cuyo `.English` es el **id estable** de la fila (lo que antes era `Entry.Key`, p. ej. `"Mic Volume"`). Así:
- `Slider/Toggle/EnumStep/Action/Section` en `VoiceSettingsPanel` reciben `Loc.LocText` en vez de `string`; la `Key` sigue siendo el literal en inglés (la ayuda por fila `RowHelp(key)` y los tests de la Etapa 0 siguen valiendo).
- Los presets de HUD (`FirstRunHudPreset`) conservan `Name`/`Description` en inglés como ids (los tests los usan) y añaden `DisplayName`/`DisplayDescription` localizados; la lista `All` ya no resuelve texto al cargar la clase.
- `scripts/localization.py` y `LocalizationTests` escanean también `Loc.L(...)`.

## Cosas que NO eran texto de interfaz y se dejaron (o se revirtieron) a propósito
- Nombres de `GameObject` (`"Summary" + título`, `"Cat_*"`, `"Section_*"`…): el script de extracción los había envuelto por error y se revirtió; el título de marca "Perfect Comms" tampoco se traduce.
- **Falsos positivos en `pairs-en-es419.tsv`**: algunas "traducciones" de `main` eran nombres de objetos (`Bind`, `Reset`, `Summary`). No usar esos pares.
- La comparación `MicrophoneStatus != "Mic check is off"` del panel pasó a comparar con `FirstRunAudioPreview.MicrophoneOffStatus` (la misma cadena traducida), para no depender del texto literal.
- Se mantienen en inglés las comparaciones contra errores crudos del helper nativo (`lower.Contains("device unavailable")`, etc.) — ver etapa 0.

## Decisiones de traducción que conviene que revises
- Hay pares de `main` con el mismo inglés y traducciones distintas; se unificó: `Mic check is off` → "La prueba de micrófono está desactivada"; `Default speaker test completed…` → "Prueba de la salida predeterminada completada - ¿la escuchaste?"; `Waiting for the selected speaker...` → "Esperando la salida seleccionada...". En el selector de dispositivos "Speaker" se traduce "Salida" (como en `main`) pero "Speaker Volume" → "Volumen del altavoz".
- El resumen de HUD del asistente se reescribió con frases completas por estado (`Controles visibles/ocultos`, `Estado de conexión visible/oculto`, `Brillo de reunión visible/oculto`, `Escala N% / Fondo activado/desactivado`) para evitar concordancias raras (antes: `{ShownHidden(...)}`).
- El atajo corto "PTT" se tradujo "PPH" como en `main` (pulsar para hablar). Las etiquetas del riel `Push to talk` / `Mute mic` se acortaron a "Hablar" / "Silenciar" por el ancho (84 px).
- Textos que el rediseño añadió y `main` no tenía son traducciones nuevas (por ejemplo las de los controles táctiles, errores de guardado, ayudas de las filas) — revísalas.

## Guardas nuevas
`NoStaticFieldOrConstResolvesTextWhileThePluginLoads` (en `LocalizationTests`): falla si alguien escribe `static readonly … = Loc.T(...)` o `const … Loc.T(...)`, que congelaría el idioma al cargar el plugin. (Se probó que detecta el caso.) Otros tests: presets, captions, ayuda en español, y `LocText` resuelto al usarse.

## Pendiente que queda para otras etapas
- **Etapa 4**: el texto de las filas de **atajos** (`bind.DisplayName`) y las **opciones del host** (`OptionHolder.Label/HelpText`), y la ayuda que sale del `ConfigDescription` (`SettingHelp(entry)`) siguen en inglés. Por eso la pestaña Atajos y el panel de host muestran hoy nombres de filas en inglés.
- **Etapa 5**: `AndroidVoiceUiPolicy` (textos de los controles táctiles), lobby browser, notificador de actualización.
- **Etapa 6 (cambio de idioma en caliente)**: `PanelShell` fija su título al crearse, y los chips de los menús se construyen una sola vez, así que ya creados no cambian si el jugador cambia el idioma con el panel vivo; hay que reconstruirlos al cambiar de idioma.

## Cómo llevarlo a `new-spanish`
Por sub-etapa (`git cherry-pick <commit>`), o todo junto con `git diff 8f8d6bb..HEAD -- Comms Resources PerfectComms.Tests scripts` (los archivos del código están en la tabla). Tras cada cherry-pick: `python3 scripts/localization.py` y `dotnet test`.
