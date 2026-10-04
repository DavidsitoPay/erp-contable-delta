# Guión — Video de Evidencia DevOps 2

Guión de grabación para el video "Implementación DEVOPS 2" del canal de YouTube del
equipo. Duración objetivo: **9 minutos, con margen hasta 10**. Cada bloque trae un
tiempo sugerido, no un límite estricto — lo que importa es la suma total al final.

Si algún integrante no puede grabar su bloque, cualquier otro lo puede leer; lo
importante es que las cuatro voces aparezcan al menos una vez en el video, no que
cada quien grabe exactamente el bloque asignado aquí.

Formato de grabación: captura de pantalla con narración en vivo (no es necesario
edición compleja — cortes simples entre bloques bastan). Practicar cada bloque una
vez en silencio, cronometrar, y ajustar el texto si se pasa del tiempo antes de grabar
la toma final.

## Resumen de tiempos

| # | Bloque | Quién habla | Duración objetivo |
|---|---|---|---|
| 1 | Introducción y objetivo | David (Product Owner) | 0:35 |
| 2 | Herramientas utilizadas | Alejandro (Scrum Master) | 0:45 |
| 3 | Infraestructura: grupo de recursos y base de datos | José | 1:05 |
| 4 | Backend y frontend desplegados | José | 0:55 |
| 5 | Repositorio y protección de rama | David | 0:35 |
| 6 | Pipeline: configuración | Luis | 1:35 |
| 7 | Pipeline: ejecución y un error real corregido | Luis | 1:25 |
| 8 | Pruebas funcionales y plan de pruebas | Luis | 1:05 |
| 9 | Monitoreo | José | 0:35 |
| 10 | Reflexión sobre Scrum | Alejandro | 0:30 |
| 11 | Cierre | David | 0:15 |
| | **Total** | | **9:20** |

---

## 1. Introducción y objetivo — David — 0:35

**Pantalla:** cara al frente o diapositiva con el nombre del proyecto; luego
`README.md` del repositorio abierto.

**Guión:**
> Hola, somos el equipo de Delta ERP Contable, proyecto del curso Seminario de
> Tecnologías de Información. Este video es la evidencia de la implementación de
> DevOps 2. En DevOps 1 dejamos configurada la base: repositorio, Azure Boards y un
> pipeline de compilación. En esta entrega automatizamos el despliegue real del
> sistema — cada cambio que se fusiona a la rama principal valida la base de datos,
> construye el backend y lo despliega solo, sin que nadie tenga que hacerlo a mano.

## 2. Herramientas utilizadas — Alejandro — 0:45

**Pantalla:** lista o diagrama simple con los logos/nombres de las herramientas
(puede ser una diapositiva, o simplemente ir mostrando cada consola mientras se
nombra).

**Guión:**
> El código vive en GitHub, y usamos GitHub Actions como motor de integración y
> despliegue continuo. El backend se empaqueta con Docker y se publica en GitHub
> Container Registry. Corre en Azure Container Apps. El frontend, hecho en React, se
> despliega en Vercel. La base de datos es PostgreSQL, administrada por Neon.
> Azure Boards sigue llevando la planificación bajo Scrum, y SonarCloud valida la
> calidad del código en cada cambio.

## 3. Infraestructura: grupo de recursos y base de datos — José — 1:05

**Pantalla:**
1. Portal de Azure → grupo de recursos `rg-delta-erp` (vista de lista de recursos).
2. Consola de Neon → pestaña `Branches`, mostrando `production` y `development`.

**Guión:**
> Toda la infraestructura del backend vive bajo un solo grupo de recursos en Azure,
> `rg-delta-erp`. Adentro está el entorno de Container Apps y la aplicación de
> contenedor que corre nuestra API.
>
> Para la base de datos usamos Neon en vez del ejemplo del enunciado, que era MySQL
> Flexible Server de Azure — porque nuestro modelo de datos ya estaba en PostgreSQL
> desde etapas anteriores, y el nivel gratuito de Neon no depende del crédito por
> tiempo limitado de la cuenta de estudiante. Neon nos deja tener dos ramas de base
> de datos separadas: `production`, la única que usa el backend desplegado, y
> `development`, para el trabajo local de cada quien.

## 4. Backend y frontend desplegados — José — 0:55

**Pantalla:**
1. Azure Portal → Container App `ca-delta-erp-backend`, pestaña `Overview` (mostrar
   el dominio público).
2. Vercel → proyecto, pestaña `Deployments`, el despliegue de producción en `Ready`.

**Guión:**
> El backend corre como imagen de contenedor en Azure Container Apps: puerto 5000,
> de cero a dos réplicas — puede escalar a cero cuando nadie lo está usando, así no
> genera costo en reposo. La cadena de conexión a la base de datos y la clave de
> firma de los tokens están guardadas como secretos de la aplicación, no como texto
> plano.
>
> El frontend se despliega directo en Vercel desde el código fuente, sin pasar por
> un contenedor — cada push a la rama principal genera un despliegue de producción
> automáticamente, por la integración nativa de Vercel con GitHub.

## 5. Repositorio y protección de rama — David — 0:35

**Pantalla:** GitHub → `Settings → Branches`, mostrando la regla activa sobre
`main`.

**Guión:**
> El repositorio en GitHub es la única fuente del código — Azure Boards y Azure
> Pipelines apuntan directo a él. Y desde hace pocos días, la rama principal está
> protegida: todo cambio tiene que llegar por Pull Request, incluso para mí como
> dueño del repositorio. Ya no es posible subir código directo a producción sin que
> pase por revisión.

## 6. Pipeline: configuración — Luis — 1:35

