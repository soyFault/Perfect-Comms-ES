# Plan de localización (XML) — Perfect Comms ES

Objetivo: que el mod muestre sus textos en español (es_419) cuando el idioma de Among Us es Español, y en inglés
en cualquier otro caso, **sin tocar las strings en inglés** del código. Base: rama `new-spanish` (v19 + rediseño).
Rama de trabajo/instrucciones: `claude-local`. La traducción vieja (hardcodeada) está en `main` / `old-spanish`.

## 1. Cómo lo hace Mira API (y qué copiamos)

`MiraAPI/Translation/MiraLocaleManager.cs`:

- XMLs embebidos `Resources/Locale/<código>.xml` (`en_US`, `es_419`, `es_ES`, …), formato `<resources><string name="clave">texto</string></resources>`.
- Idioma = `AmongUs.Data.DataManager.Settings.Language.CurrentLanguage` (enum `SupportedLangs`), en `try/catch` con inglés por defecto.
- `Get(key, fallback)`: idioma actual → inglés → fallback. Soporta `<nl>` (salto de línea) y `[ ]`→`< >` para tags.
- Se registra con `MiraLocaleManager.Register(guid)` y se enlaza al sistema de Reactor (`LocalizationProvider.OnLanguageChanged`).

**Sí es viable, pero sin depender de MiraAPI/Reactor** (Perfect Comms no los referencia; `VoiceReactorCredits` solo usa reflexión).
Ambos son LGPL-2.1, así que se puede adaptar el patrón (dejar una línea de atribución).
Implementación de referencia lista para copiar: `docs/localization/Localizer.reference.cs.txt` → `Comms/Loc.cs`.

Decisiones de diseño:

| Decisión | Motivo |
|---|---|
| API `Loc.T("clave", "English fallback", args...)` | El inglés queda en el código como fallback: los usuarios en inglés ven exactamente lo de antes, los tests existentes (que comparan texto inglés) no cambian y una clave faltante nunca muestra el id crudo. |
| `Spanish` → `es_ES.xml`, `Latam` → `es_419.xml` | Dos archivos con el mismo contenido inicial para que puedan divergir. |
| `en_US.xml` generado y versionado | Fuente de verdad para traductores + test de consistencia (mismas claves en todos los XML, mismos `{n}`). |
| Resolver al **construir la UI**, no en constructores/`const` | `OptionHolder.Label`, `ToggleDeafenDisplayName` (`const`), arrays estáticos de pestañas se evalúan en `Load`, antes de que `DataManager` esté listo, y no cambian si el jugador cambia de idioma. |
| Markup TMP (`<color>`, `<size>`) se queda en C# | El XML solo lleva texto plano (+ `{0}`, `<nl>`): menos errores de escape y traductores no rompen estilos. |
| Una clave **por uso**, no por texto en inglés | El mismo inglés se tradujo distinto en `main` según el contexto (p.ej. "Welcome" → *Bienvenida* en la pestaña y *Bienvenido* en el título). |

## 2. Hallazgos de la auditoría (qué NO se debe traducir ni cambiar)

Verificado en `new-spanish`:

1. **Claves de config**: `VoiceKeybind` usa `persistedKey` (p.ej. `"Toggle Speaker"`, `"Hold To Mute"`) para BepInEx; `DisplayName` es solo visual.
   Las `config.Bind("Audio", "MicVolume", …)` también son ids. **Solo se traduce el texto visible / `ConfigDescription`**; secciones y claves quedan en inglés
   (en `main` la traducción hardcodeada cambió nombres visibles pero conservó `"Hold To Mute"` como clave, correcto).
2. **Lógica que compara textos**: `VoiceSettingsPanel.cs:354+` hace `"Mic Volume" => "How loud…"` (ayuda según la etiqueta);
   `:377/:379` compara `e.Key == "AliveFocus.DeadPlayers"`; `HostSettingsPanel` agrupa por nombres de categoría (`"PROXIMITY"`…).
   → Cambiar a lookup por **id estable** (clave de config o id de categoría) *antes* de traducir etiquetas. Hacer esto es la Etapa 0.
