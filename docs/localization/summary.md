# Perfect Comms ES — resumen: versión hardcodeada vs. versión con locales XML

Este documento compara las dos versiones del mod en español y explica cómo funciona el sistema de locales.
Detalle por etapa en `etapa0.md` … `etapa6.md`; el plan original está en `../LOCALIZATION-PLAN.md`.

| | **Versión vieja** (`main` / `old-spanish`) | **Versión nueva** (`claude-local`, sobre `new-spanish`) |
|---|---|---|
| Base de código | Upstream v4.1.x anterior al rediseño | Upstream v19 + rediseño (`17177e1`) |
| Cómo se traduce | Se **reemplazó** el texto inglés por español dentro del C# | El inglés **se queda** en el C#; el español vive en archivos XML |
| Idiomas | Solo español (para todos los jugadores) | Idioma de Among Us: Español (Latam / España) → español, cualquier otro → inglés |
| Jugadores en inglés | Ven español | Ven exactamente el inglés original |
| Cambio de idioma | Imposible | Automático, también con la interfaz abierta |
| Mantenerlo al día con upstream | Cada merge pisa líneas traducidas (550 líneas en 29 archivos) | Los literales ingleses siguen donde estaban; el cambio por cadena es un `Loc.T(...)` |
| Añadir otro idioma | Reescribir el código | Copiar un XML y traducirlo (+ 2 líneas en `Loc.cs`) |
| Pruebas | Las suite original (1262) | 1312 pruebas, 50 de ellas de localización/identificadores |

---

## 1. Qué cambió respecto a la versión vieja

### 1.1 Problemas reales de la versión hardcodeada (corregidos)
- **Atajos reseteados.** `VoiceKeybind` guarda cada atajo en el `.cfg` bajo su nombre visible. Al traducir el nombre, 11 de 13 atajos cambiaron de clave (`"Mute / Unmute Mic"` → `"Silenciar / activar micrófono"`) y los usuarios perdían sus teclas. Ahora el nombre visible y la clave persistida están separados (`VoiceKeybind.Id`) y un test fija las 13 claves.
- **Lógica que dependía del texto.** La ayuda de cada fila se elegía con `switch (etiqueta)`; la lista de dispositivos y el lobby browser comparaban textos visibles; el botón *JOIN* se activaba si su texto era `"JOIN"`. Todo eso pasó a usar ids o estados internos.
- **Lobby browser de Among Us.** Reconocía las cajas de texto del juego leyendo texto en inglés (`"Player Speed"`, `"Host:"`…) y, además, buscaba *campos* que en el interop actual son *propiedades*, así que siempre caía a esa heurística. Ahora usa acceso tipado (`container.tag1/tag2/capacity`, `manager.gameContainers`).
- **Textos congelados al cargar.** Etiquetas guardadas en constructores, `const` y `static readonly` se evaluaban antes de conocer el idioma. Ahora se resuelven al construir la interfaz y un test prohíbe `Loc.T` en esas posiciones.
- **Concordancias y anchos.** Frases armadas con piezas (`"{ShownHidden(...)}"`, `"Controles shown"`) se reescribieron con frases completas por estado.

### 1.2 Lo que el rediseño de upstream trajo y la versión vieja no tenía
Traducido en la nueva: asistente de primer arranque rediseñado, vista previa en vivo del HUD, chat de impostores, controles táctiles, nuevo panel de ajustes, etc. (más de ~70 textos que `main` nunca vio).

### 1.3 Decisiones de comportamiento
- Los **ids** (claves de `.cfg`, ids de atajos, nombres de categoría, estados del servidor) **siguen en inglés**; solo se traduce lo que el jugador lee.
- El **`.cfg`** conserva siempre las descripciones en inglés; el panel muestra su traducción.
- **No se migran** los atajos de quienes usaron la versión vieja (decisión del dueño del repo).
- Lo que viaja por red (idioma/título del lobby, motivo de expulsión) se envía en inglés; el cliente que lo recibe lo traduce al mostrarlo.
- Nombres propios (The Skeld, Reactor, MiraAPI…), nombres de teclas, logs y texto de mods de terceros no se traducen.

### 1.4 Correcciones de CI incluidas
- `Starlight/DependencySanitizer`: hash fijado del aviso de terceros actualizado y, en la compilación Starlight, la fuente del juego en lugar de Space Grotesk (`TextCoreFontEngineModule` no está permitido). Ambos fallos existían ya en `new-spanish`.
- `scripts/localization.py` añadido a la lista permitida de `.gitignore`.

---

## 2. Cómo funciona el sistema de locales

### 2.1 Visión general
```
Among Us (idioma elegido)  ──►  Loc.CurrentLocale  ──►  Resources/Locale/<locale>.xml
                                     │                         (embebido en el DLL)
        Loc.T("clave", "English") ◄──┘     inglés = literal del código; otro idioma = tabla XML
```
Mismo patrón que `MiraLocaleManager` de MiraAPI (XML por idioma, `DataManager.Settings.Language`), pero **sin depender de MiraAPI ni de Reactor**.

