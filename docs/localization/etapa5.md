# Etapa 5 — Lobby browser (por estructura), Android, avisos y actualizaciones

**Estado: hecha en `claude-local`.** Verificado (.NET 10): `PerfectComms.csproj` Release 0 warnings / 0 errores · `PerfectComms.Tests` **1310/1310** (eran 1308; +2) ·
`PerfectComms.Starlight` (compila los bloques `#if ANDROID`, con las librerías de Android) 0 warnings, 107/107 en 3 corridas (recuerda: 2 pruebas de tiempos fallan a veces en este entorno, ver `etapa4.md`) ·
`localization.py --check` al día · verificadores de release OK. XML: **667 claves**, 0 sin traducir.
**No se probó dentro del juego** — especialmente la parte del lobby browser (más abajo) y Android (no hay dispositivo).

## 1. Lobby browser de Among Us: cajas identificadas por estructura (pedido tuyo)

**Problema real encontrado.** `VanillaLobbyBrowserPatches` buscaba los miembros del juego con reflexión `GetField("tag1"|"tag2"|"capacity"|"gameListing")`, pero en las librerías interop actuales esos miembros son **propiedades**, no campos (comprobado reflejando `Assembly-CSharp.dll`: `GameContainer.tag1/tag2/capacity/gameListing`, `FindAGameManager.gameContainers`, `FindGameMoreInfoPopup.capacity`). Así que `GetField` devolvía `null`, el mod avisaba "not found (game update?)" y todo caía en la heurística de leer **el texto del juego** (`"Player Speed"`, `"Host:"`, `"Roles"`, `"Lobby"`, `"InGame"`, `"Unknown"`, buscar un `/`…). Con Among Us en español esa heurística no reconocía nada.

**Cambio.** Acceso tipado y sin texto:
- `container.tag1` (anfitrión), `container.tag2` (estado), `container.capacity` (jugadores), `container.gameListing`, `manager.gameContainers`, `popup.capacity`.
- Se eliminaron `IsHostBox`, `IsStatusBox`, `IsCapacityText`, la búsqueda por texto y los `FieldInfo`. Si un miembro falta tras una actualización del juego, solo se avisa en el log y no se toca esa caja (no se adivina por texto).
- El rastreo de "cuánto lleva en este estado" usa el estado canónico en inglés (no el texto traducido), así que no se reinicia al cambiar el idioma.
- Se tradujo lo que el mod escribe en esas cajas: `Anfitrión: …`, `En sala` / `En partida` / `Desconocido`, `Anfitrión desconocido`, `Mapa N`, y las etiquetas del bloque de detalles (apagado por defecto).
- Nombres propios (The Skeld, MIRA HQ, Polus, The Fungle, Reactor, MiraAPI…) no se traducen.

> ⚠️ **Probar en juego**: abrir *Buscar partida* con Among Us en español e inglés y comprobar que cada fila muestra anfitrión, estado y jugadores. Si el juego tuviera esos miembros con otro nombre en otra versión, el log lo dirá (`row … text target is missing`).

## 2. Lobby browser propio del mod (`VoiceLobbyBrowserUi`) y directorio en vivo
- Panel, botones, mensajes de error de región/unión, editor de título/idioma, "n salas encontradas", etc.
- **Bug latente evitado**: el botón decidía si era interactivo comparando su texto (`status == "JOIN"`). Con "ENTRAR" habría dejado de funcionar. Ahora `JoinStatus` devuelve `(JoinState, texto)` y la lógica usa el estado (hay prueba).
- Los estados del directorio en vivo (`VoiceLobbyLiveDirectoryClient`) se traducen al producirse (seguro entre hilos); "Disconnected" se resuelve al leerse.
- **No se tradujo** (es dato que viaja al servidor): el idioma por defecto `"English"` y el título `"Perfect Comms"` del lobby publicado, ni los estados del servidor (`Lobby`, `InGame`). Si quieres que el idioma por defecto sea "Español" para jugadores en español hay que decidirlo aparte (cambia lo que otros ven en el listado).

## 3. Android — solo textos
Todo cambio es `const`/literal → `Loc.T` (las `const` pasaron a propiedades `=>` para resolverse al construir la UI; hay un test que prohíbe `Loc.T` en `static readonly`/`const`). Sin tocar lógica:
- `AndroidVoiceUiPolicy`: ayudas del micrófono, radio y altavoz, tooltip de acciones.
- `VoiceChatHudState.Android`: estados y tooltip de la radio de equipo, "Radio de equipo: …".
- `AndroidVoiceUiPolicy.SettingsCategories` (`"AUDIO"…`) se **deja** en inglés: son ids/nombres de objeto que fijan tests; el texto visible del riel ya sale de `VoiceSettingsCategoryText.Caption`.
- El asistente de primer arranque en su modo Android ya se tradujo en la Etapa 3.

## 4. Otros textos visibles
- Notificador de actualización: título y "(Haz clic aquí para descargar)". (`info.Title/Message` de `PerfectCommsUpdateClient` no se muestran; solo van al log.)
- Razones de bloqueo de voz: "Quien convocó tiene la palabra", "Silenciado por rol" (la razón que da un mod de terceros se muestra tal cual).

## 5. Decisiones: qué NO se tradujo y por qué
- **Nombres de teclas** (`Left Shift`, `Right Ctrl`… en `VoiceKeybind.FormatKey`): etiquetas de hardware/Unity, las usan tests y las comparan; se dejan.
- Excepciones, logs y diagnósticos (`SidecarLauncher`, `SpscFloatRing`, `SpeakingBarLayoutPolicy`, `WineEnvironment`, `VoiceLobbyRegistryPublisher`…): no los ve el jugador.
- Texto de terceros (`Mod Route`, etiquetas de `PerfectCommsApi`), marca "Perfect Comms", `PingTrackerPatch` (solo marca + versión).
- README/wiki/docs: fuera del XML, como acordamos (se traducen aparte).

## Auditoría de lo que queda (script de búsqueda de literales visibles)
Quedan sin `Loc` únicamente los puntos del apartado 5 y nombres de objeto/recursos. Se repetirá en la Etapa 6 con el juego.

## Cómo llevarlo a `new-spanish`
`git cherry-pick <commit-etapa-5>` o `git diff 856f5e5..HEAD -- Comms Resources PerfectComms.Tests PerfectCommsUpdateNotifier.cs`. Luego `python3 scripts/localization.py` y `dotnet test`.
