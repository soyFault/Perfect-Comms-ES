<h1 align="center">Perfect Comms</h1>

<p align="center">
  <strong>Chat de voz por proximidad inmersivo, integrado directamente en Among Us.</strong>
</p>

<p align="center">
  <a href="https://github.com/artriy/Perfect-Comms/releases/latest"><img src="https://img.shields.io/endpoint?style=for-the-badge&url=https%3A%2F%2Fgist.githubusercontent.com%2Fartriy%2Fb09ac2c39551270e9961b92e622b0893%2Fraw%2Flatest.json" alt="Última versión"></a>
  <a href="https://github.com/artriy/Perfect-Comms/releases"><img src="https://img.shields.io/endpoint?style=for-the-badge&url=https%3A%2F%2Fgist.githubusercontent.com%2Fartriy%2Fb09ac2c39551270e9961b92e622b0893%2Fraw%2Fdownloads.json" alt="Descargas totales"></a>
</p>

<p align="center">
  <a href="#controles">Controles</a> &nbsp;·&nbsp;
  <a href="#instalación">Instalación</a> &nbsp;·&nbsp;
  <a href="#mods-compatibles">Mods compatibles</a> &nbsp;·&nbsp;
  <a href="#para-desarrolladores-de-mods">Para desarrolladores de mods</a>
</p>

<p align="center">
  <img src="assets/brand/divider.svg" alt="" width="900">
</p>

Perfect Comms hace que el chat de voz se sienta como parte de la partida. Los jugadores pueden hablar dentro del juego, escuchar a quienes están cerca, encontrar salas con chat de voz y jugar con reglas de voz adaptadas a la forma en que realmente se juega Among Us.

