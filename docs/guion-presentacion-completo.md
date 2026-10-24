# Guion completo de la presentación grupal (7 partes, 10 minutos)

Guion de respaldo y de ensayo: contiene el texto de las siete partes para que cualquier integrante pueda cubrir a otro. Las partes 3 y 5 integran el guion personal de David Recinos (Plan B, tabla de enlaces y ajustes de texto). La numeración de figuras y capturas es la del entregable 12 final.

**Registro.** Entre técnico y empresarial. Poco código en pantalla. Nunca se muestran contraseñas, cadenas de conexión, valores de secretos ni variables de entorno: en las consolas de Neon, Azure y Vercel solo se enseñan nombres de recursos, ramas, revisiones y estados.

| Parte | Tema | Responsable | Tiempo | Demo en vivo | Palabras "Qué digo" (aprox.) |
| --- | --- | --- | --- | --- | --- |
| 1 | Empresa y proceso seleccionado | José López | 1:00 | No (diapositivas) | ~125 |
| 2 | Problema identificado | Alejandro Ochoa | 1:00 | No (diapositivas) | ~110 |
| 3 | Solución tecnológica desarrollada | David Recinos | 1:30 | Neon, Azure Container Apps, GitHub Actions | ~165 |
| 4 | Principales funcionalidades | Luis Reyes | 1:30 | Aplicación en producción, perfil Contador (M10) | ~165 |
| 5 | Demostración en producción | David Recinos | 2:00 | Aplicación en producción, perfil Administrador | ~225 |
| 6 | Controles de seguridad | Alejandro Ochoa | 1:30 | Azure (secretos), GitHub (Pull Request y gate de migraciones) | ~170 |
| 7 | Resultados y beneficios | José López | 1:10 | SonarCloud, Azure Boards | ~140 |
| | **Total** | | **9:40** | | **~1100** |

Referencia: 130 palabras habladas por minuto, el ritmo de una exposición con pantalla. Las palabras incluyen la transición al siguiente orador. En las partes con demo en vivo se habla mientras se hace clic, así que el texto ya cuenta el ritmo de la pantalla. A 130 palabras por minuto el texto suma unos 8:30; los 70 segundos restantes son para clics, cargas de pantalla y cambios de orador.

**Alcance.** El sistema presentado es el actual: nueve de diez módulos operativos (M1 a M8 y M10) y M9 en diseño. Se puede hablar y mostrar impuestos, IVA, DTE de FEL, libros fiscales, retenciones de ISR, multimoneda, diferencial cambiario, revaluación y enteramiento. Lo único que no se afirma es lo que el sistema no hace: no calcula la retención de IVA como agente de retención (Delta no tiene esa condición), no emite ni certifica documentos de FEL (solo registra sus datos) y la importación inicial (M9) aún no existe.

---

## Checklist global previo (día de la presentación)

**Ventanas y sesiones, en el orden en que se usan.** Dos ventanas de navegador: la ventana A (presentación y consolas) y la ventana B (aplicación). **La parte 4 necesita al Contador y la parte 5 al Administrador, así que hacen falta dos sesiones abiertas a la vez:** la ventana B con la pestaña 9 (Contador, sesión ya iniciada) y una segunda ventana B, aparte y preferiblemente de perfil o modo privado del navegador para que las sesiones no se pisen, con la pestaña 10 (Administrador, en el login). Probar en el ensayo del mismo día que al cambiar de ventana no se cierra ninguna sesión.

| N.º | Ventana / pestaña | Enlace | Se usa en |
| --- | --- | --- | --- |
| 1 | A: diapositivas con las figuras del entregable (Figuras 1, 2, 4, 44, 45 y capturas de respaldo) | [enlace: pendiente] (archivo local de la presentación) | Partes 1, 2, 3, 4, 6 y 7 |
| 2 | A: Neon, Branches del proyecto (solo el listado de ramas) | [enlace: pendiente] (Figura 60, Anexo G del entregable) | Parte 3 |
| 3 | A: Portal de Azure, Container App `ca-delta-erp-backend` > Revisions and replicas | [enlace: pendiente] (Figura 64, Anexo G) | Parte 3 |
| 4 | A: GitHub Actions | https://github.com/David RecinossitoPay/erp-contable-delta/actions | Parte 3 |
| 5 | A: Portal de Azure, `ca-delta-erp-backend` > Security > Secrets | [enlace: pendiente] (Figura 65, Anexo G) | Parte 6 |
| 6 | A: Pull Request hacia `main` > Checks (con `migrar-copia-prod`) | https://github.com/David RecinossitoPay/erp-contable-delta/pull/51/checks | Parte 6 |
| 7 | A: SonarCloud, rama `main` | https://sonarcloud.io/project/overview?id=David RecinossitoPay_erp-contable-delta&branch=main | Parte 7 |
| 8 | A: Azure Boards, épicas | https://dev.azure.com/drecinosg2/erp-contable-delta/_backlogs/backlog/erp-contable-delta%20Team/Epics | Parte 7 |
| 9 | B, sesión 1: aplicación en producción, perfil Contador, con sesión iniciada | https://delta-erp-contable.vercel.app | Parte 4 |
| 10 | B, sesión 2 (aparte): aplicación en producción, perfil Administrador, en el login | https://delta-erp-contable.vercel.app | Parte 5 |
| 11 | A: salud del backend (despertar y respaldo) | [enlace: pendiente] (dirección del Container App más `/health`) | Plan B |

Las pestañas 2, 3 y 5 son consolas: abrirlas ya en la vista indicada y no navegar a otras secciones.

**Quién presenta qué (reasignable).**

