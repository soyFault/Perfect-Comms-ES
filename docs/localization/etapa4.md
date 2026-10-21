# Etapa 4 — Opciones del host, atajos y ayuda de configuración

**Estado: hecha en `claude-local`.** Verificado (.NET 10): `PerfectComms.csproj` Release 0 warnings / 0 errores · `PerfectComms.Tests` **1308/1308** (eran 1302; +6) ·
`PerfectComms.Starlight` compila (0 warnings, incluye los bloques `#if ANDROID`) · `scripts/localization.py --check` al día · verificadores de release OK.
XML: **593 claves** (`en_US`/`es_419`/`es_ES`), 0 sin traducir. Probado con pruebas unitarias; no dentro del juego (Etapa 6).

> **Nota sobre `PerfectComms.Starlight.Tests`**: en este entorno hay 2 pruebas sensibles a tiempos (`TwoManagedEnginesNegotiateAndPlayOpusBidirectionally`, `MobileClientReapsATimedOutEngine…`) que fallan de forma intermitente (≈ 3 de cada 8 corridas) **también en el commit anterior sin ningún cambio de esta etapa** (comprobado con `git stash`). No están relacionadas con la localización; 107/107 cuando pasan.

## Qué se localizó
| Qué | Dónde | Cómo |
|---|---|---|
| **Nombre y ayuda de los 13 atajos** | `Comms/VoiceChatKeybinds.cs`, `Comms/VoiceKeybind.cs` | `VoiceKeybind.Localized(config, sección, Loc.L(nombre), Loc.L(ayuda), …)`. El nombre en inglés sigue siendo la clave persistida del `.cfg` (`configKey` explícita para `Hold To Mute` y `Toggle Speaker`); `DisplayName`/`HelpText` ahora se resuelven al leerlos. `LocalizationIdentifierTests` (Etapa 0) sigue fijando los 13 ids. |
| **Opciones del host** (26) | `Comms/VoiceChatGameOptions.cs`, `Comms/HostOptionHolders.cs` | Etiqueta, ayuda y opciones de los selectores con `Loc.L`. `OptionHolder.Label/HelpText` pasaron a resolverse al leer; los constructores con `string` (terceros) siguen existiendo. `EnumHolder` añade `LabelCount`/`LabelAt(i)` (el panel ya no asigna un arreglo por fotograma). |
| **Ayudas de los ajustes locales** (41) | `Comms/VoiceChatLocalSettings.cs`, `Comms/ConfigHelp.cs` (nuevo) | `config.Bind(..., ConfigHelp.D(Loc.L("help.x", "English"), rango))`: el `.cfg` conserva **siempre la descripción en inglés**, y `SettingHelp(entry)` del panel busca su traducción por el texto inglés. |
| Nombres de dispositivo | `Comms/VoiceChatLocalSettings.cs` | "Default", "Saved microphone/speaker" y el sufijo "(unavailable)" se traducen **solo al mostrarlos** (`DeviceNames`); `VoiceDeviceInfo.Name` sigue en inglés porque se persiste y se compara. La lista se reconstruye sola si cambia el idioma. |
| Textos por defecto de opciones de mods | `Comms/ModOptionHolders.cs` | Las ayudas genéricas ("Enables or disables this host option…") se traducen; las etiquetas/ayudas que aporta cada mod **no** (`Loc.LocText.Plain`). |

## Decisiones
- **`.cfg` en inglés siempre** (lo pediste "a tu criterio"): las descripciones se escriben una vez al crear el archivo y lo edita gente avanzada; así no queda un `.cfg` con el idioma del primer arranque. El panel sí muestra la ayuda traducida.
- **No se localizaron** las descripciones de Debug, Lobby Browser, TURN, Updates, `*.Internal`, umbrales avanzados y claves retiradas: no aparecen en la interfaz.
- `ToggleDeafenDisplayName`/`ToggleDeafenHelpText` (`const`) se conservan porque un test los fija; una prueba nueva comprueba que coinciden con el inglés localizado.
- Las 6 opciones/ayudas que el rediseño añadió (chat de impostores) y las 6 ayudas genéricas son traducciones nuevas: **revísalas**. El resto viene de `main`.

## Tests nuevos (`LocalizationTests`)
Atajos (nombre/ayuda traducidos, `Id` intacto), constantes de deafen = inglés localizado, holders del host (etiqueta, opciones, ayuda; `Id` estable), texto de terceros sin traducir, `.cfg` en inglés pero ayuda del panel traducida, y nombres de dispositivo (solo visual).

## Pendiente
- **Etapa 5**: `AndroidVoiceUiPolicy`, lobby browser, notificador de actualización, `PingTrackerPatch`, `VanillaLobbyPublicApiClient`.
- **Etapa 6**: refresco al cambiar el idioma con paneles abiertos; prueba en juego.
- Las etiquetas de `VoiceModRegistry` (mods de terceros) y su descripción en el `.cfg` ("Host option from …") se dejan tal cual.

## Cómo llevarlo a `new-spanish`
`git cherry-pick <commit-etapa-4>` o `git diff c5ccced..HEAD -- Comms Resources PerfectComms.Tests`. Archivos nuevos: `Comms/ConfigHelp.cs`. Luego `python3 scripts/localization.py` y `dotnet test`.
