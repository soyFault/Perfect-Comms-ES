# Etapa 1 — Infraestructura de localización

**Estado: hecha en `claude-local`.** Verificado (.NET 10, como `ci.yml`): `PerfectComms.csproj` Release 0 warnings / 0 errores · `PerfectComms.Tests` **1292/1292** (eran 1277; +15) ·
`PerfectComms.Starlight` build 0 warnings y `PerfectComms.Starlight.Tests` 107/107 · `verify-release-assets.py --self-test`, `package-release.sh --validate-source` y `--starlight-source` OK ·
los 3 XML quedan embebidos en `PerfectComms.dll` de escritorio (net6.0) y de Starlight. No corrí empaquetado completo ni el job de Windows.
Comprobado además que el test de coherencia **falla** si el inglés del código cambia sin regenerar `en_US.xml`.

## Qué se añadió

| Archivo | Para qué |
|---|---|
| `Comms/Loc.cs` | `Loc.T("clave", "texto en inglés")` y `Loc.T("clave", "Paso {0} de {1}", a, b)`. Lee el idioma de Among Us en cada llamada (`DataManager.Settings.Language.CurrentLanguage`, en `try/catch`; si no está listo → inglés). `Latam → es_419`, `Spanish → es_ES`, cualquier otro idioma → inglés. Inglés = literal del código (cero riesgo para jugadores en inglés y para tests existentes); clave o locale ausente → literal en inglés; traducción con `{n}` roto → formato inglés (no lanza en la UI). Sin dependencia de MiraAPI/Reactor. |
| `Resources/Locale/en_US.xml`, `es_419.xml`, `es_ES.xml` | XML embebidos (`VoiceChatPlugin.Resources.Locale.<locale>.xml`; el glob `Resources\**` ya existía, también en Starlight). `es_ES` tiene el mismo contenido que `es_419` pero es archivo propio. Formato Mira: `<resources><string name="clave">texto</string></resources>`. |
| `scripts/localization.py` | Regenera `en_US.xml` desde las llamadas `Loc.T(...)` y lista claves sin traducir / huérfanas por idioma. `--check` no escribe (devuelve 1 si `en_US.xml` está desactualizado). |
| `PerfectComms.Tests/LocalizationTests.cs` | Mapa idioma→locale, inglés/español/fallback, formato, parseo, **los 3 XML tienen las mismas claves y los mismos `{n}`**, y **`en_US.xml` coincide con los literales del código** (un escáner de `Loc.T` dentro del test; así CI lo exige sin pasos nuevos). Exige que el 2º argumento sea un literal de texto simple (no `$""` ni concatenación). |
| `Comms/VoiceCompactStatusPolicy.cs` | Prueba de punta a punta con 3 claves reales (`hud.muted`, `hud.deafened`, `hud.muted_deafened`); es el primer ítem de la Etapa 2. |
| `THIRD_PARTY_NOTICES.md` | Atribución del patrón de `MiraLocaleManager` (LGPL-2.1). |

## Reglas para las etapas siguientes

1. **Flujo por cada cadena visible**: reemplazar `"Texto"` por `Loc.T("clave", "Texto")` → `python3 scripts/localization.py` (regenera `en_US.xml`, lista lo que falta) → añadir la traducción a `es_419.xml` **y** `es_ES.xml` → `dotnet test`. El test falla si falta una clave en algún idioma, sobra una, o difiere un `{n}`.
2. **Claves**: `area.nombre_en_snake_case`, una por *uso* (no por texto); mismo texto en inglés con traducción distinta ⇒ claves distintas. Si dos usos comparten clave, deben compartir el mismo inglés (el test lo exige).
3. El marcado de TextMeshPro (`<color=…>`, `<size=…>`, `<b>`) se queda en C#: `$"<color=#8C9CB2>{Loc.T("settings.none", "None")}</color>"`. El XML es solo texto plano + `{0}`; salto de línea = `\n`, barra = `\\` (en `<nl>` no: se interpretaría como etiqueta XML; corregido respecto al borrador de referencia).
4. Nunca pasar por `Loc.T`: ids, claves de config, textos de red/lobby, logs/diagnósticos, ni comparaciones contra mensajes del helper nativo (ver `etapa0.md`).
5. No llamar a `Loc.T` en constructores, `const` ni inicializadores estáticos (el idioma aún no existe al cargar el plugin): usar getters/lambdas o resolverlo al construir la UI. `Loc.T` cuesta un lookup en diccionario; es seguro llamarlo por frame, pero conviene no reconstruir strings cada frame sin necesidad.
6. Cadenas con plural/género distinto: usar dos claves, no lógica de idioma en C#.
7. Sin `Loc.T("k", $"…")`: para interpolados usar placeholders `{0}`.

## Cómo llevarlo a `new-spanish`
Archivos nuevos: `Comms/Loc.cs`, `Resources/Locale/*.xml`, `scripts/localization.py`, `PerfectComms.Tests/LocalizationTests.cs`. Modificados: `Comms/VoiceCompactStatusPolicy.cs`, `THIRD_PARTY_NOTICES.md`.
`git cherry-pick <commit-etapa-1>` desde `claude-local`, o `git diff origin/new-spanish..claude-local -- Comms Resources scripts PerfectComms.Tests THIRD_PARTY_NOTICES.md`.

## Pendiente / siguientes
- **Etapa 2** (clases puras y HUD) continúa con `VoiceConnectionStatusPolicy`, `VoiceHudWarnings`, `VoiceChatHudState`, `SpeakingBar*`, `VoiceJoinGuard`.
- Cambio de idioma en caliente (Etapa 6): `Loc.T` ya reacciona en cada llamada; falta reconstruir los paneles abiertos.
- Si los tests de otras etapas fijan texto en inglés, siguen pasando mientras `Loc.ForcedLocale` sea `null` y no haya `DataManager` (como en CI).