| Parte | Responsable original | Responsable hoy (completar si se reasigna) | Respaldo |
| --- | --- | --- | --- |
| 1 | José López | ______ | ______ |
| 2 | Alejandro Ochoa | ______ | ______ |
| 3 | David Recinos | ______ | ______ |
| 4 | Luis Reyes | ______ | ______ |
| 5 | David Recinos | ______ | ______ |
| 6 | Alejandro Ochoa | ______ | ______ |
| 7 | José López | ______ | ______ |

Si se reasigna, quien reciba la parte cambia el nombre en la frase de transición de la parte anterior y de la propia.

**Despertar el backend y la base, 2 minutos antes.** Azure Container Apps escala de cero a dos réplicas y la primera llamada puede tardar. Abrir la pestaña 11 y recargar hasta ver `{"status":"ok","service":"Delta ERP Contable API"}`. Luego, en la segunda sesión de la ventana B, iniciar sesión una vez como Administrador, abrir Libro diario y cerrar sesión (para que Neon, que es serverless, también esté despierto). Dejar la sesión 1 con el Contador ya dentro.

**Usuarios de demostración.** Hay usuarios de demostración por perfil (Administrador del sistema, Contador, Vendedor y Técnico). Las credenciales no van en este documento ni en pantalla: tenerlas en el gestor de contraseñas. El Administrador es el único que ve el botón Reversar y el menú Configuración fiscal.

**Datos a preparar antes (en un ensayo, el mismo día):**

- Un periodo contable en estado **Abierto** (menú Periodos; confirmar cuál está abierto en producción). Anotar su nombre exacto: ______
- Dos cuentas hoja del catálogo sin relación con facturas: una de activo disponible (Caja o Bancos) y una de patrimonio (Capital). Confirmar los códigos reales en producción. Cuenta A: ______ Cuenta B: ______
- Asiento de ejemplo: `DEMO-001`, Q1,000.00 al débito de la Cuenta A y al crédito de la Cuenta B. Para el descuadrado: `DEMO-002`, débito Q1,000.00 y crédito Q900.00. Si `DEMO-001` ya existe, usar `DEMO-003`.
- Motivo de la reversa: "Demostración: corrección de asiento de prueba".
- Para la parte 4: confirmar que el lote de datos de demostración fiscal y multimoneda está cargado (libro de compras y ventas con documentos, retenciones, tipo de cambio vigente y al menos una factura en moneda extranjera). Elegir de antemano el periodo que se va a mostrar.
- Capturas propias de respaldo: formulario Registrar asiento (cuadrado y descuadrado), diálogo y resultado de la reversa, Balance general con "Cuadra", Libro de compras y ventas y pantalla de Tipos de cambio. El entregable no trae esas pantallas.

**Qué NO hacer.** No mostrar valores de secretos ni variables de entorno (en Secrets solo se ven los nombres). No abrir cadenas de conexión de Neon ni la consola SQL. No editar nada en las consolas de Azure, Neon, Vercel ni GitHub. No desplazarse por listas largas más de lo necesario.

**Aviso.** El asiento de ejemplo y su reversa quedan guardados en producción (por diseño no se borran). Avisar al equipo y usar siempre el mismo número de demostración.

**Plan B (red o backend lentos, más de 10 segundos).** Decir sin dramatismo: "El backend en la nube está arrancando; mientras tanto les muestro la evidencia". Recargar la pestaña 11. Si no responde, continuar con las capturas del entregable, por número de figura:

| Qué mostrar | Figura / captura |
| --- | --- |
| Arquitectura desplegada y límites de confianza | Figuras 44 (despliegue) y 45 (límites L1 a L4); Figura 21 (contexto) |
| Ramas de Neon production y development | 60 |
| Tablas del esquema y registro de migraciones | 47, 48 y 61 |
| Recursos y revisión Healthy del backend en Azure | 62 y 64 |
| Secretos del Container App (solo nombres) | 65 |
| Frontend desplegado en Vercel | 66 |
| Lista de los cinco flujos de GitHub Actions | 67 |
| Despliegue del backend con los jobs `migrate` y `build-and-deploy` | 68 |
| Gate de migraciones contra copia de producción en un Pull Request | 69 (y 18) |
| Pruebas de backend y frontend, duplicación y cobertura de líneas cambiadas | 70 (y 19, 20) |
| SonarCloud: rama `main` y Pull Request | 71 y 72 |
| Azure Boards: épicas, jerarquía de E10, casos de prueba | 51, 52 y 57 |
| Inicio de sesión y menús por perfil | 8, 9 y 10 |
| Registro de un asiento balanceado (perfil Administrador) | 7 |
| Factura de compra con retención de ISR en producción (perfil Contador) | 73 |
| Secuencias de reversa, JWT, factura con retención, no residente y enteramiento | 29, 30, 31, 32 y 33 |
| Formulario de asiento descuadrado, reversa y Balance general que cuadra | Capturas propias del ensayo |

Si el inicio de sesión falla por cuenta o contraseña, no insistir más de dos veces y pasar a las capturas.

**Tabla de enlaces (consulta rápida).**

| Recurso | Enlace |
| --- | --- |
| Aplicación (frontend en Vercel) | https://delta-erp-contable.vercel.app |
| API en Azure, punto de salud | [enlace: pendiente] |
| Repositorio | https://github.com/David RecinossitoPay/erp-contable-delta |
| GitHub Actions | https://github.com/David RecinossitoPay/erp-contable-delta/actions |
| Pull Request 51, checks | https://github.com/David RecinossitoPay/erp-contable-delta/pull/51/checks |
| SonarCloud | https://sonarcloud.io/project/overview?id=David RecinossitoPay_erp-contable-delta&branch=main |
| Azure Boards, épicas | https://dev.azure.com/drecinosg2/erp-contable-delta/_backlogs/backlog/erp-contable-delta%20Team/Epics |
| Neon, Azure Portal y Vercel | [enlace: pendiente] (Figuras 60, 62, 64, 65 y 66, Anexo G del entregable) |

---

## Parte 1 — Empresa y proceso seleccionado (José López · 1:00)

