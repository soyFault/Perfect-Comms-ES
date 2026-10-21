# Etapa 6 — Cambio de idioma en caliente, auditoría final y lista de pruebas en el juego

**Estado: hecha en `claude-local`.** Verificado (.NET 10): `PerfectComms.csproj` Release 0 warnings / 0 errores · `PerfectComms.Tests` **1312/1312** (eran 1310; +2) ·
Starlight compila sin warnings; sus 107 pruebas pasan salvo las 2 intermitentes de tiempos que ya fallan sin cambios míos (ver `etapa4.md`) ·
`localization.py --check` al día, `--audit` sin discrepancias de marcadores · verificadores de release OK. XML: **667 claves** (`en_US`/`es_419`/`es_ES`).
**Nada de esto se ha probado dentro del juego** — esa prueba es tuya; la lista está abajo.

## 1. Cambio de idioma con la interfaz ya creada

Los paneles guardan su "cáscara" (cabecera, pie, riel, títulos) y los chips/botones de menú se crean una sola vez, así que al cambiar el idioma en *Opciones* quedaban en el idioma anterior.

- `Loc.PollLocaleChange()` se llama cada fotograma desde `VoiceUiKit.Tick` (hilo principal). La primera llamada solo guarda el idioma; después dispara `Loc.LocaleChanged` **una vez por cambio**.
  - *Cómo sabemos que corre mientras se cambia el idioma*: `VoiceUiKit.Tick` también se invoca desde el parche `OptionsMenuBehaviour.Update` (`VoiceSettingsPanelTriggers.cs`), es decir, mientras el menú de Opciones del juego está abierto, además del `VoiceUiDriver.Update` y del parche de `HudManager`.
- `LocalizationRefresh.Apply()` (registrado en `VoiceChatPluginMain.Load`) reconstruye lo cacheado, cada paso protegido con `try/catch`:

| Elemento | Qué hace al cambiar el idioma |
|---|---|
| Panel de ajustes, panel del host, mezclador de volumen | Descarta la cáscara cacheada; si estaban abiertos, se vuelven a abrir en la misma categoría. |
| Asistente de primer arranque | Conserva el borrador y la página; cambia el título y reconstruye la página y la **vista previa del HUD** (que fija su título/"EN VIVO" al crearse). Si el diálogo "¿Salir?" estaba abierto, se vuelve a mostrar. |
| Lobby browser del mod | Cierra el panel y el editor de información, destruye solo el panel cacheado (el botón del menú es arte y se queda) y desconecta como lo haría `ClosePanel()`. |
| Chips del menú ("OPCIONES DE VOZ", "AJUSTES DEL ANFITRIÓN") | `CommsChipButton.SetLabel`. |
| Avisos del HUD | `VoiceHudWarnings.Invalidate()`. |
| Lista de dispositivos | Ya se reconstruye sola si cambia el idioma (Etapa 4). |
| Tooltips, toasts y filas creadas al abrir | Se resuelven al construirse; no necesitan refresco. |

Ajustes colaterales (iguales a los de `Hide()`): reconstruir el panel de ajustes apaga la *Vista previa en vivo* y la prueba de micrófono. Se aceptan porque cambiar el idioma con el panel abierto es raro.

### Queda desactualizado hasta el siguiente evento (conocido, menor)
- Mensajes de estado que **se guardan ya traducidos**: el estado del directorio en vivo (`VoiceLobbyLiveDirectoryClient`) y los de la prueba de micrófono/altavoz (`FirstRunAudioPreview`) cambian de idioma en su próxima actualización.
- El diálogo de cierre del asistente pierde un posible mensaje de error mostrado.
- Un toast ya visible conserva su idioma hasta que desaparece.

## 2. Auditoría final de textos
`python3 scripts/localization.py --audit`: 0 desajustes de `{n}`/marcado entre inglés y español; 9 claves idénticas al inglés, todas legítimas (`Audio`, `HUD`, `Vertical`, `Horizontal`, `LOBBY`).
Búsqueda de literales visibles sin `Loc`: solo quedan los que son **inglés por decisión** (abajo). `es_ES.xml` es hoy copia de `es_419.xml` (así lo pediste).

### Inglés por decisión (no es un olvido)
- Descripciones del `.cfg`, claves/secciones de configuración, ids de atajos y de filas.
- Nombres de teclas (`Left Shift`, `Right Ctrl`…): etiquetas de hardware/Unity.
- Lo que viaja a la red o al servidor: idioma y título por defecto del lobby publicado, estados `Lobby`/`InGame`, el texto del motivo de expulsión (se traduce en el cliente que lo recibe).
- Logs, diagnósticos, excepciones y mensajes crudos del helper nativo.
- Texto de mods de terceros, nombres propios (The Skeld, MIRA HQ, Reactor, MiraAPI…) y la marca "Perfect Comms".

## 3. Lista de pruebas en el juego (hazla en Español y en English)

**Preparación**: Among Us → *Opciones* → Idioma. Para volver a ver el asistente: Ajustes → Avanzado → *Repetir configuración*, o borrar `Setup.Internal/CompletedSetupRevision` del `.cfg`.

