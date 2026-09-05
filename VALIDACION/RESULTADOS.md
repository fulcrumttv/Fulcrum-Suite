# Validación: recuperación de parrilla original para +/-

Plugin 4.1.57 / Core 4.1.42 / Relative 6.6.98.

Base: Plugin 4.1.56 / Core 4.1.41 / Relative 6.6.98.

## Simulación triplicada previa al parche

Antes de modificar producción se ejecutó el modelo independiente:

    node VALIDACION/simulate_start_grid_triplicate.mjs

| Simulación | Semilla | Escenarios | Comprobaciones | Resultado |
| --- | ---: | ---: | ---: | --- |
| 1 | 4282113 | 1000 | 38860 | PASS |
| 2 | 4282114 | 1000 | 39590 | PASS |
| 3 | 4282115 | 1000 | 39620 | PASS |
| Total | — | 3000 | 118070 | PASS |

Cada simulación varía tamaños, órdenes y dos clases. Comprueba reinicio a
media carrera, metadata tardía, prioridad de la formación observada,
aislamiento entre clases, duplicados y estabilidad a través de pits, tow y
amarillas prolongadas.

## Regresiones contra el código final

PASS al ejecutar tres veces, de forma independiente:

    node VALIDACION/test_class_positions.mjs

| Suite por ejecución | Aserciones | Alcance |
| --- | ---: | --- |
| Regresiones anteriores | 351045 | Stints, meta, highlights, clase y fórmulas reales del dashboard |
| Posiciones y parrilla | 436885 | Reinicio, metadata tardía, sesiones offline, IA parcial, multiclase, pits/tow/garage y caution |
| Total por ejecución | 787930 | Aserciones; no son escenarios independientes |
| Total de tres ejecuciones | 2363790 | Las tres terminaron PASS |

Cada ejecución conserva 31,250 combinaciones exhaustivas de posiciones
nativas/generales para una clase de tres coches, 960 frames multiclase
14/12/14 y 154,080 muestras de recuperación de highlights.

El flujo C# se ejecuta mediante un adaptador JavaScript mecánico y limitado,
con sustitutos de BCL/reflection. Las fórmulas se leen del dashboard real y la
prueba de contexto extrae el método de producción. Esto no es compilación C#,
SimHub ni una grabación real de telemetría iRacing.

## Regresiones comprobadas

| Caso | Resultado |
| --- | --- |
| Resolver 4.1.56 reiniciado en Racing con `QualifyPositions` disponible | Reproduce el defecto: toma la clasificación actual y publica 0 para todos |
| Resolver 4.1.57 nuevo en la misma condición | Recupera la parrilla original y publica -1/+1 por clase inmediatamente |
| Dos clases con órdenes distintos | Cada una conserva P1..N y calcula su propio cambio |
| `QualifyPositions.ClassPosition` de la sesión actual | Se busca por identidad `SessionNum` y convierte de base 0 a base 1 una sola vez |
| Fallback global `QualifyResultsInfo.ClassPosition` | Recupera el orden por clase sin derivarlo del orden general |
| Fallback de la sesión de qualifying anterior | Recupera `ResultsPositions.ClassPosition` únicamente anterior a la sesión actual |
| `CurrentSessionInfo.QualifyPositions` coincidente | Aceptado como fallback de la sesión actual |
| Metadata ausente en el primer frame | Crea referencia provisional para no ocultar +/- |
| Metadata que aparece después | Sustituye una vez la provisional por la parrilla histórica |
| Metadata que aparece tras agotar el sondeo rápido | Se recupera durante el sondeo periódico de bajo costo |
| Formación observada que contradice qualifying | La formación real tiene prioridad y sobrevive al green |
| `CarIdx` duplicado o sesión incorrecta | Se rechaza y limpia el snapshot histórico |
| Posiciones de clase duplicadas, incompletas o fuera de rango | No se acepta una parrilla parcial como completa |
| Pits, tow, garage y regreso | No cambian la población ni recapturan la referencia |
| Caution/CautionWaving/OneLapToGreen prolongadas | No ponen a cero ni reemplazan la parrilla |
| Offline AI y otras sesiones activas | Conservan el comportamiento all-session de 4.1.56 |
| Highlights, stints, meta, radar y dashboard | Regresiones anteriores PASS; producción sin cambios |

## Alcance de archivos

- Hay 117 archivos C# de producción.
- Cambian dos archivos de comportamiento: `ClassPositionResolver.cs` y
  `RelativeSessionReader.cs`.
- Cuatro archivos C# adicionales solo cambian etiquetas/versiones.
- Los otros 111 archivos C# de producción permanecen idénticos.
- Relative 6.6.98 permanece idéntico byte a byte.
- `RelativeLapTracker`, highlights, `StintTracker`, módulo/publicadores, gaps,
  radar y Pit Assistant no cambian.

SHA-256 del dashboard sin cambios:

    e7ba898d0ee625bb97abb5636e870d169848a6189c4e6f9654eecd37c1dd0016

## Pruebas nativas incluidas, pendientes de Windows

`BUILD_FULCRUM_v4.1.57_START_GRID_RECOVERY.bat` compila Core y ejecuta:

- `RegressionTests.cs`, incluidas las regresiones de dashboard/laps.
- `ClassPositionTests.cs`, incluidas las combinaciones exhaustivas y las
  fuentes históricas de parrilla.
- `RelativeIntegrationTests.cs`, con lector, módulo y publicadores reales;
  solo sustituye el registro de propiedades de SimHub.

No hay `dotnet`, `csc`, `mcs`, `mono` ni PowerShell disponibles en el entorno
de entrega. No se afirma que esas pruebas nativas hayan pasado. El BAT se
detiene antes de copiar DLL si compilar o probar falla.

## Límites de comportamiento y desempeño

- Normalmente `QualifyPositions` se obtiene en el primer update y el sondeo
  termina. Si no existe, se intenta durante 120 updates y luego una vez cada
  60 updates. No existe I/O de disco ni asignación dinámica por cada auto.
- Si Fulcrum no observó la formación y iRacing ya no expone ninguna fuente
  histórica, el pasado exacto no es reconstruible. Se conserva la primera
  clasificación coherente como referencia provisional, sin inventar puestos.
- El valor cambia cuando cambia la clasificación oficial recibida; no se
  infiere de proximidad física y el SDK puede actualizar ciertas sesiones solo
  al cerrar una vuelta.
- Con varios puestos ausentes y sin orden fiable no se inventan posiciones.
- Esta entrega requiere actualizar ambos DLL. Reimportar el dashboard no
  cambia el cálculo.