| Tiempo | Bloque | Pantalla |
| --- | --- | --- |
| 0:00 – 0:08 | Saludo y presentación del grupo | Portada |
| 0:08 – 0:25 | Quién es Delta | Diapositiva 1 |
| 0:25 – 0:42 | El proceso elegido | Diapositiva 1 |
| 0:42 – 1:00 | Por qué automatizarlo y qué van a ver | Diapositiva 1 |

**Qué digo**

> Buenos días. Somos el Grupo 6 y presentamos Delta ERP Contable.
>
> Delta es una empresa guatemalteca, fundada en 1991, que desarrolla sistemas de información para más de 200 clientes. Y hay una contradicción: Delta digitaliza los procesos de sus clientes, pero su propio control contable seguía en hojas de cálculo, correo y papel.
>
> Ese es el proceso que elegimos: el control contable interno. Transacciones, cuentas por cobrar y por pagar, bancos, reportes financieros y, hoy, también impuestos y moneda extranjera.
>
> Lo justifican tres razones. Responde al primer objetivo estratégico de Delta: un ERP propio que pueda evolucionar a SaaS. Reduce el riesgo, porque las reglas se verifican solas. Y Delta ya domina .NET y PostgreSQL.
>
> Y lo que van a ver hoy no es un prototipo: está en producción.

**Qué muestro.** Pestaña 1: portada y una diapositiva con tres datos de Delta (año de fundación, clientes, proceso elegido).

> [Insertar captura: página de inicio del sitio corporativo de Delta (Figura 1 del entregable). Enlace: pendiente]

**Transición.** "Veamos qué problemas genera ese esquema manual. Alejandro Ochoa, adelante."

---

## Parte 2 — Problema identificado (Alejandro Ochoa · 1:00)

| Tiempo | Bloque | Pantalla |
| --- | --- | --- |
| 0:00 – 0:08 | Planteamiento | Diapositiva 2 |
| 0:08 – 0:45 | Los cinco problemas y su efecto | Diapositiva 2 (tabla) |
| 0:45 – 1:00 | Conclusión: la necesidad | Diapositiva 2 |

**Qué digo**

> El control manual produce cinco problemas.
>
> Primero, procesos manuales y herramientas no integradas: el control queda disperso entre hojas, correos y papel.
>
> Segundo, datos fragmentados. Cada persona guarda su propia versión y no existe una única fuente de verdad.
>
> Tercero, errores y pérdida de trazabilidad. Se duplican registros y cuesta rastrear una transacción.
>
> Cuarto, decisiones tardías, porque los reportes financieros llegan tarde.
>
> Quinto, no escala. El esfuerzo crece con el volumen y la confiabilidad baja.
>
> Para colmo, los requerimientos de este control no estaban documentados: vivían en la práctica diaria del personal.
>
> La necesidad es clara: una sola aplicación, una sola base de datos y reglas contables que se cumplan solas.

**Qué muestro.** Pestaña 1: tabla de dos columnas, "Problema" y "Efecto en la operación", resaltando cada fila mientras se nombra.

> [Insertar captura: diagrama de causa y efecto del planteamiento del problema (Figura 2 del entregable). Enlace: pendiente]

**Transición.** "Esa necesidad la resolvimos con una arquitectura en capas. David Recinos explica la solución."

---

## Parte 3 — Solución tecnológica desarrollada (David Recinos · 1:30)

Esta parte es la solución vista desde la nube real: se enseñan los servicios donde corre el sistema. Todo en modo lectura.

| Tiempo | Bloque | Pantalla |
| --- | --- | --- |
| 0:00 – 0:10 | Frase puente desde el problema | Pestaña 1 |
| 0:10 – 0:40 | Arquitectura en capas, en vivo | Pestaña 1 (Figura 44), luego pestañas 2 y 3 |
| 0:40 – 0:55 | Reglas contables en la base de datos | Pestaña 1 (pseudocódigo) |
| 0:55 – 1:22 | CI/CD con puertas de calidad | Pestaña 4 |
| 1:22 – 1:30 | Scrum y cierre | Pestaña 1 |

### Bloque 3.1 — Frase puente (0:00 – 0:10)

**Qué digo**

> El problema era un control contable disperso. Nuestra respuesta: una aplicación web de tres capas, desplegada en la nube.

**Qué muestro.** Pestaña 1: diagrama de despliegue (Figura 44). Sin señalar todavía.

> [Insertar captura: diagrama de despliegue y flujo de entrega continua (Figura 44 del entregable). Enlace: pendiente]

### Bloque 3.2 — Arquitectura en capas, en vivo (0:10 – 0:40)

**Qué digo**

> El usuario entra a una aplicación React, en Vercel. Solo presenta datos; no decide nada contable.
>
> Habla con una API en ASP.NET Core sobre .NET 10, en un contenedor de Azure que escala a cero sin tráfico. Aquí, la revisión activa, en estado Healthy.
>
> Y todo termina en PostgreSQL 16, en Neon: aquí, las ramas de producción y desarrollo.

**Qué hago**

1. Pestaña 1: con el cursor, recorrer de izquierda a derecha Usuario, Vercel, Azure Container Apps y Neon; marcar la flecha "HTTPS + JWT" entre la aplicación y la API.
2. Pestaña 2 (Neon > Branches): señalar solo las ramas `production` y `development`. No abrir ninguna.
3. Pestaña 3 (Azure > `ca-delta-erp-backend` > Revisions and replicas): señalar la revisión activa con estado Healthy y las revisiones anteriores. No abrir Secrets ni variables.

**Plan B.** Figura 60 (ramas de Neon) y Figura 64 (revisiones en Azure).

> [Insertar captura: Neon > Branches con las ramas production y development (Figura 60 del entregable). Enlace: pendiente]