**Pantalla:**
1. GitHub → archivo `.github/workflows/backend-deploy.yml` abierto (vista de código).
2. GitHub → archivo `.github/workflows/frontend-alias.yml` abierto, brevemente.
3. `devops/azure-pipelines.yml` abierto, brevemente, para explicar su rol reducido.

**Guión:**
> Para la automatización elegimos GitHub Actions en vez de Jenkins o GitLab CI: el
> código ya vive en GitHub, así que no hay que conectar una plataforma externa, no
> hay que mantener un servidor propio, y se puede autenticar contra Azure sin
> guardar una contraseña de larga duración, usando identidad federada.
>
> Configuramos dos workflows. El primero, `backend-deploy.yml`, corre solo cuando
> se hace push a `main` y tiene dos trabajos encadenados: `migrate`, que aplica las
> migraciones pendientes de base de datos contra la rama `production` de Neon, y
> `build-and-deploy`, que solo arranca si la migración funcionó — construye la
> imagen del backend, la publica, y actualiza la aplicación en Azure. El segundo
> workflow mantiene actualizado el dominio corto de Vercel después de cada
> despliegue.
>
> El pipeline de Azure que configuramos en DevOps 1 lo mantuvimos, pero con un
> alcance más chico: solo verifica que el backend y el frontend compilen. Le
> quitamos que corriera sobre cada Pull Request, para que no bloqueara las fusiones
> en GitHub duplicando una validación que ya hace Actions.

## 7. Pipeline: ejecución y un error real corregido — Luis — 1:25

**Pantalla:**
1. GitHub → pestaña `Actions`, lista de ejecuciones del workflow de backend, varias
   en verde.
2. Una ejecución exitosa expandida, mostrando los dos trabajos.
3. Localizar en el historial real del equipo la ejecución que falló con el mensaje
   `Cache export is not supported for the docker driver` (paso "Build y push de la
   imagen del backend"), y la ejecución siguiente ya corregida, con el paso
   "Configurar Docker Buildx" agregado antes del build.

**Guión:**
> Cada push a `main` dispara una ejecución que queda registrada acá, con su
> resultado y cuánto tardó cada paso.
>
> Esto no salió bien a la primera. [Mostrar la ejecución fallida.] El controlador
> de compilación de Docker que usa por defecto el runner de GitHub Actions no
> soporta exportar caché al mismo GitHub Actions, y eso es justo lo que le
> pedíamos. Lo corregimos agregando un paso que configura un controlador distinto,
> Buildx, que sí lo soporta, antes de construir la imagen — se ve acá. [Mostrar la
> ejecución corregida, en verde.] Esa es la idea de tener el pipeline corriendo en
> cada cambio: el error se ve al momento, no semanas después.

## 8. Pruebas funcionales y plan de pruebas — Luis — 1:05

**Pantalla:**
1. `https://delta-erp-contable.vercel.app` — login con un usuario de prueba y
   navegación al registro de asientos.
2. Azure DevOps → `Test Plans` → "Plan de pruebas - Delta ERP Contable", mostrando
   las nueve suites (una por módulo) con sus Test Cases.

**Guión:**
> Después de cada despliegue probamos el sistema ya desplegado, no solo en local.
> Entramos con un usuario de prueba, y registramos un asiento contable para
> comprobar que la validación de partida doble funciona sobre el ambiente real.
>
> Los casos de prueba están documentados como Test Cases en Azure Boards, y desde
> esta semana ya están organizados dentro de un Test Plan formal — Microsoft liberó
> el hub de Test Plans para el nivel gratuito de Azure DevOps, así que pudimos
> armar las nueve suites, una por módulo, sin pagar licencia.

## 9. Monitoreo — José — 0:35

**Pantalla:** Azure Portal → Container App → pestaña `Log stream`, capturando en
vivo mientras alguien hace una petición desde el frontend.

**Guión:**
> Para monitorear el backend usamos el log en vivo de Azure Container Apps — acá se
> ve la petición que acabamos de hacer llegando al servidor. Para el frontend, el
> panel de Vercel registra el resultado de cada compilación y despliegue.

## 10. Reflexión sobre Scrum — Alejandro — 0:30

**Pantalla:** opcional, diapositiva simple o cara al frente.

**Guión:**
> Scrum nos ayudó a ver a tiempo cuando el avance real se alejaba de lo planificado
> — con el Sprint Backlog y el burndown lo notamos con días de anticipación, no
> hasta el final. Eso nos dejó decidir el alcance de esta entrega con información
> real, en vez de improvisar a último momento.

## 11. Cierre — David — 0:15

**Pantalla:** cara al frente.

**Guión:**
> Con esto queda automatizado el ciclo completo: un cambio en el código termina
> desplegado en producción sin pasos manuales, validado y probado. Gracias.

---

## Notas de producción

- Grabar los bloques de pantalla (3, 4, 6, 7, 8, 9) por separado, sin narración
  perfecta a la primera — es más fácil regrabar solo el audio de un bloque corto
  que el video completo.
- El bloque 7 ya usa un solo error, el más sencillo de explicar de los cuatro
  documentados en `docs/devops2-implementacion.md` (falla de caché de Docker
  Buildx) — no hace falta mostrar los otros tres. Antes de grabar, localizar en
  GitHub Actions la ejecución real que falló con ese mensaje y la ejecución
  siguiente ya corregida, para no perder tiempo buscándolas en vivo.
- Ocultar o recortar cualquier valor sensible antes de grabar: contraseñas,
  cadenas de conexión completas, tokens. Los nombres de los secretos sí se pueden
  mostrar; sus valores nunca.
- Subtítulos no son obligatorios, pero si se agregan, ayuda tenerlos listos antes
  de subir el video al canal.