3. **Textos que salen del cliente — no localizar**: `LobbyBrowserLanguage` (default `"English"`, se publica en `VoiceLobbyRegistryPublisher`) y título de sala,
   payloads RPC/JSON (`VoiceRoomSettingsRpc`, `VoiceRadioStateRpc`), nombres de eventos de diagnóstico (`voice.ui.host_panel_revoked`), `VoiceDiagnostics.Log`, `Log.*`, prefijos `[PC-UI]`/`[PC-SETUP]`.
   Pendiente decidir: los nombres de los canales de radio integrados si viajan por red → traducir solo al **mostrarlos**.
4. **Terceros**: etiquetas que otros mods registran vía `PerfectCommsApi`/`VoiceModRegistry` son suyas; fuera de alcance (se muestran tal cual).
5. **Tests**: `PerfectComms.Tests` referencia el proyecto principal y asserta inglés (p.ej. `Assert.Equal("Toggle Deafen", …ToggleDeafenDisplayName)`,
   `VoiceCompactStatusPolicy`, `VoiceConnectionStatusPolicy`). Con el fallback en inglés y `Loc.CurrentLocale` devolviendo `en_US` cuando `DataManager` no existe, siguen pasando. Para `const` → cambiar a propiedad `static string` o conservar el `const` como fallback + clave.
6. **Fuente**: la UI usa Space Grotesk SDF estática (`VoiceUiBranding.CreateFont`) con la fuente del juego como fallback. Verificar en juego `á é í ó ú ñ ü ¿ ¡ Á É Í Ó Ú Ñ` (probablemente OK por el fallback; `main` ya corría en español con la fuente anterior).
7. `Comms/Android/*`, README/wiki/docs, `cloudflare/`, `native/` (Rust) y los textos del updater **no** forman parte de la UI principal; ver Etapa 5.

## 3. Material de traducción ya extraído

`docs/localization/pairs-en-es419.tsv`: **602 pares inglés → es_419** sacados de `git diff 02a8ddc origin/main` (ancestro común → `main`, que incluye
las cadenas acortadas por "Arreglé cadenas que estaban muy largas"). 599 existen literalmente en `new-spanish`; 3 no (el rediseño las cambió: `None`, `Open Mic`, `Push To Talk`) → traducirlas a mano.
Regenerable con `python3 docs/localization/extract-pairs.py` desde la raíz del repo.
Ojo: el diff incluye cambios ajenos de `main` (quitar Starlight, Grace Period, `VoiceRoleMuteState`); el script ya ignora tests y solo empareja literales,
pero revisar. Todo literal visible en `new-spanish` que **no** aparezca en el TSV (el rediseño añadió ~35 archivos de cambios) queda como "pendiente de traducir": al terminar cada etapa, listarlos con
`grep -rnE '"[A-Z][^"]{3,}"' Comms *.cs` y traducirlos a mano.

## 4. Etapas

Cada etapa = un commit/PR pequeño que compila y pasa los tests; el inglés no cambia en ninguna.

**Etapa 0 — Desacoplar ids de textos** (sin traducir nada aún)
- Ayuda por etiqueta en `VoiceSettingsPanel` (`switch` de ~línea 354) → por id estable; revisar toda comparación `== "texto visible"` y todo uso de categoría como clave (icono, orden).
- `OptionHolder`/`ToggleHolder`/`EnumHolder`/`NumberHolder`: guardar `LabelKey` + `LabelEnglish` y resolver `Label` en un getter (los `Labels[]` de enums igual).
- `ToggleDeafenDisplayName` (`const`) → ver punto 5.
- Criterio: build + tests verdes, comportamiento idéntico.

**Etapa 1 — Infraestructura**
- `Comms/Loc.cs` (referencia incluida), `Resources/Locale/en_US.xml` y `es_419.xml` (vacíos o con 5–10 claves), atribución a MiraAPI en `THIRD_PARTY_NOTICES.md`.
- Verificar que el nombre del recurso embebido es `VoiceChatPlugin.Resources.Locale.es_419.xml` (el glob `Resources\**` ya lo incluye; si cambia el `RootNamespace`/LogicalName, ajustar la constante).
- Test unitario: `Loc.ForcedLocale = "es_419"` devuelve la traducción; clave faltante → fallback; `{0}` mal formado → no lanza.
- Test de paridad: mismas claves y mismos `{n}` en `en_US.xml` y `es_419.xml`; el fallback en C# coincide con `en_US.xml`.
- Script `tools/` para generar `en_US.xml` desde las llamadas `Loc.T("k","…")` (regex simple).