- [ ] **Asistente de primer arranque** (5 páginas + final + diálogo "¿Salir?"): textos completos, nada cortado; el resumen del HUD lee bien; botones con ancho fijo (autoajustan el texto, riesgo bajo).
- [ ] **Ajustes de voz**, las 5 pestañas (Audio, Dispositivos, Atajos, HUD, Avanzado): etiquetas y ayudas; nombres de los atajos y "Ninguna"; lista de dispositivos ("Predeterminado", "(no disponible)"); prueba de micrófono; vista previa en vivo del HUD.
- [ ] **Panel del host** (hay que ser host): pestañas, opciones, ayudas, selectores ("Lineal", "Suave"…), aviso "Solo para anfitrión" si no eres host.
- [ ] **Mezclador de volumen** y vista del HUD: botones de micrófono/altavoz, tooltips, "Silenciado"/"Ensordecido", barra de estado compacta, "Mezcla de voz: …", aviso "Conectando voz…", radio de equipo.
- [ ] **Buscar partida (lista de Among Us)** — el cambio más delicado: cada fila debe mostrar *Anfitrión: …*, estado (*En sala*/*En partida*) y jugadores; revisa `BepInEx/LogOutput.log` por `text target is missing`.
- [ ] **Lobby browser del mod**: título, botón *Entrar* (que siga funcionando y se deshabilite en *LLENA*/*EN PARTIDA*/*VERSIÓN*), editor de título/idioma, mensajes de error de región.
- [ ] **Aviso de actualización** y chips del menú de opciones.
- [ ] **Expulsión por versión distinta** (dos clientes con versiones diferentes): el cliente rechazado debe leer el motivo en su idioma.
- [ ] **Cambio de idioma con la UI abierta**: (a) ajustes abiertos en la pestaña HUD, (b) asistente abierto en la página HUD, (c) lobby browser abierto. Debe cambiar sin errores en el log (`language refresh failed` indicaría un fallo).

**Anchos a vigilar (más probable que desborden en español)**: botón del lobby browser (`EN PARTIDA`, `ENTRAR`, `DESCONOCIDO`), etiquetas del riel (`Hablar`/`Silenciar`, 84 px), etiqueta del chip (`AJUSTES DEL ANFITRIÓN`), nombres de pasos del asistente, posiciones largas del HUD ("Abajo a la izquierda") y la vista previa ("ABAJO A LA IZQUIERDA").

**Sin verificar**: Android (no hay dispositivo) y la tipografía: la fuente Space Grotesk estática usa la del juego como respaldo; confirma que se ven bien `á é í ó ú ñ ü ¿ ¡ Á É Í Ó Ú Ñ`.

## 4. Añadir otro idioma (p. ej. portugués)
1. Copiar `Resources/Locale/es_419.xml` a `pt_BR.xml` y traducir los valores (`{0}`, `\n` se conservan).
2. En `Comms/Loc.cs`: añadir el locale a `ShippedLocales` y el caso `SupportedLangs.Brazilian => "pt_BR"` en `LocaleFor`.
3. `python3 scripts/localization.py --audit` y `dotnet test` (los tests de paridad exigen las mismas claves y marcadores). Una fuente que no tenga los glifos usa la del juego como respaldo.

## 5. Cómo llevarlo todo a `new-spanish`
`new-spanish` **no se ha movido** desde que empezamos (`17177e1`). Todo el trabajo de código está en `claude-local` sobre esa base:
- Todo junto, sin docs: `git checkout new-spanish && git checkout claude-local -- . ':!docs'` o `git diff origin/new-spanish..claude-local -- . ':!docs' | git apply` (44 archivos).
- Con docs: `git merge claude-local` (fast-forward).
- Por etapas: los commits de cada etapa están listados en `etapa1.md`…`etapa6.md` (cherry-pick en orden).
Después: `python3 scripts/localization.py --check`, `dotnet build`, `dotnet test`.

## 6. Corrección posterior: el job de CI de Starlight fallaba (ya antes de la localización)
El workflow *CI* fallaba en los jobs **"Starlight managed build, tests, and single-DLL validation"** y **"Starlight and desktop Pion interoperability"** (Windows), también en la rama base `new-spanish` (run "redesign"), es decir, **antes de cualquier cambio de localización**. Las pruebas de Starlight pasaban; fallaba el paso de empaquetar/validar el DLL con `DependencySanitizer`. Reproducido y corregido en local con los mismos pasos de CI:

1. `Required embedded notice content does not match its pinned source: Licenses.THIRD_PARTY_NOTICES.md` — el rediseño añadió el aviso de la fuente Space Grotesk a `Starlight/THIRD_PARTY_NOTICES.md` pero no actualizó el hash SHA-256 fijado en `Starlight/DependencySanitizer/Program.cs`. Se actualizó el hash (`98E571B2…` → `A61A5AF9…`; el archivo está fijado a LF en `.gitattributes`, así que es el mismo en Linux y Windows).
2. `External assembly reference is outside the Starlight 1.6.3 contract: UnityEngine.TextCoreFontEngineModule` — `VoiceUiBranding.CreateFont` (la fuente Space Grotesk estática del rediseño) usa tipos de `UnityEngine.TextCore`, módulo que el contrato de Starlight 1.6.3 no permite. En la compilación `STARLIGHT` ahora `VoiceUiBranding.Font()` devuelve la fuente del juego (en escritorio no cambia nada). **Efecto visible:** en Starlight/Android la interfaz usa la fuente del juego en vez de Space Grotesk. Alternativa si prefieres conservar la fuente: añadir `"UnityEngine.TextCoreFontEngineModule|0.0.0.0|"` a la lista de referencias permitidas del sanitizer (Unity.TextMeshPro ya depende de ese módulo, así que existe en tiempo de ejecución), pero eso amplía un contrato que el autor original quiso cerrado.

Resultado local: `package-starlight.sh Release` + `verify-release-assets.py --starlight-dll` + `DependencySanitizer validate` pasan y producen un único `PerfectCommsStarlight.dll`. El job de Windows ejecuta el mismo empaquetado y después una prueba de interoperabilidad con Pion que no pude correr aquí; confírmalo en CI.

Además: `scripts/localization.py` **no se había subido** (el `.gitignore` ignora `scripts/*` salvo una lista); se añadió a la lista y se subió.