### 2.2 Archivos
| Archivo | Función |
|---|---|
| `Comms/Loc.cs` | API (`T`, `L`, `LocText`), elección de idioma, carga de XML, seguridad de hilos, detección de cambio |
| `Resources/Locale/en_US.xml` | **Generado** desde el código; documentación y base de las pruebas |
| `Resources/Locale/es_419.xml`, `es_ES.xml` | Traducciones (idénticas hoy; archivos separados para poder divergir) |
| `Comms/ConfigHelp.cs` | Traducción de las ayudas que salen de `ConfigDescription` |
| `Comms/LocalizationRefresh.cs` | Reconstruye la interfaz cacheada cuando cambia el idioma |
| `scripts/localization.py` | Regenera `en_US.xml`, lista lo que falta (`--audit`, `--check`) |
| `PerfectComms.Tests/LocalizationTests.cs`, `LocalizationIdentifierTests.cs` | Contratos y paridad |

### 2.3 Elección del idioma
`Loc.CurrentLocale`: `Latam → es_419`, `Spanish → es_ES`, cualquier otro idioma (o si `DataManager` aún no existe) → `en_US`. Se lee en cada llamada, así que no hay que reiniciar el juego. Los hilos que no son el principal reutilizan el último idioma visto (el objeto de idioma vive en IL2CPP y solo debe tocarse desde el hilo principal).

### 2.4 Las tres formas de pedir texto
```csharp
Loc.T("hud.muted", "Muted");                          // texto ya
Loc.T("tooltip.volume", "Volume: {0}%", pct);          // con argumentos (string.Format)
Loc.L("settings.row.mic_volume", "Mic Volume");       // diferido: LocText, se resuelve con .Text
```
- **Inglés siempre es el literal del código.** Si el idioma es inglés, si falta la clave o si la traducción trae un `{n}` roto, se devuelve el inglés. Nunca se muestra una clave cruda ni se lanza una excepción en la interfaz.
- **`LocText`** (`Loc.L`) sirve donde la etiqueta se declara antes de construir la UI (filas de ajustes, opciones del host, presets). `.English` es el **id estable**; `.Text` es el texto en el idioma actual.
- El XML guarda solo texto plano: `{0}` para argumentos, `\n` para saltos de línea, `\\` para barra. El marcado de TextMeshPro (`<color>`, `<b>`) se queda en el C#.

Ejemplo de entrada en `es_419.xml`:
```xml
<string name="tooltip.volume">Volumen: {0}%</string>
```

### 2.5 Ids frente a texto
| Se traduce | Nunca se traduce |
|---|---|
| Etiquetas, ayudas, avisos, toasts, estados, tooltips | Claves/secciones del `.cfg` y sus descripciones |
| Nombres de atajos y opciones del host | `VoiceKeybind.Id`, `OptionHolder.Id`, `Entry.Key` |
| Estados del lobby browser | Estados del servidor (`Lobby`, `InGame`), idioma/título publicado |
| Nombres de dispositivo mostrados | `VoiceDeviceInfo.Name` (se persiste y compara) |

### 2.6 Cambio de idioma en caliente
`VoiceUiKit.Tick` (que corre también mientras el menú de Opciones está abierto) llama a `Loc.PollLocaleChange()` cada fotograma. Al detectar un cambio dispara `LocalizationRefresh.Apply()`, que reconstruye lo que está cacheado: panel de ajustes, panel del host, mezclador, asistente (conserva borrador y página), lobby browser del mod, chips de menú y avisos del HUD. Lo que se construye al abrirse (tooltips, toasts, filas) ya sale en el idioma nuevo.

### 2.7 Flujo para añadir o cambiar un texto
1. Escribir `Loc.T("area.clave", "English")` en el código.
2. `python3 scripts/localization.py` → regenera `en_US.xml` y lista lo que falta.
3. Añadir la traducción a `es_419.xml` **y** `es_ES.xml`.
4. `dotnet test`: falla si falta/sobra una clave, cambia un `{n}`, el inglés del XML no coincide con el del código o alguien usa `Loc.T` en un `static readonly`/`const`.

### 2.8 Añadir otro idioma
Copiar `es_419.xml` a, p. ej., `pt_BR.xml`, traducir los valores, añadirlo a `ShippedLocales` y mapear `SupportedLangs.Brazilian => "pt_BR"` en `Loc.LocaleFor`.

---

## 3. Estado y pendientes
- Compila sin warnings; 1312/1312 pruebas de escritorio; Starlight valida su DLL único.
- **Falta probar dentro del juego** (lista en `etapa6.md`): lobby browser de Among Us, expulsión por versión, anchos de texto en español, cambio de idioma con la interfaz abierta y Android.
- Dos pruebas de tiempos de Starlight son intermitentes en entornos lentos (también sin los cambios de localización).
- El helper nativo de audio no viene en el repositorio: un build local sin `Libs/pc-capture`, `Libs/dsp` y `Libs/pion` no tiene voz (ver `etapa4.md` y el hilo de la sesión).
