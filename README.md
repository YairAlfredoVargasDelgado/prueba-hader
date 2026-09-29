# API de Catálogo de Productos

API REST para administrar un catálogo de productos y su inventario, construida
con **.NET 8** y **MySQL**. Resuelve la prueba técnica de Analista Desarrollador
de Aplicaciones de Fundación delamujer.

La documentación interactiva (Swagger UI) se sirve en la **raíz** de la API, de
modo que abrir la URL del despliegue lleva directamente a los endpoints y permite
probarlos desde el navegador.

---

## Índice

1. [Qué resuelve](#qué-resuelve)
2. [Decisiones técnicas](#decisiones-técnicas)
3. [Arquitectura](#arquitectura)
4. [Puesta en marcha](#puesta-en-marcha)
5. [Configuración](#configuración)
6. [Endpoints](#endpoints)
7. [Manejo de errores](#manejo-de-errores)
8. [Concurrencia: por qué el stock nunca queda en negativo](#concurrencia-por-qué-el-stock-nunca-queda-en-negativo)
9. [Pruebas](#pruebas)
10. [Despliegue público](#despliegue-público)
11. [Estructura del repositorio](#estructura-del-repositorio)

---

## Qué resuelve

| Requisito de la prueba | Dónde está resuelto |
| --- | --- |
| `POST /api/products` con nombre, descripción, precio y stock inicial | `ProductsController.Create` |
| `GET /api/products/{id}` | `ProductsController.GetById` |
| Ajuste de stock sumando o restando, sin permitir negativos | `PATCH /api/products/{id}/stock` |
| CRUD completo con códigos HTTP semánticos | `ProductsController` (201, 200, 204, 400, 404, 409) |
| Paginación en el listado | `GET /api/products?page=&pageSize=&search=` |
| Validaciones y manejo de errores | Entidad `Product` + `ExceptionHandlingMiddleware` |
| Swagger funcional y legible | Servido en la raíz, alimentado por los comentarios XML del código |
| Arquitectura | N-Capas (Dominio, Aplicación, Infraestructura, API) |
| Proyecto replicable | `dotnet run`: la API crea la base de datos y el esquema sola |

---

## Decisiones técnicas

La prueba deja varias decisiones abiertas. Estas son las que se tomaron y el
motivo de cada una.

### .NET 8 (LTS) con C# puro

Se usa .NET 8 por ser la versión con soporte extendido en el momento de la
entrega. Se evitan librerías de terceros salvo donde aportan algo que el
framework base no cubre: el proyecto solo depende de **MySqlConnector** (el
driver de MySQL) y **Swashbuckle** (el generador de Swagger). No hay ORM, ni
mapeadores automáticos, ni librerías de validación o de mediación. El código que
importa —las reglas de negocio y el acceso a datos— es C# que se lee de arriba
abajo sin conocer ninguna convención externa.

### Arquitectura N-Capas

De las opciones permitidas se eligió **N-Capas** por ser la que menos ceremonia
impone para un dominio de esta talla, manteniendo lo esencial: las reglas de
negocio aisladas y una frontera clara con la base de datos. Las dependencias
apuntan siempre hacia adentro, así que el dominio no sabe que existe MySQL ni
HTTP.

### MySQL con ADO.NET, sin ORM

La base es relacional porque el dominio lo es (un catálogo con existencias) y
porque una transacción con bloqueo de fila es exactamente lo que hace falta para
el control de stock. Se accede con **ADO.NET puro**: el SQL está a la vista, todas
las sentencias son parametrizadas y el comportamiento transaccional es explícito.
Con un ORM habría que pelear con su capa de traducción precisamente en la
operación más delicada de la API.

### El stock solo se mueve por su propio endpoint

`PUT /api/products/{id}` actualiza nombre, descripción y precio, pero **no** el
stock. El inventario se mueve únicamente con `PATCH /api/products/{id}/stock`.
Así existe un solo camino hacia el stock, y ese camino es el que tiene control de
concurrencia: no hay forma de saltárselo por descuido.

### PATCH para el ajuste de stock

El enunciado admite PATCH o PUT. Se eligió **PATCH** porque la operación es un
ajuste *relativo* sobre el valor actual (`quantity: -3` significa «descuenta 3»),
no el reemplazo del recurso completo que PUT implica semánticamente.

### El cuerpo lleva una cantidad con signo

Una sola forma de expresar el movimiento: positivo ingresa, negativo descuenta.
La alternativa (`{"operation": "subtract", "amount": 3}`) obliga a validar un
campo de texto y a documentar su vocabulario sin ganar nada a cambio.

### Errores en formato ProblemDetails (RFC 7807)

Todas las respuestas de error —de validación, de negocio o inesperadas— comparten
la misma forma estándar, con un `traceId` que permite cruzar la respuesta del
cliente con los registros del servidor.

### Las reglas de negocio viven en la entidad

`Product` es la única pieza que decide si un dato es válido. Sus propiedades solo
se pueden modificar desde dentro, así que no existe forma de construir un producto
inconsistente. Las anotaciones de los contratos (`[Required]`, `[Range]`) no
duplican esa lógica: cumplen otra función, que es documentar el contrato en
Swagger y devolver un `400` con el detalle campo por campo antes de llegar al
dominio. Si se saltaran, la entidad seguiría rechazando el dato.

---

## Arquitectura

```
┌──────────────────────────────────────────────┐
│  ProductCatalog.Api                          │  Controladores, middleware de
│  (ASP.NET Core Web API)                      │  errores, configuración de Swagger
└──────────────────┬───────────────────────────┘
                   │ depende de
┌──────────────────▼───────────────────────────┐
│  ProductCatalog.Application                  │  Contratos (request/response),
│  (casos de uso)                              │  puertos e implementación de los
│                                              │  casos de uso
└──────────────────┬───────────────────────────┘
                   │ depende de
┌──────────────────▼───────────────────────────┐
│  ProductCatalog.Domain                       │  Entidad Product y sus reglas.
│  (reglas de negocio)                         │  No depende de nada.
└──────────────────▲───────────────────────────┘
                   │ implementa los puertos de Application
┌──────────────────┴───────────────────────────┐
│  ProductCatalog.Infrastructure               │  Repositorio MySQL con ADO.NET,
│  (acceso a datos)                            │  transacciones e inicialización
└──────────────────────────────────────────────┘
```

Dos detalles que explican la forma del diagrama:

- **Infraestructura implementa, no es implementada.** `Application` define el
  puerto `IProductRepository`; `Infrastructure` lo implementa. La API solo conoce
  las interfaces, y el motor de base de datos se podría cambiar tocando una sola
  capa.
- **El dominio no depende de nada.** Ni de ASP.NET, ni de MySQL, ni de paquetes
  externos. Por eso sus pruebas corren en milisegundos y sin infraestructura.

---

## Puesta en marcha

Solo se necesitan dos cosas, y la segunda probablemente ya la tenga:

1. [.NET SDK 8](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Un **MySQL 8** accesible (vale el que ya tenga instalado)

No hay más dependencias: ni contenedores, ni herramientas de migración, ni
paquetes globales.

```bash
git clone https://github.com/YairAlfredoVargasDelgado/prueba-hader.git
cd prueba-hader
dotnet run --project src/ProductCatalog.Api
```

Eso es todo. Al arrancar, la API **crea la base de datos y la tabla si no
existen**, así que no hay que ejecutar ningún script previo ni crear el esquema a
mano. La operación es idempotente: repetirla no tiene efectos secundarios.

Desde Visual Studio o Rider el equivalente es abrir `ProductCatalog.sln` y pulsar
**Ejecutar**; el navegador abre solo en Swagger.

| Recurso | URL |
| --- | --- |
| Swagger UI | <http://localhost:5140> |
| Contrato OpenAPI | <http://localhost:5140/swagger/v1/swagger.json> |
| Comprobación de vida | <http://localhost:5140/health> |

### Si su MySQL no es el de por defecto

La configuración de fábrica apunta a `localhost:3306` con usuario `root` y
contraseña `root`. Si la suya es distinta, ajústela en
`src/ProductCatalog.Api/appsettings.json` o expórtela como variable de entorno,
sin tocar el código:

```bash
export ConnectionStrings__ProductCatalog="Server=localhost;Port=3306;Database=product_catalog;User Id=root;Password=root;"
```

En Windows (PowerShell):

```powershell
$env:ConnectionStrings__ProductCatalog = "Server=localhost;Port=3306;Database=product_catalog;User Id=root;Password=root;"
```

El usuario indicado necesita permiso para crear la base de datos la primera vez.
Si prefiere crearla usted, el esquema está en
[`src/ProductCatalog.Infrastructure/Scripts/schema.sql`](src/ProductCatalog.Infrastructure/Scripts/schema.sql)
y puede desactivar la creación automática con `Database__AutoMigrate=false`.

### Ejecutar las pruebas

```bash
dotnet test
```

Las 35 pruebas son unitarias y **no necesitan base de datos**, así que corren
aunque no tenga MySQL levantado.

---

## Configuración

Todo se configura con variables de entorno, sin tocar el código. Es la vía que se
usa en el despliegue para no versionar credenciales.

| Variable | Descripción | Valor por defecto |
| --- | --- | --- |
| `ConnectionStrings__ProductCatalog` | Cadena de conexión a MySQL. **Obligatoria.** | La de `appsettings.json` (MySQL local) |
| `Database__AutoMigrate` | Si la API debe crear el esquema al arrancar. Póngala en `false` si el esquema se gestiona por otra vía. | `true` |
| `PORT` | Puerto de escucha. La inyectan proveedores como Railway o Render. | Puerto por defecto de ASP.NET Core |
| `ASPNETCORE_ENVIRONMENT` | Entorno de ejecución. | `Development` al ejecutar en local |

El esquema SQL está en
[`src/ProductCatalog.Infrastructure/Scripts/schema.sql`](src/ProductCatalog.Infrastructure/Scripts/schema.sql)
por si prefiere aplicarlo manualmente.

---

## Endpoints

Hay una colección lista para ejecutar en [`docs/api.http`](docs/api.http)
(compatible con REST Client de VS Code y con Rider).

| Método | Ruta | Descripción | Respuestas |
| --- | --- | --- | --- |
| `POST` | `/api/products` | Crea un producto | `201`, `400` |
| `GET` | `/api/products` | Lista paginada, con filtro opcional por nombre | `200`, `400` |
| `GET` | `/api/products/{id}` | Consulta un producto | `200`, `404` |
| `PUT` | `/api/products/{id}` | Actualiza nombre, descripción y precio | `200`, `400`, `404` |
| `DELETE` | `/api/products/{id}` | Elimina un producto | `204`, `404` |
| `PATCH` | `/api/products/{id}/stock` | Suma o resta unidades al stock | `200`, `400`, `404`, `409` |

### Crear un producto

```bash
curl -X POST http://localhost:5140/api/products \
  -H 'Content-Type: application/json' \
  -d '{
        "name": "Teclado mecánico RGB",
        "description": "Switch azul, retroiluminado, layout español.",
        "price": 189900.00,
        "stock": 25
      }'
```

`201 Created`, con la cabecera `Location` apuntando al recurso creado:

```json
{
  "id": 1,
  "name": "Teclado mecánico RGB",
  "description": "Switch azul, retroiluminado, layout español.",
  "price": 189900.00,
  "stock": 25,
  "createdAt": "2026-09-29T19:59:27.2830734Z",
  "updatedAt": "2026-09-29T19:59:27.2830734Z"
}
```

**Reglas de validación:** el nombre es obligatorio y admite hasta 150 caracteres;
la descripción es opcional y admite hasta 500; el precio no puede ser negativo y
admite como máximo 2 decimales; el stock inicial no puede ser negativo.

### Listar con paginación

```bash
curl "http://localhost:5140/api/products?page=1&pageSize=5&search=teclado"
```

```json
{
  "items": [ { "id": 1, "name": "Teclado mecánico RGB", "...": "..." } ],
  "page": 1,
  "pageSize": 5,
  "totalItems": 13,
  "totalPages": 3,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

`page` empieza en 1 y `pageSize` admite entre 1 y 100 (por defecto 10); fuera de
ese rango la respuesta es `400`. El orden es por identificador descendente —lo más
reciente primero— y al ser la clave primaria el orden es estable, de modo que la
paginación no repite ni omite filas. `search` filtra por coincidencia parcial en
el nombre, sin distinguir mayúsculas ni acentos.

### Ajustar el stock

Un valor positivo ingresa mercancía y uno negativo la descuenta:

```bash
curl -X PATCH http://localhost:5140/api/products/1/stock \
  -H 'Content-Type: application/json' \
  -d '{"quantity": -3}'
```

`200 OK` con el producto y su stock vigente. Si el ajuste dejara el stock en
negativo, la respuesta es `409 Conflict` y **el stock no se modifica**:

```json
{
  "title": "Stock insuficiente",
  "status": 409,
  "detail": "Stock insuficiente: el producto tiene 22 unidades y se solicitó un ajuste de -9999.",
  "instance": "/api/products/1/stock",
  "currentStock": 22,
  "requestedChange": -9999,
  "traceId": "00-1def1ea7196c6096f749b56ce7e105a2-2cff8d98835b4ebe-00"
}
```

Un `quantity` de `0` se rechaza con `400`: no es un movimiento de inventario.

---

## Manejo de errores

Un middleware traduce toda excepción a una respuesta
[ProblemDetails](https://datatracker.ietf.org/doc/html/rfc7807). Los controladores
no llevan un solo `try/catch`.

| Situación | Código | Título |
| --- | --- | --- |
| Campos inválidos o regla de negocio incumplida | `400` | `Solicitud inválida` |
| El producto no existe | `404` | `Producto no encontrado` |
| El ajuste dejaría el stock en negativo | `409` | `Stock insuficiente` |
| Cualquier fallo inesperado | `500` | `Error interno del servidor` |

La elección de `409` para el stock es deliberada: la petición está bien formada
—no es culpa del cliente haber enviado algo inválido—, lo que ocurre es que choca
con el estado actual del recurso. Eso es exactamente un conflicto, no un `400`.

Los errores de validación de campos incluyen el detalle por campo:

```json
{
  "title": "Solicitud inválida",
  "status": 400,
  "detail": "Uno o más campos no superaron la validación.",
  "instance": "/api/products",
  "errors": {
    "Name": ["El nombre del producto es obligatorio."],
    "Price": ["El precio no puede ser negativo."]
  },
  "traceId": "00-ac1f3362e3f0986cb24bbc85fd62e063-d55e8d4a557f676a-00"
}
```

Los fallos inesperados nunca exponen detalles internos: se registran en el
servidor y el cliente recibe un mensaje genérico con el `traceId` para poder
rastrearlo.

---

## Concurrencia: por qué el stock nunca queda en negativo

El enunciado subraya que la API será «el núcleo transaccional consumido por
múltiples plataformas concurrentes». Validar el stock en memoria no basta: si dos
peticiones leen el mismo valor antes de que ninguna haya escrito, ambas creerán
que hay inventario suficiente y una de las dos se perderá (*lost update*).

La solución está en `ProductRepository.AdjustStockAsync`: el ajuste ocurre dentro
de una transacción que **bloquea la fila antes de leerla**.

```sql
START TRANSACTION;
SELECT ... FROM products WHERE id = @id FOR UPDATE;  -- la fila queda bloqueada
-- el dominio valida que el resultado no sea negativo
UPDATE products SET stock = @stock WHERE id = @id;
COMMIT;                                              -- aquí se libera
```

Una segunda petición sobre el mismo producto espera en el `SELECT ... FOR UPDATE`
hasta el `COMMIT` de la primera, así que siempre lee el stock ya actualizado. Los
ajustes se serializan por producto, no a nivel global: peticiones sobre productos
distintos no se estorban.

Como red adicional, la tabla lleva una restricción `CHECK (stock >= 0)`: aunque
alguien escribiera en la base saltándose la API, el dato incorrecto no entraría.

### Reprodúzcalo

El repositorio incluye un script que lo comprueba:

```bash
./scripts/prueba-concurrencia.sh http://localhost:5140 30 50
```

Crea un producto con 30 unidades y lanza 50 descuentos de 1 unidad **en paralelo**.
Resultado obtenido:

```
  respuestas:      30 200
  respuestas:      20 409

  stock final: 0

OK: no se perdió ningún ajuste y el stock nunca quedó en negativo.
```

Exactamente 30 aciertos, 20 rechazos y stock final 0. Ni un ajuste perdido, ni un
stock negativo. Con una condición de carrera se verían más de 30 respuestas `200`
o un stock final distinto de 0.

---

## Pruebas

```bash
dotnet test
```

35 pruebas unitarias, sin necesidad de base de datos:

- **Dominio** — las invariantes de `Product`: nombre obligatorio y acotado,
  precio no negativo y con máximo 2 decimales, stock inicial no negativo, ajustes
  que dejarían el stock en negativo (verificando además que el producto queda
  intacto tras el rechazo) y desbordamiento del entero.
- **Aplicación** — la orquestación de los casos de uso contra un repositorio en
  memoria escrito a mano: qué se devuelve, qué metadatos de paginación se calculan
  y qué excepción se propaga en cada caso.

El repositorio en memoria se escribió a mano en lugar de usar una librería de
mocks, en coherencia con la decisión de mantener las dependencias al mínimo.

---

## Despliegue público

La API no impone nada al proveedor. Para publicarla solo hace falta que el
servicio sepa ejecutar una aplicación **.NET 8** y darle dos cosas:

| Qué | Para qué |
| --- | --- |
| `ConnectionStrings__ProductCatalog` | Apuntar al MySQL del proveedor |
| Nada más | El puerto se toma de la variable `PORT` si el proveedor la inyecta, y el esquema se crea solo en el primer arranque |

### Railway (tiene MySQL gestionado en el mismo proyecto)

1. Cree un proyecto en [railway.app](https://railway.app) y elija
   **Deploy from GitHub repo** apuntando a este repositorio. Detecta el proyecto
   .NET y lo compila sin configuración adicional.
2. Añada un servicio **MySQL** al mismo proyecto (*New → Database → MySQL*).
3. En el servicio de la API, defina la variable:

   ```
   ConnectionStrings__ProductCatalog = Server=${{MySQL.MYSQLHOST}};Port=${{MySQL.MYSQLPORT}};Database=${{MySQL.MYSQLDATABASE}};User Id=${{MySQL.MYSQLUSER}};Password=${{MySQL.MYSQLPASSWORD}};
   ```

4. En *Settings → Networking*, pulse **Generate Domain**.

La URL generada abre directamente en Swagger.

### Alternativas

- **MonsterASP.NET** — hosting gratuito pensado para ASP.NET Core y que incluye
  una base MySQL; el despliegue se hace publicando el resultado de
  `dotnet publish -c Release`.
- **Azure App Service** — soporta .NET de forma nativa y tiene una capa gratuita;
  la base MySQL se contrata aparte o se apunta a una externa.

> **Estado:** el despliegue público queda pendiente de ejecutarse con una cuenta
> propia del proveedor. La aplicación ya está preparada (configuración por
> variables de entorno, soporte de `PORT` y creación automática del esquema) y
> verificada en local. Una vez generada la URL, añádala aquí.

---

## Estructura del repositorio

```
.
├── src/
│   ├── ProductCatalog.Domain/           # Entidad Product y excepciones de dominio
│   │   ├── Entities/Product.cs          #   todas las reglas de negocio
│   │   └── Exceptions/
│   ├── ProductCatalog.Application/      # Casos de uso
│   │   ├── Contracts/                   #   DTOs de entrada y salida
│   │   ├── Abstractions/                #   puertos (IProductRepository, IProductService)
│   │   └── Services/ProductService.cs
│   ├── ProductCatalog.Infrastructure/   # Acceso a datos
│   │   ├── Persistence/                 #   repositorio ADO.NET, fábrica de conexiones
│   │   └── Scripts/schema.sql           #   esquema, embebido en el ensamblado
│   └── ProductCatalog.Api/              # Capa HTTP
│       ├── Controllers/                 #   endpoints
│       ├── Middleware/                  #   traducción de errores a ProblemDetails
│       └── Program.cs                   #   composición y configuración de Swagger
├── tests/ProductCatalog.Tests/          # 35 pruebas unitarias
├── scripts/prueba-concurrencia.sh       # Demostración del control de concurrencia
├── docs/api.http                        # Colección de peticiones de ejemplo
└── ProductCatalog.sln                   # Solución: ábrala y ejecute
```