> [Insertar captura: Azure > ca-delta-erp-backend > Revisions and replicas con la revisión activa en Healthy (Figura 64 del entregable). Enlace: pendiente]

### Bloque 3.3 — Reglas contables en la base de datos (0:40 – 0:55)

**Qué digo**

> Lo importante: las reglas contables no dependen solo de la aplicación. La partida doble la vigila un disparador en la propia base de datos. Si un asiento no cuadra, PostgreSQL lo rechaza, venga de donde venga.

**Qué muestro.** Pestaña 1: proyectar este pseudocódigo en una diapositiva y no navegar el repositorio.

```text
al confirmar un asiento:
    sumar débitos y créditos de sus líneas
    si débitos <> créditos -> RECHAZAR la transacción (RN-01)
```

Opcional, solo si sobra tiempo y por máximo 5 segundos: abrir el disparador real en el repositorio y apuntar únicamente a la comparación `v_total_debito <> v_total_credito` y al `RAISE EXCEPTION 'RN-01...'`. Si hay duda de tiempo, se omite.

### Bloque 3.4 — CI/CD con puertas de calidad (0:55 – 1:22)

**Qué digo**

> Cada cambio pasa por GitHub Actions: pruebas contra PostgreSQL real, control de duplicación y 80 % de cobertura en las líneas que cambian. Aquí, una ejecución en verde.
>
> Si todo pasa, se despliega solo; y antes de fusionar, las migraciones se prueban sobre una copia de producción.

**Qué hago**

1. Pestaña 4 (GitHub > Actions): en la lista lateral señalar los cinco flujos de trabajo.
2. Clic en el flujo "Pruebas, backend y frontend (dev)" y abrir la última ejecución en verde. Pasar el cursor por los jobs de backend, frontend, duplicación, cobertura de líneas cambiadas y "Resultado de las pruebas".
3. Volver a la lista y señalar el flujo "Backend, build y despliegue a Azure Container Apps" (jobs `migrate` y `build-and-deploy`). No abrir logs.

**Plan B.** Figuras 67, 70 y 68.