**Esta es una traducción no oficial realizada por mí, Fault. No está vinculada en ninguna forma al proyecto oficial y podría no ser compatible con la versión en inglés. La hice específicamente para jugar con mis amigos en Discord pero si tienes un problema / error y necesitas que haga algún cambio para que te funcione a ti, puedes pedirlo con confianza en un Issue de GitHub o en el [Discord donde jugamos](https://discord.gg/fFfPazRK2W)**

*OJO: Este fork es mantenido por mí, NO molesten a los desarrolladores de PerfectComms en inglés porque no harán una versión en español. Si no actualizo con cada release es por falta de tiempo, crea un issue y lo revisaré. También puedes unirte al discord donde jugamos Among Us con mods y ahí respondo rápido.*
<br>

## Por qué los jugadores lo usan

* **Chat de voz integrado en Among Us**, sin Discord ni bots de muteo
* **Audio por proximidad extremadamente inmersivo**
* **Modo opcional solo para reuniones y sala** para una configuración más sencilla
* **Comportamiento de voz específico para cada rol**
* **Buscador de salas con chat de voz integrado**
* **Controles sencillos dentro del juego**, instalar y jugar

<br>

## Cómo funciona

**Proximidad de forma predeterminada.** Todos hablan mediante su propio micrófono y escuchan a cada jugador según qué tan cerca se encuentre dentro del juego: con claridad al estar cerca y más bajo a la distancia.

**El anfitrión configura la partida.** El alcance de escucha, la oclusión por paredes y visión, las reglas de voz para fantasmas y reuniones, y un modo exclusivo para reuniones son opciones configurables por el anfitrión, por lo que cada sala funciona según la configuración que este elija.

<br>

## Mods compatibles

Perfect Comms funciona por sí solo como un mod de voz por proximidad. Algunos mods permiten comportamientos de voz adicionales; sus integraciones se activan automáticamente cuando el mod correspondiente está presente y permanecen inactivas cuando no lo está.

| Mod          | Comportamiento de voz                                                                                                                                                                                                                                                                                                                              |
| :----------- | :------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **TOU-Mira** | Silencios de Chantajista, Carcelero, Parásito / Titiritero, Esfumador y Glitch.<br>Reglas de voz de impostor para el Crewpostor.<br>Modos de voz de fantasmas del Médium.<br>Audio amortiguado para los efectos de Eclipsal, Granadero e Hipnotizador.<br>Radio de Equipo para Impostores, Vampiros y Enamorados, con atajo para cambiar de canal. |

<br>

## Ajustes

| Los anfitriones configuran las reglas de la partida | Los jugadores configuran su propio audio                                                     |
| :-------------------------------------------------- | :------------------------------------------------------------------------------------------- |
| Distancia de voz, atenuación y oclusión             | Dispositivo de micrófono y altavoces                                                         |
| Reglas de voz para conductos, fantasmas y reuniones | Micrófono abierto, pulsar para hablar y puerta de ruido opcional                             |
| Canales de Radio de Equipo                          | Audio espacial en reuniones, además de supresión de ruido y cancelación de eco en escritorio |
| Silencios según el rol (con mods compatibles)       | Volumen individual de cada jugador y disposición del HUD                                     |

<br>

## Controles

### Atajos de escritorio

|      Tecla     | Acción                             |
| :------------: | :--------------------------------- |
|  `Alt derecho` | Silenciar / activar micrófono      |
| `Ctrl derecho` | Ensordecer / dejar de ensordecer   |
|   Mantén `C`   | Pulsar para hablar                 |
|   Mantén `V`   | Radio de Equipo                    |
|       `G`      | Cambiar canal de Radio de Equipo   |
|    `Shift+B`   | Abrir volúmenes de jugadores       |
|      `F7`      | Actualizar conexión de voz         |
|      `F10`     | Abrir Ajustes de Voz               |
|      `F11`     | Abrir Ajustes de Voz del Anfitrión |

> [!NOTE]
> **Los atajos opcionales están desactivados de forma predeterminada.**
>
> Asigna cualquiera de ellos en **Ajustes de Voz > Atajos**:
>
> * **Pulsar para silenciar** (mantener)
> * **Alternar Micrófono Abierto / Pulsar para Hablar**
> * **Vivos más alto / Muertos más bajo** (mantener)
> * **Vivos más bajo / Muertos más alto** (mantener)

> [!TIP]
> **¿Es tu primera vez usando Perfect Comms?**
>
> * **Micrófono Abierto** es el modo predeterminado: tu voz se transmite automáticamente cuando hablas.
>   Pulsa `Alt derecho` para silenciar o activar tu micrófono.
> * **Pulsar para Hablar:** selecciónalo en **Ajustes de Voz > Audio** y luego mantén `C`
>   cuando quieras hablar.
> * **Ensordecer:** pulsa `Ctrl derecho` para dejar de escuchar el chat de voz y pausar tu micrófono
>   hasta que desactives el ensordecimiento.
> * **Radio de Equipo:** cuando tu rol y los ajustes del anfitrión permitan un canal privado,
>   mantén `V` para hablar y pulsa `G` para cambiar entre los canales disponibles.

### Sobre el DLL de Starlight

Realísticamente hablando, pocos tendrán acceso a las builds de testeo de Starlight así que producir un DLL para Android es de momento poco plausible, ya veremos cómo hacemos compatible esto cuando el equipo oficial de Perfect Comms lo portee a Starlight, de momento solo estaré actualizando la versión para PC.

## Instalación

> A partir de v4.1.7, la versión de escritorio se distribuye únicamente como un plugin DLL. No incluye BepInEx.

> [!TIP]
> **Escritorio: si tu mod incluye BepInEx**
>
> 1. Cierra Among Us.
> 2. Conserva la instalación de BepInEx incluida con el mod o modpack. **No instales ni combines otra copia encima de ella.**
> 3. Descarga `PerfectComms.dll` desde la [última versión](https://github.com/artriy/Perfect-Comms/releases/latest).
> 4. Coloca o reemplaza la DLL en `BepInEx/plugins`.

> [!IMPORTANT]
> **Escritorio: si BepInEx no está incluido**
>
> Sigue estos pasos si tu mod no incluye BepInEx o si no estás utilizando ningún otro mod.
>
> 1. Descarga **BepInEx 6 Unity IL2CPP** desde la [página oficial de compilaciones de BepInEx](https://builds.bepinex.dev/projects/bepinex_be).
> 2. Elige la compilación correspondiente a tu plataforma:
>
>    * **Steam o itch.io:** `Unity.IL2CPP-win-x86`
>    * **Epic Games Store o Microsoft Store:** `Unity.IL2CPP-win-x64`
> 3. Extrae BepInEx en la carpeta que contiene `Among Us.exe`.
> 4. Inicia el juego una vez para completar la configuración de BepInEx y luego ciérralo.
> 5. Descarga `PerfectComms.dll` desde la [última versión](https://github.com/artriy/Perfect-Comms/releases/latest) y colócala en `BepInEx/plugins`.

### Estructura de carpetas en escritorio

```text
BepInEx/
└─ plugins/
   └─ PerfectComms.dll
```

<br>

## Para desarrolladores de mods

¿Estás creando un mod de roles? Puedes agregar tus propios comportamientos de voz a Perfect Comms **sin hacer un fork**: silencios, rutas privadas, opciones persistentes del anfitrión, overlays seguros para ocultar identidades, colores animados y Radio de Equipo administrada que reutiliza el selector, PTT y la ruta de red de Perfect Comms. Compila utilizando el pequeño paquete de API exclusivo como referencia; este nunca instala ni copia el runtime de Perfect Comms dentro de tu mod:

```xml
<PackageReference Include="PerfectComms.Api"
                  Version="4.1.7.1"
                  PrivateAssets="all" />
```

`4.1.7.1` es la revisión del paquete de API exclusivo como referencia y su versión es independiente de la del mod destinado a los jugadores. Es compatible con Perfect Comms 4.1.7 y runtimes posteriores que mantengan este contrato de API. Los jugadores deben seguir instalando Perfect Comms por separado. Decláralo como una dependencia opcional y registra tus reglas únicamente cuando esté presente:

```csharp
[BepInDependency("com.edgetel.perfectcomms", BepInDependency.DependencyFlags.SoftDependency)]
// en Load():
PerfectCommsApi.RegisterVoiceRule("com.me.mymod", ctx =>
    ctx.Phase == VoicePhaseKind.Meeting && MyRoles.IsGagged(ctx.Player)
        ? VoiceRuleResult.Mute("Gagged")
        : VoiceRuleResult.Pass);
```

La guía completa, todas las funciones y ejemplos listos para copiar se encuentran en la **[Wiki de integración de mods](https://github.com/artriy/Perfect-Comms/wiki/Mod-Integration)**.

<br>

## Créditos

* Repositorio en inglés: https://github.com/artriy/Perfect-Comms
* Repositorio original: https://github.com/FangkuaiYa/AmongUs-VoiceChat
* BetterCrewLink: https://github.com/OhMyGuus/BetterCrewLink
* Transporte entre pares: [Pion WebRTC](https://github.com/pion/webrtc)
* Agradecimiento especial a [idkimneil](https://github.com/idkimneil), la razón por la que hice esto.

<div align="center">

<img src="assets/brand/divider.svg" alt="" width="900">

</div>

> Perfect Comms es un mod no oficial. No está afiliado con Innersloth, Among Us, BepInEx, MiraAPI, Reactor, BetterCrewLink ni con ninguno de los mods compatibles.

> Yo, Fault, no soy ni el creador de Perfect Comms ni parte del equipo de desarrollo, mi único trabajo aquí fue traducir la mayor parte del texto y cambiar segmentos de código para poder ejecutar la compilación en Github.