**Etapa 2 — Clases puras y HUD**: `VoiceCompactStatusPolicy`, `VoiceConnectionStatusPolicy` (`"Connecting voice…"`, `{0}/{1} players connected`), `VoiceHudWarnings`, `VoiceChatHudState`, `SpeakingBar*`, `VoiceJoinGuard`. Son las más fáciles de testear en ambos idiomas.

**Etapa 3 — Paneles grandes**: `VoiceSettingsPanel` (95 pares), `HostSettingsPanel`, `VoiceFirstRunSetup` (144), `FirstRunAudioPreview` (68), `FirstRunSetupDraft`, `VoiceVolumeMenu`, `CommsChipButton`, `VoiceUiKit`. Textos interpolados (`$"Step {n} of {m}"`) → `Loc.T(k, "Step {0} of {1}", n, m)`.
Cuidar el ancho: varias cadenas se acortaron en `main` porque no cabían; revisar visualmente cada panel en español (ver Etapa 6).

**Etapa 4 — Opciones y keybinds (nombres/ayudas visibles)**: `VoiceChatKeybinds` (26), `VoiceChatLocalSettings` (52, solo `ConfigDescription` y etiquetas), `VoiceChatGameOptions` (50, opciones del host), `ModOptionHolders`, `HostOptionHolders`. Claves/secciones BepInEx **intactas**.
Nota: los `ConfigDescription` se escriben al `.cfg` al crear el archivo → quedarán en el idioma del primer arranque; decidir si se dejan en inglés (recomendado, son para el usuario avanzado que edita el archivo).

**Etapa 5 — Periferia**: `VoiceLobbyBrowserUi` (28; ojo con la regla 3 de la auditoría), `PerfectCommsUpdateNotifier`/`UpdateClient`, `PingTrackerPatch`, `VanillaLobbyPublicApiClient` (`+N more`), `Comms/Android/*`. Decidir aparte si README/wiki en español forman parte del alcance.

**Etapa 6 — Cambio de idioma en caliente y verificación en juego**
- `Loc.T` lee el idioma en cada llamada (barato), así que los textos nuevos ya salen en el idioma correcto. Para paneles **ya abiertos**: reconstruirlos al cambiar el idioma (parche Harmony postfix a `TranslationController.SetLanguage`, o simplemente re-resolver al abrir cada panel — más simple, recomendado).
- Checklist manual con Among Us en English y en Español: primer arranque (wizard), ajustes, panel de host, HUD (silenciado/ensordecido/conectando), menú de volumen, lobby browser, notificador. Revisar desbordes de texto y los acentos/`ñ`/`¿¡`.
- Prueba de regresión: cfg existente de un usuario previo se carga igual (las claves no cambiaron).

## 5. Decisiones confirmadas (por el dueño del repo)
1. Se traduce todo lo que el jugador ve dentro del mod; README/wiki/docs se traducen **fuera del XML** (archivos `.md` aparte). Android y updater entran en el alcance (Etapa 5).
2. `Resources/Locale/es_ES.xml` existe como archivo propio, con el mismo contenido que `es_419.xml` (para que puedan divergir). `Loc` mapea `SupportedLangs.Spanish → es_ES` y `Latam → es_419`.
3. `.cfg` (ConfigDescription) siempre en inglés; la UI traduce la ayuda por id.
4. Se trabaja por etapas; cada etapa documentada en `docs/localization/etapaN.md`.

Progreso: [Etapa 0](localization/etapa0.md) ✔ · [Etapa 1](localization/etapa1.md) ✔ · [Etapa 2](localization/etapa2.md) ✔ · [Etapa 3](localization/etapa3.md) ✔
5. Atajos del fork hardcodeado (`main`): no se migran; los ids siguen en inglés.

## 6. (histórico) Preguntas iniciales
1. Confirmar el alcance de la Etapa 5 (¿Android, README/wiki, updater?).
2. ¿`es_ES` comparte el XML de es_419 (propuesto) o quieres uno aparte?
3. ¿Los `ConfigDescription` del `.cfg` se quedan en inglés (propuesto)?
4. Si quieres, puedo implementar las etapas en `claude-local` en el orden de arriba, y tú las recreas en `new-spanish`; indícame con qué etapa empiezo.
