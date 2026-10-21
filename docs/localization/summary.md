# Perfect Comms: upstream (artriy) vs. tu repositorio — resumen y sistema de locales

Compara **`artriy/Perfect-Comms`** (upstream, solo inglés) con **tu versión** (`soyFault/Perfect-Comms-ES`, rama `claude-local`, lista para llevar a `new-spanish`), y explica cómo funciona el sistema de locales.
Detalle por etapa: `etapa0.md` … `etapa6.md`. Plan original: `../LOCALIZATION-PLAN.md`.

> **Punto de partida.** Al escribir esto, `main` de upstream está en `17177e1 "redesign"` (18-oct-2026), que es **exactamente** la base de tu `new-spanish`. Por eso la comparación es limpia: todo lo que difiere es lo que se añadió en `claude-local` (15 commits, 50 archivos fuera de `docs/`, +4364 / −804 líneas; la mayoría son los XML y las pruebas).

## 1. Resumen en una tabla

| | **Upstream** `artriy/Perfect-Comms` | **Tu versión** `claude-local` |
|---|---|---|
| Idiomas de la interfaz | Solo inglés (texto fijo en el C#) | Inglés + español (Latam y España), según el idioma de Among Us |
| Dónde viven los textos | Literales en el código | Inglés: literal en el código (igual que upstream). Español: `Resources/Locale/*.xml` embebidos |
| Cambio de idioma | No aplica | Automático, también con paneles abiertos |
| Jugadores con el juego en inglés | Inglés | Exactamente el mismo inglés que upstream |
| Atajos / claves del `.cfg` | Clave = nombre visible | Clave = id estable en inglés; el nombre visible se traduce (`VoiceKeybind.Id`) |
| Lobby browser de Among Us | Reconoce cajas por texto en inglés | Por estructura (`tag1`, `tag2`, `capacity`), funciona con el juego en cualquier idioma |
| Dependencias nuevas | — | Ninguna (el patrón imita a `MiraLocaleManager`, sin usar MiraAPI ni Reactor) |
| Pruebas de escritorio | 1262 | 1312 (50 de localización/identificadores) |
| Cómo se integran cambios de upstream | — | Casi todo son llamadas `Loc.T(...)` sobre el mismo literal inglés: un merge conflictúa solo donde upstream cambia esa misma línea |

## 2. Qué cambió respecto a upstream

### 2.1 Infraestructura nueva (archivos añadidos)
- `Comms/Loc.cs` — API de localización, elección de idioma, carga de XML, seguridad de hilos y detección de cambios.
- `Comms/ConfigHelp.cs` — ayudas traducidas para el panel sin tocar las descripciones del `.cfg`.
- `Comms/LocalizationRefresh.cs` — reconstruye la interfaz cacheada al cambiar el idioma.
- `Resources/Locale/en_US.xml`, `es_419.xml`, `es_ES.xml` — **667 claves** (se generan/validan con `scripts/localization.py`).
- `scripts/localization.py` — regenera `en_US.xml`, lista lo pendiente (`--audit`, `--check`).
- `PerfectComms.Tests/LocalizationTests.cs`, `LocalizationIdentifierTests.cs` — contratos y paridad.

### 2.2 Textos localizados (archivos existentes tocados)
HUD y avisos (`VoiceChatHudState`, `VoiceCompactStatusPolicy`, `VoiceConnectionStatusPolicy`, `VoiceHudWarnings`, `VoiceJoinGuard`), vista previa del HUD, paneles de ajustes / host / mezclador (`VoiceSettingsPanel`, `HostSettingsPanel`, `VoiceVolumeMenu`, `VoiceUiKit`), asistente de primer arranque (`VoiceFirstRunSetup`, `FirstRunSetupDraft`, `FirstRunAudioPreview`), atajos y opciones del host (`VoiceChatKeybinds`, `VoiceKeybind`, `VoiceChatGameOptions`, `HostOptionHolders`, `ModOptionHolders`, `VoiceChatLocalSettings`), lobby browser y directorio en vivo, notificador de actualización, controles de Android.

### 2.3 Cambios de comportamiento (no solo texto)
| Qué | Por qué |
|---|---|
| Los atajos separan **id** (clave del `.cfg`) y **nombre visible** | Traducir el nombre habría reseteado los atajos de todos (el `.cfg` los guardaba bajo el nombre visible). Un test fija las 13 claves actuales. |
| La ayuda de cada fila se busca por id, no por la etiqueta | Una etiqueta traducida nunca coincidiría con el `switch`. |
| El botón *Entrar* del lobby browser usa un estado interno | Comparaba su texto con `"JOIN"`. |
| Lobby browser de Among Us por acceso tipado | Upstream buscaba *campos* por reflexión (en el interop actual son *propiedades*, siempre `null`) y caía a una heurística de texto en inglés. |
| Textos que antes se fijaban al cargar el plugin ahora se resuelven al construir la UI | El idioma aún no existe en `Load`. Un test prohíbe `Loc.T` en `static readonly`/`const`. |
| El motivo de expulsión por versión se envía en inglés y lo traduce el cliente que lo recibe | El otro jugador puede tener otra versión u otro idioma. |
| Nombres de dispositivo traducidos solo al mostrarlos | `VoiceDeviceInfo.Name` se persiste y se compara. |

### 2.4 Correcciones de CI
- `Starlight/DependencySanitizer/Program.cs`: hash fijado de `Starlight/THIRD_PARTY_NOTICES.md` actualizado (el rediseño de upstream cambió el archivo sin actualizar el hash).
- `Comms/VoiceUiBranding.cs`: en Starlight se usa la fuente del juego, porque `UnityEngine.TextCoreFontEngineModule` está fuera del contrato de Starlight 1.6.3. En escritorio sigue Space Grotesk.
- `.gitignore`: se añadió `scripts/localization.py` a la lista permitida.
- Esos dos fallos de Starlight ya ocurrían en `new-spanish` (la propia base de upstream); en upstream no pude ver su CI.

### 2.5 Decisiones de diseño
- Los **ids** (claves del `.cfg`, ids de atajos, estados del servidor) **no se traducen**; el `.cfg` conserva las descripciones en inglés.
- **No se migran** los atajos de quien usó la versión hardcodeada antigua de tu fork (`main`).
- Nombres propios, nombres de teclas, logs y texto de mods de terceros se dejan tal cual.

## 3. Cómo funciona el sistema de locales

### 3.1 Visión general
```
Among Us (idioma elegido)  ──►  Loc.CurrentLocale  ──►  Resources/Locale/<locale>.xml
                                     │                         (embebido en el DLL)
        Loc.T("clave", "English") ◄──┘     inglés = literal del código; otro idioma = tabla XML
```
Patrón de `MiraLocaleManager` (XML por idioma + `DataManager.Settings.Language`), implementado dentro del mod, sin dependencias.

### 3.2 Elección del idioma
`Loc.CurrentLocale`: `Latam → es_419`, `Spanish → es_ES`, cualquier otro idioma (o si `DataManager` aún no existe) → `en_US`. Se lee en cada llamada, sin reiniciar. Los hilos que no son el principal reutilizan el último idioma visto, porque el objeto de idioma vive en IL2CPP.

### 3.3 Cómo se piden los textos
```csharp
Loc.T("hud.muted", "Muted");                          // texto inmediato
Loc.T("tooltip.volume", "Volume: {0}%", pct);          // con argumentos (string.Format)
Loc.L("settings.row.mic_volume", "Mic Volume");       // diferido: LocText, se resuelve con .Text
```
- **El inglés es siempre el literal del código**: si el idioma es inglés, falta la clave o la traducción tiene un `{n}` roto, se devuelve el inglés. Nunca se muestra una clave cruda ni se lanza una excepción en la interfaz.
- **`LocText`** (`Loc.L`) se usa donde la etiqueta se declara antes de construir la UI (filas de ajustes, opciones del host, presets). `.English` es el id estable; `.Text`, el texto en el idioma actual.
- El XML guarda texto plano: `{0}` para argumentos, `\n` para saltos de línea. El marcado de TextMeshPro (`<color>`, `<b>`) se queda en el C#.

```xml
<string name="tooltip.volume">Volumen: {0}%</string>
```

### 3.4 Ids frente a texto
| Se traduce | Nunca se traduce |
|---|---|
| Etiquetas, ayudas, avisos, toasts, estados, tooltips | Claves/secciones del `.cfg` y sus descripciones |
| Nombres de atajos y opciones del host | `VoiceKeybind.Id`, `OptionHolder.Id`, `Entry.Key` |
| Estados del lobby browser | Estados del servidor (`Lobby`, `InGame`), idioma/título publicado |
| Nombres de dispositivo mostrados | `VoiceDeviceInfo.Name` |

### 3.5 Cambio de idioma en caliente
`VoiceUiKit.Tick` (también activo con el menú de Opciones abierto) llama a `Loc.PollLocaleChange()` cada fotograma. Al cambiar el idioma se dispara `LocalizationRefresh.Apply()`: reconstruye ajustes, host, mezclador, asistente (conserva borrador y página), lobby browser del mod, chips de menú y avisos del HUD. Lo demás se construye al abrirse y ya sale en el idioma nuevo.

### 3.6 Flujo para añadir o cambiar un texto
1. `Loc.T("area.clave", "English")` en el código.
2. `python3 scripts/localization.py` → regenera `en_US.xml` y lista lo que falta.
3. Traducir en `es_419.xml` **y** `es_ES.xml`.
4. `dotnet test`: falla si falta/sobra una clave, cambia un `{n}`, el inglés del XML no coincide con el código o hay `Loc.T` en `static readonly`/`const`.

### 3.7 Añadir otro idioma
Copiar `es_419.xml` (p. ej. a `pt_BR.xml`), traducir los valores, añadirlo a `ShippedLocales` y mapear `SupportedLangs.Brazilian => "pt_BR"` en `Loc.LocaleFor`.

## 4. Mantenerlo sincronizado con upstream
1. `git fetch upstream` (`https://github.com/artriy/Perfect-Comms`) y `git merge upstream/main` en tu rama.
2. Cada texto nuevo de upstream aparecerá como literal inglés sin `Loc`: envuélvelo con `Loc.T`, regenera con `scripts/localization.py` y traduce.
3. Si upstream cambia el inglés de una cadena ya envuelta, `dotnet test` te avisa (el XML ya no coincide).
4. Si upstream cambia `Starlight/THIRD_PARTY_NOTICES.md`, vuelve a actualizar el hash fijado del sanitizer.

## 5. Apéndice: la versión hardcodeada anterior de tu fork (`main` / `old-spanish`)
Tradujo directamente el C# (≈550 líneas en 29 archivos) sobre una base anterior al rediseño. Diferencias con la versión nueva:
- Español para todos los jugadores, sin inglés ni cambio de idioma.
- Cambió la clave de 11 atajos del `.cfg`, por lo que los usuarios perdían sus teclas.
- Dependía de textos visibles en varios lugares (ayudas, botón *Entrar*, estados).
- Cada merge de upstream chocaba con líneas traducidas.
Sus traducciones se aprovecharon (602 pares extraídos en `pairs-en-es419.tsv`) para llenar `es_419.xml`.

## 6. Estado y pendientes
- Compila sin warnings; 1312/1312 pruebas de escritorio; Starlight valida su DLL único.
- **Falta probar dentro del juego** (lista en `etapa6.md`): lobby browser de Among Us, expulsión por versión, anchos de texto en español, cambio de idioma con la UI abierta y Android.
- Dos pruebas de tiempos de Starlight son intermitentes en entornos lentos (también sin los cambios de localización).
- El helper nativo de audio no viene en el repositorio: un build local sin `Libs/pc-capture`, `Libs/dsp` y `Libs/pion` no tiene voz.
