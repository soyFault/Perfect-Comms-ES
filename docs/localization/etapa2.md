# Etapa 2 — Clases puras, HUD y avisos (toasts)

**Estado: hecha en `claude-local`.** Verificado (.NET 10): `PerfectComms.csproj` Release 0 warnings / 0 errores · `PerfectComms.Tests` **1297/1297** (eran 1292; +5) ·
`PerfectComms.Starlight.Tests` 107/107 (compila los mismos archivos con `ANDROID;STARLIGHT`, 0 warnings) · `scripts/localization.py --check` al día ·
`verify-release-assets.py --self-test` y `package-release.sh --validate-source` OK. Probado en pruebas unitarias con `es_419`/`es_ES`; **no probado dentro del juego** (se hace en la Etapa 6).
XML: 73 claves nuevas (76 en total) en `en_US`, `es_419` y `es_ES`.

## Qué se localizó

| Área | Archivos | Claves |
|---|---|---|
| Estado de conexión del HUD | `Comms/VoiceConnectionStatusPolicy.cs` | `connection.*` (los puntos animados son el argumento `{0}`) |
| Avisos del HUD | `Comms/VoiceHudWarnings.cs` | `warning.*` |
| Canales de radio de equipo | `Comms/VoiceTeamRadioChannel.cs` | `radio.*` (solo se muestran localmente; no viajan por red) |
| Estados y tooltips del micrófono/altavoz | `VoiceChatHudState.cs` | `hud.*`, `tooltip.*` (el `<b>…</b>` queda en C#) |
| Razones de bloqueo de transmisión | `VoiceChatHudState.cs` | `hud.block_*` |
| Vista previa del HUD (barra de voz) | `Comms/SpeakingBarLivePreview.cs`, `SpeakingBarPreviewSupport.cs` | `preview.*` (nombres de muestra `Jugador 01`/`Fantasma 11` con `{0:00}`; orientación y posiciones con claves propias en vez de `enum.ToString()`) |
| Avisos al unirse | `Comms/VoiceJoinGuard.cs` | `joinguard.*` |
| Toasts de voz/dispositivos | `Comms/PerfectCommsVoiceBackend.cs`, `Comms/VoiceChatRoom.cs`, `Comms/VoiceChatPatches.cs` | `toast.*` |

Las traducciones salen de `main` (`pairs-en-es419.tsv`) salvo las de textos que el rediseño agregó (`HUD PREVIEW`, `Solo chat de impostores`, `Micrófono no disponible - reintentando con el dispositivo`, nombres de muestra y orientación), que son nuevas; revísalas.

## Decisiones y cuidados

1. **El motivo de expulsión por versión se envía en inglés y lo traduce quien lo recibe.** `VoiceJoinGuard` manda el texto por red (otros jugadores pueden tener otra versión u otro idioma). Ahora `MismatchFormat`/`MissingFormat` son el formato "de cable" y `LocalizeKickReason()` los reconoce al mostrarlos en el cliente y los reemplaza por la traducción con las versiones. Un motivo desconocido (otro mod, host más nuevo) se muestra sin cambios. Los avisos `WarnCriticalPatchFailure`/`WarnUnconfirmedGeneration` se muestran traducidos pero el diagnóstico en el log sigue en inglés. Hay una prueba que fija los dos formatos.
2. **Seguridad de hilos (nuevo en `Loc`)**: el idioma del juego vive en objetos IL2CPP, que solo deben tocarse desde el hilo principal de Unity, y varios toasts se disparan desde hilos auxiliares (`ShowToastThreadSafe`). `Loc.Initialize()` (llamado en `VoiceChatPluginMain.Load`) recuerda el hilo principal; los demás hilos reutilizan el último idioma visto en él y nunca leen `DataManager`. Hay un test. **Regla para etapas siguientes: nunca llamar a `AmongUs.Data.*` directamente para el idioma; usar siempre `Loc`.**
3. Las cadenas de la **versión Android** (`Comms/Android/*`, `AndroidVoiceUiPolicy`) y el mensaje de `HostSettingsPanel` ("Host voice settings closed") se dejaron para sus etapas (5 y 3). `Android` comparte con escritorio el código de `VoiceChatHudState.cs`, así que lo compartido ya está traducido.
4. El detalle `{failureDetail}` de los toasts de altavoz es texto del helper nativo / de otras rutas: se interpola tal cual; su traducción corresponde a la Etapa 3 (`FirstRunAudioPreview` y compañía).
5. `VoiceHudWarnings.BuildWarning()` cachea el texto un instante; al cambiar de idioma el aviso se actualiza en el siguiente refresco (no hace falta invalidar).
6. La cadena `"Hotkey: {0}"` etc. pasan `Label` de teclas (`Right Alt`…) sin traducir: son nombres de tecla de Unity; se tratan en la Etapa 4 con `VoiceKeybind.FormatKey`.

## Tests nuevos (`LocalizationTests`)
Conexión en inglés y español (todos los estados), canales de radio, nombres de la vista previa, motivos de expulsión (inglés idéntico, español con versiones, desconocido sin cambios), y que un hilo ajeno reutiliza el idioma en caché. El chequeo de placeholders ahora entiende `{0:00}`.

## Cómo llevarlo a `new-spanish`
`git diff <commit-etapa-1>..<commit-etapa-2>` (o cherry-pick). Archivos de código: los de la tabla + `Comms/Loc.cs`, `VoiceChatPluginMain.cs` (una línea: `Loc.Initialize();`). Después `python3 scripts/localization.py` y `dotnet test`.

## Siguiente: Etapa 3 — paneles (`VoiceSettingsPanel`, `HostSettingsPanel`, `VoiceFirstRunSetup`, `FirstRunAudioPreview`, `FirstRunSetupDraft`, `VoiceVolumeMenu`, `CommsChipButton`, `VoiceUiKit`). Es la más grande (≈330 pares).