> [Insertar captura: GitHub > Actions con la lista lateral de los cinco flujos de trabajo (Figura 67 del entregable). Enlace: https://github.com/David RecinossitoPay/erp-contable-delta/actions]

> [Insertar captura: ejecución exitosa del flujo de pruebas de backend y frontend (Figura 70 del entregable). Enlace: pendiente]

### Bloque 3.5 — Scrum y cierre (1:22 – 1:30)

**Qué digo**

> Todo trazado con Scrum en Azure Boards. Ahora Luis Reyes nos cuenta qué hace el sistema.

**Qué muestro.** Pestaña 1 (o dejar Actions a la vista). Ceder la palabra a Luis Reyes. Las épicas de Boards se muestran en la parte 7.

---

## Parte 4 — Principales funcionalidades (Luis Reyes · 1:30)

Dos momentos: el mapa de módulos en diapositiva y un recorrido corto de M10 en la aplicación real, con el perfil Contador (ventana B, sesión 1, ya iniciada). La demo completa de punta a punta es la parte 5. Es la parte más justa de tiempo: si el ensayo pasa de 1:30, se recorta primero la demo (ver "Chequeo de tiempos"), nunca se acelera la voz.

| Tiempo | Bloque | Pantalla |
| --- | --- | --- |
| 0:00 – 0:08 | Visión de los diez módulos | Pestaña 1 (Figura 4) |
| 0:08 – 0:30 | M1 a M8: base contable, cartera, tesorería, reportes y seguridad | Pestaña 1 (Figura 4) |
| 0:30 – 1:20 | M10: fiscal y multimoneda, en vivo | Pestaña 9 (perfil Contador) |
| 1:20 – 1:30 | M9 pendiente y transición | Pestaña 9 |

**Qué digo**

> El sistema tiene diez módulos y nueve están operativos.
>
> M1 a M3 son la base contable: catálogo y periodos, asientos con partida doble, y libros diario y mayor. M4 y M5, cuentas por cobrar y por pagar. M6, tesorería y conciliación bancaria. M7, balance general y estado de resultados. M8, seguridad: JWT, cuatro perfiles y bitácora inalterable.
>
> Lo más reciente es el módulo diez: cumplimiento fiscal y multimoneda. Lo muestro con el perfil Contador. En el menú Fiscal, el libro de compras y ventas, con el IVA y los datos del DTE de FEL. Aquí, las retenciones de ISR, con sus constancias y el enteramiento a la SAT. Y en Catálogo, el tipo de cambio que se trae de Banguat; con él, el sistema calcula el diferencial cambiario y la revaluación de fin de mes.
>
> Fíjense: el Contador no ve Configuración fiscal; eso es solo del Administrador.
>
> Solo la importación inicial, el módulo nueve, sigue en diseño. David Recinos, ahora de punta a punta.

**Qué hago (ventana B, sesión 1, perfil Contador, ya con sesión)**

1. Pestaña 1: mostrar la figura de módulos mientras se nombran M1 a M8.
2. Cambiar a la pestaña 9 (aplicación, Contador). Abrir el menú **Fiscal** y hacer clic en **Compras y ventas**. Elegir el periodo del ensayo y dejar a la vista unos 6 segundos las columnas del libro (IVA y datos del DTE).
3. Menú **Fiscal** > **Retenciones**. Dejar a la vista 5 segundos el reporte. Señalar con el cursor, sin entrar, **Enteramiento ISR**.
4. Menú **Catálogo** > **Tipos de cambio**. Señalar el tipo de cambio vigente de Banguat (6 segundos). Menú **Reportes** > **Saldos en moneda** solo si sobra tiempo.
5. Abrir de nuevo el menú **Fiscal** y mostrar que no aparece **Configuración fiscal**. Al decir "David Recinos, ahora de punta a punta", ceder la palabra.

**Plan B.** Figura 73 (factura de compra con retención de ISR en producción, perfil Contador), Figura 4 (módulos) y capturas propias del Libro de compras y ventas y de Tipos de cambio. Secuencias de apoyo: Figuras 31 y 33.

> [Insertar captura: aplicación en producción con perfil Contador, factura de compra con retención de ISR (Figura 73 del entregable). Enlace: https://delta-erp-contable.vercel.app/cxp]

> [Insertar captura: Libro de compras y ventas en producción, con IVA y datos del DTE de FEL. Perfil: Contador. Enlace: pendiente]

> [Insertar captura: pantalla de Tipos de cambio con la tasa vigente de Banguat. Perfil: Contador. Enlace: pendiente]

**Transición.** "David Recinos, ahora de punta a punta."

---

## Parte 5 — Demostración en producción (David Recinos · 2:00)

Es un solo flujo de punta a punta: registrar un asiento, intentar uno inválido, consultar, corregir con una reversa y comprobar que los estados financieros siguen cuadrando. No es un recorrido por módulos (eso lo hizo Luis Reyes en la parte 4). Se hace en la ventana B, sesión 2, con el perfil Administrador.

| Tiempo | Paso | Pantalla (menú) |
| --- | --- | --- |
| 0:00 – 0:15 | 1. Inicio de sesión | Login |
| 0:15 – 0:30 | 2. Catálogo de cuentas (vista rápida) | Catálogo > Cuentas contables |
| 0:30 – 0:55 | 3. Registrar un asiento cuadrado | Operaciones > Registrar asiento |
| 0:55 – 1:10 | 4. Intentar uno descuadrado | Operaciones > Registrar asiento |
| 1:10 – 1:25 | 5. Ver el asiento en el libro diario | Libros > Libro diario |
| 1:25 – 1:45 | 6. Reversar con motivo | Libro diario > Reversar |
| 1:45 – 2:00 | 7. Comprobar que cuadra y transición | Reportes > Balance general |

### Paso 1 — Inicio de sesión (0:00 – 0:15)

**Qué digo**

> Entro a producción con el usuario de demostración con perfil Administrador. El menú se adapta al perfil. Sigo un solo flujo: registrar, consultar, corregir y comprobar.

**Qué hago**

1. Pestaña 10, pantalla de login.
2. Campo **Correo**: teclear el correo del usuario de demostración. Campo **Contraseña**: teclearla sin mostrarla.
3. Clic en el botón de ingreso del formulario de login. Esperar la carga del menú superior (si tarda, ver Plan B).
4. Dejar a la vista unos segundos los grupos del menú: Catálogo, Operaciones, Libros, Fiscal y Reportes.

> [Insertar captura: menú principal completo con el perfil Administrador del sistema (Figura 9 del entregable). Enlace: https://delta-erp-contable.vercel.app/]

### Paso 2 — Catálogo de cuentas (0:15 – 0:30)

**Qué digo**

> Este es el catálogo de cuentas, en jerarquía. Es la base de todo asiento: nada se registra contra una cuenta que no exista aquí. Uso dos de estas cuentas.

**Qué hago**

1. Menú **Catálogo** > **Cuentas contables**.
2. Mostrar la lista unos 5 segundos y señalar con el cursor la Cuenta A y la Cuenta B anotadas en el checklist. No editar nada.

### Paso 3 — Registrar un asiento cuadrado (0:30 – 0:55)

**Qué digo**

> Registro un asiento: mil quetzales al débito de esta cuenta y mil al crédito de esta otra. El indicador marca que débitos y créditos son iguales. Registro, y el sistema lo confirma al instante.

**Qué hago**

1. Menú **Operaciones** > **Registrar asiento** (título "Registrar asiento contable").
2. Campo **Número de asiento**: `DEMO-001`. Campo **Fecha**: la del día (ya viene cargada). Selector **Periodo** ("Selecciona periodo"): el periodo Abierto anotado.
3. Fila 1: selector **Cuenta** = Cuenta A, **Débito** = 1000.00. Fila 2: selector **Cuenta** = Cuenta B, **Crédito** = 1000.00. El formulario trae dos filas; si hiciera falta otra, botón **+ Agregar línea**. Dejar **Centro de costo** en "(ninguno)".
4. Señalar la línea del indicador: "Débitos: 1,000.00 · Créditos: 1,000.00 ✓ Cuadrado".
5. Clic en el botón **Registrar asiento**. Mostrar el mensaje verde "Asiento DEMO-001 registrado correctamente."

> [Insertar captura: registro de un asiento contable balanceado con el mensaje de confirmación (Figura 7 del entregable). Enlace: https://delta-erp-contable.vercel.app/asientos]

### Paso 4 — Intentar uno descuadrado (0:55 – 1:10)

**Qué digo**

> Ahora uno que no cuadra: mil al débito, novecientos al crédito. El indicador muestra la diferencia y el botón se bloquea. Y aunque alguien se saltara la pantalla, la API y la base de datos también lo rechazan.

**Qué hago**

1. En el mismo formulario (tras registrar, se limpian número y líneas, y se conservan fecha y periodo): **Número de asiento** `DEMO-002`.
2. Fila 1: Cuenta A, **Débito** 1000.00. Fila 2: Cuenta B, **Crédito** 900.00.
3. Señalar el indicador "Débitos: 1,000.00 · Créditos: 900.00 · Diferencia: 100.00" y el botón **Registrar asiento** deshabilitado.
4. No enviar nada. Pasar directo a **Libros** > **Libro diario**.

*Nota para mí:* el primer rechazo visible es el del propio formulario (botón bloqueado). El rechazo de la API (400) y del disparador de la base de datos no se ven en pantalla; los menciono de palabra y los respaldan las pruebas automatizadas (TC-04). No afirmar que se vio un mensaje del servidor.

### Paso 5 — Libro diario (1:10 – 1:25)

**Qué digo**

> El asiento válido ya figura en el libro diario, con su fecha y sus dos líneas. Cada movimiento queda registrado y se consulta cuando se necesite.

**Qué hago**

1. Menú **Libros** > **Libro diario**.
2. En el selector de periodo (opción inicial "Selecciona periodo"), elegir el periodo Abierto.
3. Ubicar el asiento `DEMO-001` (columnas Fecha, Asiento, Cuenta, Centro de costo, Débito, Crédito, Estado). El libro lista solo asientos confirmados; en la columna Estado aparece el botón Reversar. Señalarlo. No desplazarse por el resto de la lista.

### Paso 6 — Reversar con motivo (1:25 – 1:45)

**Qué digo**

> Un asiento confirmado no se borra ni se edita; se corrige con una reversa. Solo el Administrador puede hacerlo, y con un motivo. El original queda Anulado y se crea una reversa que lo compensa: nada se pierde y todo queda trazable.

**Qué hago**

1. En la fila de `DEMO-001`, columna **Estado**, clic en el botón **Reversar**.
2. En el diálogo **Reversar asiento**, escribir en el campo **Motivo**: "Demostración: corrección de asiento de prueba". El botón **Reversar asiento** del diálogo permanece bloqueado mientras el motivo esté vacío (señalarlo un instante).
3. Clic en el botón **Reversar asiento** del diálogo (no en **Cancelar**).
4. Mostrar el mensaje "Asiento ... anulado. Reversa REV-... registrada.", la insignia "Anulado — reversado por REV-..." en el original y "Reversa de ..." en la reversa Confirmada.
5. Si sobra un segundo: decir que los asientos vinculados a facturas, pagos o tesorería no se reversan desde aquí y no muestran el botón (no abrirlos).

> [Insertar captura: libro diario con el diálogo de reversa y el motivo, y el resultado con el original Anulado. Perfil: Administrador. Enlace: pendiente] (el entregable muestra solo el diagrama de secuencia, Figura 29)

### Paso 7 — Estados financieros y transición (1:45 – 2:00)

**Qué digo**

> Original y reversa se compensan: el efecto neto es cero. Lo compruebo en el balance general, que sigue cuadrando. Información contable íntegra, trazable y al instante.

**Qué hago**

1. Menú **Reportes** > **Balance general**. El reporte abre con el periodo más reciente; si no es el de la demo, cambiarlo en el selector de periodo.
2. Ir al pie de la tabla y mostrar la fila **Cuadra** (en verde) bajo "Total pasivo + capital". El distintivo "Cifras preliminares (periodo abierto)" es normal. No desplazarse por el detalle de cuentas.
3. Cerrar con la frase de transición mirando al compañero que sigue.

> [Insertar captura: Balance general con la fila "Cuadra". Perfil: Administrador. Enlace: pendiente]

**Transición (literal).**

> Un sistema que cuadra es solo el comienzo. Ahora Alejandro Ochoa explica cómo lo protegemos.

**Plan B de la parte 5.** Si el inicio de sesión o la carga fallan, aplicar el Plan B global: Figura 7 (asiento balanceado), capturas propias del descuadrado, de la reversa y del Balance general, y la Figura 29 (secuencia de la reversa). Si `DEMO-001` ya existe, usar `DEMO-003` y ajustar la búsqueda en el Libro diario.

**Cinco frases clave (por si hay que improvisar).**

1. "Tres capas desacopladas: React en Vercel, API ASP.NET Core sobre .NET 10 en Azure, PostgreSQL 16 en Neon."
2. "La partida doble la garantiza la base de datos con un disparador, no solo la aplicación."
3. "Cinco flujos de GitHub Actions y SonarCloud con Quality Gate aprobado, cobertura de 98.5 % en código nuevo y 0 % de duplicación (verificar en vivo)."
4. "Un asiento confirmado no se borra: se reversa. Solo el Administrador, con motivo; el original queda Anulado y la reversa lo compensa."
5. "Del commit a producción sin pasos manuales, con las migraciones probadas antes sobre una copia de producción."

---

## Parte 6 — Controles de seguridad (Alejandro Ochoa · 1:30)

| Tiempo | Bloque | Pantalla |
| --- | --- | --- |
| 0:00 – 0:18 | Acceso: JWT, contraseñas y perfiles | Pestaña 1 (diapositiva de seguridad) |
| 0:18 – 0:40 | Integridad en la base y bitácora inmutable | Pestaña 1 |
| 0:40 – 0:52 | Mínimo privilegio | Pestaña 1 |
| 0:52 – 1:15 | Secretos y cadena de entrega, en vivo | Pestañas 5 y 6 |
| 1:15 – 1:30 | Límites de confianza y cierre | Pestaña 1 (Figura 45) |

**Qué digo**

> Protegemos el sistema en capas.
>
> Primero, el acceso: un token JWT firmado, con vigencia de 60 minutos, contraseñas con hash y sal, y perfiles que el servidor verifica, no solo el menú. Los parámetros fiscales, por ejemplo, los escribe solo el Administrador.
>
> Segundo, la integridad en la base de datos: además de la partida doble, rechaza modificar un periodo cerrado, borrar asientos y pagar más que el saldo pendiente. Cada operación relevante queda en una bitácora, en la misma transacción, y un disparador impide alterarla, incluso al Administrador.
>
> Tercero, mínimo privilegio: la API no usa el usuario propietario de la base, sino un rol propio.
>
> Cuarto, los secretos. Aquí, en Azure, solo se ven los nombres, nunca los valores, y están fuera del repositorio.
>
> Quinto, la entrega: nada llega a producción sin Pull Request. Este tiene el job de migraciones en verde contra una copia de producción, y es requisito para fusionar a main.
>
> Todo esto se ordena en cuatro límites de confianza. José cierra con los resultados.

**Qué hago**

1. Pestaña 1: diapositiva con cinco iconos (acceso, integridad, mínimo privilegio, secretos, cadena de entrega). Explicar sin abrir nada hasta el cuarto punto.
2. Pestaña 5 (Azure > `ca-delta-erp-backend` > Security > Secrets): mostrar únicamente la columna de nombres. Si el portal muestra valores, no hacer clic en ningún "mostrar valor". 5 segundos.
3. Pestaña 6 (Pull Request > Checks): señalar el job `migrar-copia-prod` en verde y su resumen de migraciones. Si se ve, señalar la protección de `main` que lo exige. 10 segundos.
4. Pestaña 1: cerrar con la Figura 45 (límites L1 a L4: navegador, API, base de datos y repositorio).

**Plan B.** Figura 65 (secretos, solo nombres), Figura 69 (gate de migraciones) y Figura 45.

> [Insertar captura: Azure > ca-delta-erp-backend > Security > Secrets, solo nombres, sin valores (Figura 65 del entregable). Enlace: pendiente]

> [Insertar captura: Pull Request 51 > Checks con el job migrar-copia-prod en verde y la protección de main (Figura 69 del entregable). Enlace: https://github.com/David RecinossitoPay/erp-contable-delta/pull/51/checks]

> [Insertar captura: límites de confianza L1 a L4 (Figura 45 del entregable). Enlace: pendiente]

> [Insertar captura: rechazo RN-01 por la base de datos y roles de la base (solo nombres de roles). Enlace: pendiente] (el entregable no trae estas capturas; usar las propias del ensayo si existen)

**Transición.** Ya va en la última frase del texto: "José cierra con los resultados."

---

## Parte 7 — Resultados y beneficios (José López · 1:10)

| Tiempo | Bloque | Pantalla |
| --- | --- | --- |
| 0:00 – 0:20 | Problema, beneficio | Pestaña 1 (tabla de resultados) |
| 0:20 – 0:42 | Evidencias en vivo | Pestañas 7 y 8 |
| 0:42 – 0:50 | Aporte a la transformación digital | Pestaña 1 |
| 0:50 – 1:05 | Limitaciones y siguiente paso | Pestaña 1 |
| 1:05 – 1:10 | Cierre y preguntas | Diapositiva final |

**Qué digo**

> Cerramos con los resultados, problema por problema.
>
> Una sola aplicación reemplaza hojas, correos y papel, con una única base de datos. Los errores se frenan en la propia base y una bitácora inalterable sostiene la trazabilidad. Los reportes son en línea, y los impuestos y la moneda extranjera viven en el mismo sistema.
>
> Lo respaldan más de novecientas pruebas de backend y más de setecientas de frontend. SonarCloud tiene el Quality Gate aprobado, con 98.5 % de cobertura en código nuevo. Y en Azure Boards, nueve de diez épicas están cerradas.
>
> Delta aplica a su propio control la transformación digital que ofrece a sus clientes.
>
> Limitaciones: los beneficios son cualitativos, sin mediciones previas. Faltan la importación inicial y las notificaciones por correo, y hoy el sistema atiende a una sola empresa; la versión SaaS multiempresa es la siguiente fase.
>
> Gracias. Quedamos atentos a sus preguntas.

**Qué hago**

1. Pestaña 1: tabla "Problema" y "Beneficio" (sin cifras de ahorro, que no existen).
2. Pestaña 7 (SonarCloud, rama `main`, Overall Code): señalar el Quality Gate (Passed), la cobertura y la duplicación. Verificar las cifras en vivo antes de decirlas (el entregable registra 98.5 % y 0.0 % al 17/10/2026). No abrir el detalle de issues.
3. Pestaña 8 (Azure Boards > Epics): dejar visibles las diez épicas E1 a E10, con E9 en New y el resto en Done. 8 segundos.
4. Cierre con una diapositiva con el nombre del proyecto y el enlace público de la aplicación: https://delta-erp-contable.vercel.app

**Plan B.** Figura 71 (SonarCloud), Figura 72 (Quality Gate de un Pull Request) y Figura 51 (épicas).

> [Insertar captura: panel de SonarCloud de la rama main con el Quality Gate aprobado (Figura 71 del entregable). Enlace: https://sonarcloud.io/project/overview?id=David RecinossitoPay_erp-contable-delta&branch=main]

> [Insertar captura: épicas E1 a E10 en Azure Boards (Figura 51 del entregable). Enlace: https://dev.azure.com/drecinosg2/erp-contable-delta/_backlogs/backlog/erp-contable-delta%20Team/Epics]

**Transición.** "Pasamos a preguntas" (mirar al jurado; cada integrante responde en su área).

---

## Chuleta de preguntas del jurado

| Pregunta | Respuesta corta |
| --- | --- |
| ¿Por qué la partida doble en la base y no solo en la app? | Porque la base es el último control. Si alguien accede sin pasar por la API, PostgreSQL igual rechaza el asiento descuadrado. La aplicación valida para dar buena experiencia; la base garantiza la integridad. |
| ¿Qué pasa si falla Azure o hay arranque en frío? | Azure Container Apps escala de cero a dos réplicas. Tras inactividad, la primera llamada tarda unos segundos. Existe un punto de salud (`/health`) para verificarlo y el despliegue comprueba que la revisión llegue a Healthy. Los datos están a salvo en Neon. |
| ¿Cómo se garantiza la auditoría? | Cada operación relevante escribe en la bitácora dentro de la misma transacción. Ambas se confirman o ambas se revierten. Un disparador impide modificar o borrar registros, incluso al Administrador. |
| ¿Por qué Neon y PostgreSQL? | PostgreSQL ofrece disparadores y procedimientos para imponer reglas en la base, y Delta ya lo domina. Neon es serverless y tiene ramas separadas para producción y desarrollo. |
| ¿Cómo evitan que un error de la app dañe los datos? | La API se conecta con un rol de mínimo privilegio que no puede modificar las tablas inmutables. Los disparadores siguen activos con esos permisos. Las pruebas de integración corren contra PostgreSQL real. |
| ¿Se pueden reversar todos los asientos? | No. Un asiento ya reversado, una reversa, uno de un periodo no Abierto, o uno vinculado a un documento de cuentas por cobrar o por pagar o a un movimiento de tesorería, se rechaza: reversarlo desincronizaría los libros de su módulo de origen. La regla está en `ReversaAsiento`. |
| ¿Qué cubre M10? | Parámetros fiscales e IVA, datos del DTE de FEL en facturas y notas de crédito, libro de compras y ventas, retenciones de ISR con constancias y enteramiento a la SAT, pagos a proveedores no residentes, multimoneda con tipo de cambio de Banguat, diferencial cambiario, revaluación de fin de mes y calendario de feriados. |
| ¿El sistema emite o certifica facturas electrónicas ante la SAT? | No. Registra los datos del DTE de FEL (UUID, serie, número y fecha de certificación) y los lleva al libro fiscal; no certifica ni emite documentos con validez fiscal (RN-12). |
| ¿Y la retención de IVA? | Está fuera del alcance porque Delta no tiene la condición de agente de retención. Si la empresa se marca como agente, el sistema advierte al usuario y no calcula esa retención. |
| ¿De dónde sale el tipo de cambio y qué pasa si Banguat no responde? | Se consulta a Banguat y queda en un historial con su fuente (Banguat o Manual). Si no responde, un usuario autorizado puede registrar el tipo de cambio de forma manual y luego sustituirlo por el de Banguat. |
| ¿Cómo sé que el diferencial cambiario y la revaluación son correctos? | Se calculan con reglas de dominio probadas y los asientos pasan por la misma partida doble. La revaluación tiene vista previa y se puede anular. Los casos de prueba de M10 (TC-21 a TC-64) están cerrados. |
| ¿Quién puede cambiar parámetros fiscales? | Solo el Administrador escribe los parámetros fiscales y de retención. El Contador los consulta y registra operaciones; el servidor valida el permiso, no solo el menú. |
| ¿Qué fue el fallo de migración y cómo lo evitaron? | Un script falló al aplicarse en producción. Se corrigió con un script nuevo, porque las migraciones aplicadas no se editan, y se creó el job `migrar-copia-prod`: en cada Pull Request a main aplica las migraciones sobre una rama efímera copia de producción en Neon. Es requisito para fusionar. |
| ¿Cómo se despliega y se puede volver atrás? | Al fusionar a main: se aplican migraciones, se construye la imagen etiquetada con el commit, se actualiza el Container App y se verifica /health. Cada despliegue crea una revisión y las anteriores quedan visibles en Azure. El frontend se publica en Vercel con alias automático. |
| ¿Cómo garantizan la calidad? | Cinco condiciones para fusionar: pruebas en verde, 80 % de cobertura en líneas cambiadas, sin bloques duplicados de 80 tokens, Quality Gate de SonarCloud aprobado y migraciones probadas sobre copia de producción. |
| ¿Cuántas pruebas hay? | 917 pruebas de backend (xUnit) y 778 de frontend (Vitest, conteo por líneas), y 62 de los 64 casos de prueba definidos están automatizados en integración continua. |
| ¿Por qué .NET 10 y por qué se migró? | Es la versión con soporte de largo plazo vigente. El backend empezó en .NET 8 y se migró el 17 de octubre de 2026; las pruebas y los flujos se actualizaron en el mismo cambio. |
| ¿Por qué no se ejecutan las suites en Azure Test Plans? | La licencia gratuita de Azure DevOps limita la ejecución formal. Los casos de prueba son elementos enlazados a las historias y las pruebas automáticas corren en GitHub Actions. |
| ¿Qué falta para producción real? | Mediciones de la operación para evidenciar beneficios, bloqueo por intentos fallidos y política de contraseñas, monitoreo de rendimiento y alertas, rotar las cuentas de demostración, importar datos iniciales (M9), notificaciones por correo y la versión multiempresa (Fase 2). |

---

## Chequeo de tiempos

| Parte | Responsable | Tiempo asignado | Palabras "Qué digo" (aprox.) | A 130 palabras por minuto | Acumulado |
| --- | --- | --- | --- | --- | --- |
| 1 | José López | 1:00 | ~125 | 0:58 | 1:00 |
| 2 | Alejandro Ochoa | 1:00 | ~110 | 0:51 | 2:00 |
| 3 | David Recinos | 1:30 | ~165 | 1:16 | 3:30 |
| 4 | Luis Reyes | 1:30 | ~165 | 1:16 | 5:00 |
| 5 | David Recinos | 2:00 | ~225 | 1:44 | 7:00 |
| 6 | Alejandro Ochoa | 1:30 | ~170 | 1:19 | 8:30 |
| 7 | José López | 1:10 | ~140 | 1:05 | 9:40 |

Margen: 20 segundos entre el tiempo asignado y los 10:00, más lo que sobra en cada parte (de 2 a 16 segundos por parte, para clics y cargas). El total nunca debe pasar de 10:00. Las partes 1 y 7 son las más justas: no añadir frases.

Si el ensayo supera 10:00, recortar en este orden: bloque 3.4 (CI/CD a una sola frase, sin abrir la ejecución), pasos 2 y 3 de la demo de Luis Reyes (dejar solo Compras y ventas y Tipos de cambio), paso 2 de la demo de David Recinos (catálogo), la lista de M1 a M8 en la parte 4 y la pestaña de Secrets en la parte 6. Nunca recortar el paso 6 de la demo de David Recinos (reversa), que sostiene el mensaje central, ni la secuencia fiscal de Luis Reyes, que es lo nuevo del sistema.
