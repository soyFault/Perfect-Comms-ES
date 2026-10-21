# Etapa 0 — Separar identificadores de textos visibles

**Estado: hecha en `claude-local`. Sin cambios de comportamiento ni de texto.**
Verificado (como en `ci.yml`, .NET 10): `PerfectComms.csproj` Release **0 warnings / 0 errores** (re-medido después de los cambios) ·
`PerfectComms.Tests` 1262 → **1277 tests, 0 fallos** (15 nuevos) · `PerfectComms.Starlight.Tests` (compila los mismos `Comms/*.cs` con `ANDROID;STARLIGHT`) restore `--locked-mode` + build 0 warnings + **107/107 tests**.
No se ejecutaron los pasos de empaquetado de CI (`package-api.sh`, `package-starlight.sh`, verificadores de release) ni el job de Windows.
Los tests no corren en paralelo (`AssemblyInfo.cs`: `DisableTestParallelization`), así que el nuevo test que llama a `VoiceChatKeybinds.Initialize` no compite con `VoiceKeybindTests`.
(Para reproducir: .NET SDK 10, `dotnet build PerfectComms.csproj -c Release` y `dotnet test PerfectComms.Tests -c Release`.)

## Convención que fijan las siguientes etapas

> **El literal en inglés que hoy se pasa a un helper/constructor es el *id estable*. Nunca se traduce ni se usa
> como texto visible directamente; el texto visible se obtiene al construir la UI con `Loc.T(clave, inglés)`.**

Esto evita tocar los ~60 call sites de `VoiceSettingsPanel` (`Slider(defs, "Mic Volume", …)`): la localización irá *dentro* de los helpers (`Slider`, `Toggle`, `EnumStep`, `Section`…), derivando la clave XML del id.

## Hallazgo principal (corrige lo que dije en el plan)

`VoiceKeybind` guarda su binding en el `.cfg` bajo **`displayName`** salvo que se use el overload con `configKey`
(solo `Push To Mute` → `"Hold To Mute"` y `Toggle Deafen` → `"Toggle Speaker"` lo usan). Es decir, **11 de 13 keybinds persisten con su texto visible como clave**.
Traducir `displayName` habría reseteado los atajos de todos los usuarios (esto es lo que pasó con la versión hardcodeada de `main`: p.ej. su clave persistida pasó a ser `"Silenciar / activar micrófono"`).

## Cambios

| Archivo | Cambio |
|---|---|
| `Comms/VoiceKeybind.cs` | Nueva propiedad `Id` = clave persistida en el `.cfg` (nunca localizada). `DisplayName` sigue igual; en la Etapa 4 pasará a resolverse con `Loc.T` sin alterar `Id`. |
| `Comms/HostOptionHolders.cs`, `Comms/ModOptionHolders.cs` | `OptionHolder.Id` (`"Sección.Clave"` en opciones propias, `"modId.optionKey"` en las de terceros). |
| `Comms/HostSettingsPanel.cs` | La firma de cambio (`Signature`) usa `Id` en vez de `Label.GetHashCode()`. |
| `Comms/VoiceSettingsPanel.cs` | La ayuda por fila ya no es un `switch` inline sobre la etiqueta: `RowHelp(key)` (interno, testeable) por *clave estable*; `Entry.Key` documentado como id; las filas de keybind usan `bind.Id` (antes `bind.DisplayName`, que iba a cambiar con el idioma). |
| `Comms/VoiceSettingsCategory.cs`, `Comms/VoiceUiKit.cs` | `VoiceSettingsCategoryText.Caption()` reemplaza `category.ToString()` en el rail: el nombre del enum también es el nombre del recurso del icono (`Audio.png`…), no debe ser texto visible. |
| `PerfectComms.Tests/LocalizationIdentifierTests.cs` | Tests de contrato: ids de los 13 keybinds fijados a las claves ya persistidas en `.cfg` reales (y que existen como entradas), `Id` ≠ `DisplayName` con overload `configKey`, ids de holders, ayuda por clave, captions. **Si alguien cambia un id, estos tests fallan antes de que se rompa la config de un usuario.** |

## Auditoría: qué se queda en inglés para siempre (no tocar en etapas siguientes)

- Claves y secciones de `config.Bind(...)`, ids de keybinds, ids de holders, nombres de iconos/recursos, `Cat_*` / `Section_*` (nombres de GameObject).
- Valores en red / lobby: `LobbyBrowserLanguage` (default `"English"`) y título, payloads RPC, `VoiceDiagnostics.Log`, `Log.*`, `[PC-UI]`, eventos tipo `voice.ui.host_panel_revoked`.
- `FirstRunAudioPreview.cs:91-125`: compara con `lower.Contains("device unavailable", …)` etc. el texto **de error en inglés que devuelve el helper nativo (Rust)**. Son datos de entrada, no UI: no traducir esas comparaciones (sí se traducen los mensajes que se *muestran* a partir de ellas). Idem `PerfectCommsVoiceBackend.cs:2072-2080`.
- Etiquetas que otros mods registran por `PerfectCommsApi` (`VoiceModRegistry`): son suyas, se muestran tal cual.
- Nombres de dispositivos de audio (los da el sistema operativo).

## Riesgos detectados que NO se arreglan aquí (quedan asignados)

1. **Etapa 5 — `VanillaLobbyBrowserPatches.cs:419-426`**: `IsHostBox`/`IsStatusBox` reconocen cajas de texto *del juego* buscando `"Player Speed"`, `"Host:"`, `"Roles"`, `"Lobby"`, `"InGame"`, `"In Game"`, `"Unknown"`. El juego ya está traducido cuando está en español, así que esa heurística **ya falla con Among Us en español** (independiente de nuestra traducción) y además, si traducimos `FormatStatusLabel` (`"In Game"`), dejaría de reconocer su propia salida. Hay que identificar las cajas por estructura (nombre de GameObject / orden / un marcador propio), no por texto. Requiere probar en juego.
2. **Etapa 4 — ayuda en los ajustes**: `SettingHelp(entry)` (`VoiceSettingsPanel.cs:974`) muestra en la UI el `ConfigDescription` del `.cfg`. Decisión: el `.cfg` conserva **siempre inglés** (se escribe una sola vez y lo editan usuarios avanzados), y la UI traduce la ayuda por id (`Loc.T("help." + sección + "." + clave, inglés)`).
3. **Etapa 2/4 — timing**: `const ToggleDeafenDisplayName` (lo fija un test en inglés), `OptionHolder.Label/HelpText` (se asignan en el constructor al cargar el plugin, antes de que exista el idioma), `StepNames` en `VoiceFirstRunSetup`, `BuiltInCategories`/`RailSections` en `HostSettingsPanel` y `Categories` en `VoiceSettingsPanel` (arrays `static readonly`; este último lo fijan tests en inglés y en Android). Se resuelven con getters/`Loc.T` al construir la UI, conservando el valor inglés como fallback.
4. **Etapa 3**: textos con la misma palabra en inglés pero traducción distinta (p.ej. `Welcome`) → clave por uso.

## Cómo llevarlo a `new-spanish`

El commit de esta etapa solo toca los archivos de la tabla. Opciones: `git cherry-pick <commit>` desde `claude-local`, o
`git diff origin/new-spanish..claude-local -- Comms PerfectComms.Tests` para recrearlo a mano. No depende de `Loc` (Etapa 1).

## Pendiente de decisión: migrar los atajos de quien usó la versión hardcodeada de `main`

Quienes instalaron builds de `main`/`old-spanish` tienen estos atajos guardados en `[Keybinds]` bajo la clave **en español**. Con ids en inglés (lo correcto) esos atajos personalizados se verían reseteados al actualizar.
Hay un patrón de migración en el repo (`RemoveRetiredHostRefreshBindings`). Propuesta (Etapa 1 o 4): en `VoiceChatKeybinds.Initialize`, antes de crear cada `VoiceKeybind`, si existe la clave legacy en el `.cfg` y la nueva no, copiar `<clave>`, `<clave> Modifier`, `<clave> Modifier Match` y `<clave> Allow While Chat Open`, y borrar las legacy. Tabla (id actual ← clave de `main`):

| Id (inglés, actual) | Clave persistida en `main` |
|---|---|
| Mute / Unmute Mic | Silenciar / activar micrófono |
| Team Radio (Hold) | Radio de equipo (mantener) |
| Cycle Team Radio Channel | Cambiar canal de radio de equipo |
| Push To Talk (Hold) | Pulsar para hablar (mantener) |
| Toggle Open Mic / Push To Talk | Alternar Micrófono abierto / Pulsar para hablar |
| Player Volumes | Volumen de jugadores |
| Alive Louder / Dead Quieter (Hold) | Vivos más alto / Muertos más bajo (mantener) |
| Alive Quieter / Dead Louder (Hold) | Vivos más bajo / Muertos más alto (mantener) |
| Refresh Voice Connection | Reconectar chat de voz |
| Open Voice Menu | Abrir menú de voz |
| Open Host Voice Settings | Abrir ajustes de voz del host |

(`Hold To Mute` y `Toggle Speaker` ya eran claves en inglés en `main`.) Las opciones del host (`Section`/`Key` de `VoiceChatGameOptions`) y `VoiceChatLocalSettings` **no** tradujeron sus claves en `main`, solo las etiquetas: no necesitan migración.
