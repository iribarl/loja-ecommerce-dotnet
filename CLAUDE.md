# E-commerce Genérico Moderno

## Contexto (para Claude)

Tienda online configurable para cualquier rubro (ropa, repuestos, alimentos), orientada a Brasil: pago con Pix y tarjeta, envío por CEP, LGPD. Objetivo principal: **portafolio técnico** que demuestre un sistema completo de punta a punta con arquitectura profesional. No es para competir con Shopify o Nuvemshop.

Soy desarrollador junior en .NET. Proyecto de aprendizaje.

## Cómo trabajar conmigo

- Explica cada decisión no obvia, sobre todo de arquitectura y patrones.
- Trabaja por fases; no avances sin mi confirmación.
- Plan antes de cambios de más de 3 archivos.
- Pasos pequeños; compila y tests tras cada cambio.
- No agregues paquetes sin justificar.
- Código en inglés; textos de la tienda en portugués de Brasil.
- Conventional Commits.

## Reglas

- Solo datos ficticios (productos, clientes, pedidos). Script de seed para poblar la demo.
- Pagos siempre en **modo sandbox/test** del gateway en desarrollo y en la demo.
- Secretos en `dotnet user-secrets` / variables de entorno.
- Nunca guardar datos de tarjeta: el gateway tokeniza.

## Alcance

### MVP

- Catálogo: categorías, productos con variantes (talla, color), imágenes, stock por variante.
- Búsqueda por texto y filtros.
- Carrito (anónimo y de usuario) y checkout.
- Pago con **Pix** vía gateway (Mercado Pago, Pagar.me o Stripe: elegir en Fase 0), confirmado **por webhook**, nunca por el front.
- Pedidos con máquina de estados.
- Envío: tabla fija por región/CEP en el MVP.
- Cuenta de cliente: registro, login, historial de pedidos.
- Panel administrativo: productos, stock, pedidos.

### Después del MVP

- Tarjeta de crédito, cupones.
- IA: búsqueda semántica, descripciones generadas, chat de atención.
- n8n: carrito abandonado, postventa, stock bajo.
- Cálculo de envío por API (Correios / agregadores).
- Emisión de NF-e mediante API de terceros.
- Multi-tienda.

## Stack

- .NET 10 (LTS), ASP.NET Core, **monolito modular**
- Front: Blazor Web App (renderizado en servidor + interactividad donde haga falta)
- PostgreSQL + EF Core; Redis para caché del catálogo (el carrito vive en Postgres, ver "Decisiones")
- .NET Aspire para orquestar Postgres, Redis, API y web en desarrollo
- Auth: ASP.NET Core Identity
- Eventos internos: patrón **Outbox** (tabla en Postgres + `BackgroundService` que publica)
- IA: Claude para descripciones y chat (tool use). Embeddings para búsqueda semántica: Claude no genera embeddings; usar un proveedor de embeddings (p. ej., Voyage AI) o un modelo local, guardados con pgvector
- Observabilidad: OpenTelemetry (ServiceDefaults)
- Tests: xUnit, Shouldly, Testcontainers; tests de arquitectura (NetArchTest) para que los módulos no se crucen
- Deploy de demo: contenedor en un servicio barato (Fly.io, Railway, Azure Container Apps)

## Arquitectura

Monolito modular: un solo deploy, módulos con límites claros. Cada módulo tiene su `DbContext` y su esquema en PostgreSQL. Los módulos se comunican por **contratos públicos** o **eventos**, nunca accediendo a las tablas de otro.

```
Loja.sln
src/
  Loja.AppHost/
  Loja.ServiceDefaults/
  Loja.Web/                     # Host único: Blazor (tienda + panel admin) + endpoints HTTP
  Loja.Api/                     # Biblioteca de grupos de endpoints (incluye webhooks de pago)
  Modules/
    Catalog/   (Domain, Application, Infrastructure, Contracts)
    Cart/
    Orders/
    Payments/
    Customers/
    Shipping/
  Shared/
    Loja.SharedKernel/          # Result, Entity, DomainEvent, Money
    Loja.Outbox/
tests/
  Modules.*.Tests/
  Loja.ArchitectureTests/
  Loja.IntegrationTests/
```

## Dominio clave

- `Money` como value object (monto + moneda, BRL por defecto). Nunca `double` para dinero.
- `Product` → `ProductVariant` (SKU, atributos, precio, stock).
- **Reserva de stock**: al crear el pedido se reserva; se confirma al pagar; se libera si el pago vence (Pix con expiración).
- Estados del pedido:

```
Criado → AguardandoPagamento → Pago → EmSeparacao → Enviado → Entregue
            ↓ (expira)            ↓
         Cancelado            Reembolsado
```

Transiciones solo a través de métodos del agregado `Order` (nada de setear el estado desde fuera).

## Pagos

- Crear cobro Pix en el gateway → mostrar QR y copia-e-cola.
- Webhook del gateway → validar firma → **idempotencia** (guardar el id del evento; si ya fue procesado, ignorar) → evento `PaymentConfirmed` → `Orders` cambia a `Pago`.
- Job que cancela pedidos con Pix expirado.

## Fases

### Fase 0 — Setup
- Solución, Aspire (Postgres + Redis), ServiceDefaults, CI, tests de arquitectura vacíos.
- Elegir gateway de pago (registrar en "Decisiones").

### Fase 1 — Catálogo
- Productos, variantes, categorías, imágenes; seed de datos ficticios.
- Páginas: listado con filtros, detalle de producto. Caché Redis.

### Fase 2 — Carrito y clientes
- Carrito anónimo (cookie) que se fusiona al iniciar sesión. Identity.

### Fase 3 — Pedidos y stock
- Agregado `Order`, máquina de estados, reserva de stock, outbox.

### Fase 4 — Pagos Pix
- Integración sandbox, webhook idempotente, expiración.
- **Hecho cuando**: una compra completa en sandbox termina en `Pago` con stock descontado.

### Fase 5 — Panel administrativo
- CRUD de productos y stock, lista de pedidos con cambio de estado.

### Fase 6 — Deploy de demo
- Contenedor, base gestionada, dominio, datos ficticios, README con capturas y diagrama.
- Pendientes detectados en fases anteriores:
  - Migraciones y seed en producción (hoy solo se aplican al arrancar en Development).
  - ~~Versionar las claves de caché~~ hecho en la Fase 5 (`CatalogCache.KeyPrefix`).
  - La imagen del contenedor debe incluir ICU (datos de idioma) para el formato `pt-BR` de precios.
  - CI: `dotnet tool restore` si algún paso usa `dotnet-ef`.
  - Cuenta admin en producción: hoy se crea al arrancar solo en Development (`Admin:Email` / `Admin:Password`).
  - Imágenes subidas en el panel: hoy en disco (`LocalImageStorage`, carpeta `uploads/produtos`). En un contenedor ese disco se pierde al redesplegar: usar un volumen o implementar `IImageStorage` con un blob storage.

### Fase 7+ — IA y automatización
- Descripción de producto generada desde el panel (Claude).
- Búsqueda semántica con embeddings + pgvector.
- Chat de atención con tool use: `get_order_status`, `search_products`.
- n8n vía eventos del outbox (webhook): carrito abandonado, postventa, stock bajo.

## Convenciones de arquitectura

Estas reglas valen para todo el código del proyecto. Si alguna no encaja, pregúntame antes de romperla.

### Tipo de despliegue

- **Monolito** (o monolito modular): una sola aplicación desplegable. **No usar microservicios.**
- Un worker separado de la API solo cuando una fase lo indique explícitamente.

### Capas (Clean Architecture)

| Capa | Contiene | Puede depender de |
| --- | --- | --- |
| Domain | Entidades, value objects, reglas de negocio, eventos de dominio | Nada |
| Application | Casos de uso (handlers), interfaces (`IRepository`, `ILlmClient`...), DTOs | Domain |
| Infrastructure | EF Core, clientes HTTP externos (Claude, gateways), archivos, email | Application, Domain |
| Api / Web | Endpoints, configuración, inyección de dependencias | Application (e Infrastructure solo para registrar servicios) |

- Las dependencias apuntan hacia adentro: el dominio no conoce EF Core, HTTP ni Claude.
- Si el proyecto tiene un único `Core`, este contiene Domain y Application en carpetas separadas con las mismas reglas.
- En un monolito modular, cada módulo tiene sus propias capas y su esquema en la base de datos. Un módulo nunca lee las tablas de otro: usa sus `Contracts` o eventos.

### Minimal APIs

- Endpoints agrupados por funcionalidad, un archivo por grupo:

```csharp
// Api/Endpoints/ProjectEndpoints.cs
public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjectEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects").WithTags("Projects");

        group.MapPost("/", async (CreateProjectRequest request, CreateProjectHandler handler, CancellationToken ct) =>
        {
            var result = await handler.HandleAsync(request, ct);
            return result.IsSuccess
                ? TypedResults.Created($"/api/projects/{result.Value.Id}", result.Value)
                : result.ToProblem();
        });

        group.MapGet("/{id:guid}", async (Guid id, GetProjectHandler handler, CancellationToken ct) =>
            await handler.HandleAsync(id, ct) is { } project
                ? Results.Ok(project)
                : Results.NotFound());

        return app;
    }
}
```

- `Program.cs` solo registra servicios y llama a `app.MapProjectEndpoints()`, `app.MapXxxEndpoints()`...
- Endpoints delgados: recibir, validar, llamar al caso de uso, traducir el resultado a HTTP. **Sin lógica de negocio en el endpoint.**
- Casos de uso como clases simples (`CreateProjectHandler`) registradas en DI. Sin MediatR salvo que haya un motivo claro.
- Errores esperados (no encontrado, validación, conflicto) con un tipo `Result<T>`, no con excepciones. Excepciones solo para lo inesperado.
- Errores HTTP en formato **ProblemDetails** (`AddProblemDetails()` + manejador global de excepciones).
- Validación: la integrada de Minimal APIs en .NET 10 o FluentValidation. Decidir en la Fase 0 y registrar en "Decisiones".
- Documentación: OpenAPI (`Microsoft.AspNetCore.OpenApi`) con una UI como Scalar en desarrollo.

### Organización de un caso de uso

```
Application/
  Projects/
    CreateProject/
      CreateProjectRequest.cs
      CreateProjectHandler.cs
      CreateProjectValidator.cs
    GetProject/
      GetProjectHandler.cs
      ProjectResponse.cs
```

Organizar por funcionalidad (vertical), no por tipo de archivo (no una carpeta gigante `Services/` ni `DTOs/`).

### Tests por capa

- Domain y Application: tests unitarios, sin base de datos.
- Infrastructure: tests de integración con Testcontainers (PostgreSQL real).
- Api: tests de endpoints con `WebApplicationFactory`.
- Si el proyecto lo permite, tests de arquitectura (NetArchTest) que fallen si una capa depende de otra que no debe.

## Estado

- [x] Fase 0 — Setup
- [x] Fase 1 — Catálogo
- [x] Fase 2 — Carrito y clientes
- [x] Fase 3 — Pedidos y stock
- [x] Fase 4 — Pagos Pix (con gateway falso; Mercado Pago sandbox pendiente, ver "Decisiones")
- [ ] Fase 5 — Panel admin
- [ ] Fase 6 — Deploy de demo
- [ ] Fase 7+ — IA y automatización

## Decisiones tomadas

(Registrar con fecha y motivo.)

- **2026-10-20 — Gateways de pago: arquitectura multi-gateway, Mercado Pago primero.**
  - `Payments` define `IPaymentGateway` en Application; cada gateway es una implementación en Infrastructure (patrón Strategy), registrada con keyed services de .NET y elegida por un `IPaymentGatewayResolver` (configuración/reglas, no el cliente; en la demo puede haber un selector visible).
  - `Payment` guarda el gateway usado y su id externo. Un endpoint de webhook por gateway (`/api/webhooks/{gateway}`), cada uno valida su firma y publica el mismo evento interno `PaymentConfirmed`. Idempotencia por (gateway, id de evento).
  - MVP: solo **Mercado Pago** + `FakePaymentGateway` para tests. Stripe después del MVP.
  - Pendiente: verificar cómo se simula la aprobación de un Pix en el sandbox de Mercado Pago (necesario para cerrar la Fase 4).
  - Motivo: Mercado Pago es el más reconocido en Brasil y tiene SDK oficial .NET; el diseño multi-gateway evita acoplarse a un proveedor y deja añadir otro tocando solo Infrastructure.

- **2026-10-20 — Validación: la integrada de Minimal APIs en .NET 10 (`AddValidation()` + DataAnnotations).**
  - Los validadores solo comprueban forma (obligatorios, rangos, formatos); las reglas de negocio viven en el dominio.
  - Ojo: el generador de código de validación solo ve los endpoints y DTOs del proyecto que llama a `AddValidation()`. Por eso se llama dentro de `Loja.Api` (`AddLojaApi()`), no en el host. Verificado: llamado desde el host, la validación no se aplicaba.
  - Motivo: sin paquetes extra, errores como ProblemDetails automáticamente y es la opción moderna de .NET 10. Si aparece un caso complejo, se evalúa FluentValidation puntualmente y se registra aquí.

- **2026-10-20 — CI: GitHub Actions.** Un workflow `restore → build → test` en push y PR a `main` (runner Linux con Docker para Testcontainers). Rama principal: `main`.
  - Motivo: el portafolio vive en GitHub, el estado del CI queda visible y es gratis para repos públicos.

- **2026-10-20 — Tests: xUnit v3 sobre Microsoft.Testing.Platform (MTP), no VSTest.**
  - Activado en `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`). Comandos: `dotnet test --solution Loja.slnx` o `dotnet test --project <ruta>`.
  - Sin `Microsoft.NET.Test.Sdk` ni `xunit.runner.visualstudio` (solo los usa VSTest).
  - Motivo: xUnit v3 4.x usa MTP por defecto y el SDK de .NET 10 ya no ejecuta MTP a través de VSTest; es el motor recomendado y requiere menos paquetes.

- **2026-10-20 — Un solo host: `Loja.Web` es el único ejecutable.**
  - Sirve Blazor y también los endpoints HTTP (webhooks, OpenAPI/Scalar, integraciones). `Loja.Api` es una biblioteca de grupos de endpoints (`MapXxxEndpoints()`) que el host registra.
  - Las páginas Blazor llaman a los casos de uso (handlers) en proceso; los endpoints HTTP son para clientes externos (gateway, n8n, futuras apps).
  - Motivo (host único): cumple "una sola aplicación desplegable", simplifica Identity (una cookie) y la demo usa un solo contenedor. Los límites entre módulos los garantizan los proyectos y los tests de arquitectura, no los procesos.

- **2026-10-20 — SharedKernel: `Money`, `Result`/`Error`, `Entity<TId>`, `DomainEvent`; ids fuertemente tipados.**
  - `Money`: record inmutable, `decimal` + moneda ISO 4217 (BRL por defecto), redondeo a 2 decimales `AwayFromZero`; mezclar monedas lanza excepción (error de programación).
  - `Result`/`Result<T>` con `Error(Code, Message, ErrorType)`; `ToProblem()` en `Loja.Api` mapea `Validation`→400, `NotFound`→404, `Conflict`→409.
  - Ids: `readonly record struct` por entidad (p. ej. `ProductId(Guid Value)`), con `ValueConverter` en EF Core. Guids v7 (`Guid.CreateVersion7()`): ordenables, mejor para índices.
  - Motivo: el compilador impide confundir ids de entidades distintas; errores esperados sin excepciones.

- **2026-10-20 — Catálogo (Fase 1).**
  - Búsqueda por texto: full-text de Postgres con configuración `portuguese` (tsvector + índice GIN), no `ILIKE`.
  - Migraciones y seed: se aplican al arrancar **solo en Development**. Producción se decide en la Fase 6.
  - Seed: datos ficticios escritos a mano y deterministas (sin Bogus). Rubro de la demo: **ropa**.
  - Atributos de variante genéricos (`{"Tamanho": "M", "Cor": "Azul"}`, jsonb) para servir a cualquier rubro.
  - Lecturas: interfaces de consulta en Application (`IProductQueries`) que devuelven DTOs; Infrastructure las implementa con EF Core (`AsNoTracking` + proyección). Caché con `HybridCache` (memoria + Redis) como decorador.
  - `Loja.Catalog.Contracts` se crea cuando otro módulo lo necesite (Fase 2).

- **2026-10-20 — Caché del catálogo: `HybridCache` (memoria + Redis), decorador sobre las consultas.**
  - Expiración 5 min en Redis, 1 min en memoria local. Todas las entradas llevan la etiqueta `catalog`: un cambio en el catálogo (Fase 5) invalida con `RemoveByTagAsync("catalog")`.
  - El stock mostrado desde la caché es **solo informativo**: carrito y checkout siempre validan stock contra la base de datos.
  - Redis es opcional en el host: sin cadena de conexión (tests), `HybridCache` funciona solo en memoria.
  - `HybridCache` devuelve copias deserializadas, no la misma instancia.

- **2026-10-20 — Carrito y clientes (Fase 2).**
  - Carrito en **Postgres** (módulo `Cart`, esquema `cart`), no en Redis: duradero, consultable (carrito abandonado) y fusión transaccional.
  - El carrito guarda solo variante + cantidad. Precio, nombre, imagen y stock se consultan al catálogo al mostrarlo; el precio se congela al crear el pedido (Fase 3).
  - Comunicación entre módulos por `Loja.<Modulo>.Contracts` (p. ej. `ICatalogApi`). El stock de los contratos se lee de la base, nunca de la caché.
  - Carrito anónimo: cookie con id aleatorio (`HttpOnly`, `Secure`, 30 días) emitida por un middleware en la primera petición; la fila se crea al añadir el primer producto. Motivo: los componentes interactivos (SignalR) no pueden escribir cookies. Cookie estrictamente necesaria (LGPD: sin consentimiento).
  - Clientes: módulo `Customers` con ASP.NET Core Identity (esquema `customers`). Sin confirmación por email por ahora. Datos mínimos (nombre, email) + aceptación de la política de privacidad.
  - Fusión al iniciar sesión en el evento de cookie de Identity (`OnSignedIn`), conectado por el host: `Customers` no conoce `Cart`.

- **2026-10-20 — Pedidos y stock (Fase 3).**
  - Checkout solo con cuenta (compra sin cuenta: después del MVP).
  - Módulo `Shipping` en esta fase: tabla fija por región según el primer dígito del CEP, envío gratis desde cierto subtotal. Dirección escrita a mano (ViaCEP después).
  - Stock en el Catálogo con reservas: reservar = descontar con `UPDATE ... WHERE stock >= n` (atómico); tabla `catalog.stock_reservations` por pedido, liberar/confirmar idempotentes. Sin transacción distribuida: primero se reserva, luego se guarda el pedido; las reservas huérfanas caducan.
  - Outbox por módulo (tabla `outbox_messages` en su esquema, escrita en la misma transacción mediante un interceptor de EF que traduce eventos de dominio a eventos de integración de los `Contracts`). `BackgroundService` con `FOR UPDATE SKIP LOCKED`, despacho en proceso, reintentos. Inbox (`inbox_messages`) en el consumidor para idempotencia.
  - Síncrono por contrato: reservar stock y vaciar el carrito. Por evento + outbox: devolver stock al cancelar (y confirmar al pagar en la Fase 4). Caducidad del pedido sin pagar: Fase 4.

- **2026-10-20 — Dirección por CEP con ViaCEP.** Contrato `IAddressLookup` en `Shipping`; `HttpClient` tipado (5 s) y caché de 1 día. Si falla, el cliente escribe la dirección a mano (nunca bloquea la compra). Solo se envía el CEP. Los tests usan un `IAddressLookup` falso: nunca llaman a internet.

- **2026-10-20 — Pagos Pix (Fase 4).**
  - Módulo `Payments` (esquema `payments`). Primero un `FakePixGateway` (solo Development y tests, con página `/dev/pix/{id}` que envía el webhook firmado); Mercado Pago sandbox después, implementando el mismo `IPaymentGateway`.
  - El cobro se crea de forma síncrona tras confirmar el pedido; si falla, la página del pedido permite "Gerar Pix novamente".
  - Webhook: firma HMAC-SHA256 al estilo Mercado Pago (`x-signature: ts=…,v1=…`, tolerancia de tiempo, comparación en tiempo constante) → idempotencia por (gateway, id del evento) → el estado real se CONSULTA al gateway (el contenido del webhook no se usa como verdad).
  - Cadena por outbox: `PixChargeCreated` → pedido `AwaitingPayment`; `PaymentConfirmed` → pedido `Paid` → `OrderPaid` → reserva de stock confirmada; `PaymentExpired` (Pix sin pagar en 30 min) → pedido cancelado → stock devuelto.
  - La página del pedido consulta el estado cada 3 s (script mínimo) en lugar de abrir un WebSocket.
  - Fase 4 cerrada con el gateway falso: un test recorre checkout → "Simular pagamento" (webhook firmado) → pedido `Paid` → reserva `Confirmed` con stock descontado.
  - **Pendiente:** `MercadoPagoPixGateway` en sandbox cuando haya credenciales (en user-secrets; los webhooks necesitan un túnel). Solo añade una implementación de `IPaymentGateway` en Infrastructure.

- **2026-10-21 — Panel administrativo (Fase 5): rol `Admin` de Identity.**
  - El personal de la tienda son clientes con el rol `Admin` (constante en `Loja.Customers.Contracts`). El rol viaja como claim en la cookie: sin consulta por petición.
  - Páginas en `Components/Pages/Admin/`: su `_Imports.razor` aplica `AdminLayout` y `[Authorize(Roles = Admin)]` a todas, así ninguna página nueva queda abierta por olvido.
  - Cuenta admin desde configuración (`Admin:Email`, `Admin:Password` en user-secrets de `Loja.Web`), creada al arrancar en Development. Sin cuenta configurada, el panel no tiene usuario (aviso en el log).
  - Productos: escritura por `IProductRepository` (agregado completo) y lectura del panel por `IProductAdminQueries` (sin caché, incluye borradores y stock real). Cada cambio vacía la caché de la tienda (`ICatalogCacheInvalidator` → `RemoveByTagAsync("catalog")`).
  - El slug no cambia al renombrar (la URL del producto es estable); al crear, si existe, se usa `-2`, `-3`... El SKU es único en todo el catálogo y no se edita (lo usan reservas y pedidos).
  - Atributos de variante escritos como texto, un "Nombre: valor" por línea (`AttributesText`).
  - Pedidos: el panel muestra solo las acciones que permite el estado actual (`OrderAction`: separar, enviar con código de rastreo, entregar, cancelar sin pagar con motivo) y cada una llama al método del agregado `Order`. Si el pedido cambió mientras el admin lo veía (xmin), se avisa en lugar de un error 500 (`SaveIfUnchangedAsync`). Sin reembolsos desde el panel: requieren devolver el dinero en el gateway.
  - Búsqueda por código de pedido: SQL que devuelve solo ids y luego LINQ. `FromSql` sobre `Order` no sirve: `SELECT *` omite `xmin` y EF nombra mal las columnas de los tipos complejos anidados.
  - **Pendiente (con Mercado Pago):** al cancelar un pedido con Pix pendiente, anular el cobro en el gateway; si un pago llega para un pedido ya cancelado, reembolsarlo. Hoy el pago quedaría confirmado en Payments y el pedido seguiría cancelado.
  - Imágenes: subida de archivos desde el panel detrás de `IImageStorage` (Application); hoy `LocalImageStorage` en disco, servido en `/uploads/produtos` con `UseStaticFiles`. Seguridad: formato por los primeros bytes (JPG, PNG, WebP; **SVG no**, puede llevar scripts), máximo 5 MB leídos de verdad (no el tamaño declarado), nombre de archivo generado por el servidor, antiforgery en el endpoint. Hasta 10 imágenes por producto; opcionalmente asociadas a una variante ("Cor: Preto"). La subida es un formulario HTML a un endpoint Minimal API porque `InputFile` necesita una página interactiva.
  - Procesamiento (`IImageProcessor`, implementado con **SkiaSharp**): resolución máxima 8000 px comprobada en la cabecera antes de decodificar (bombas de descompresión), orientación EXIF aplicada, metadatos eliminados (GPS de fotos de celular), tres tamaños WebP que caben en un cuadrado de 160, 480 y 1200 px sin agrandar (`{id}-160.webp`...). El producto guarda la URL de 1200; `ImageSizes` deriva las otras y el `srcset`.
    - Por qué SkiaSharp (MIT): ImageSharp 3.x tiene vulnerabilidades altas sin parche y la 4.x exige una clave de licencia (registro) para compilar en Release. Para el contenedor Linux se usa `SkiaSharp.NativeAssets.Linux.NoDependencies`.
  - Tienda: marco cuadrado (1:1) con la foto completa (`object-fit: contain`), sin recortar ni deformar. Galería con scroll-snap (deslizar sin JavaScript) + `js/gallery.js` (flechas, miniaturas, teclado), en la ficha y en las tarjetas (hasta 5 fotos). Solo manual, sin autoplay (accesibilidad). Elegir una variante muestra su foto.
  - Claves de caché versionadas (`catalog:v2:`): subir la versión cuando un tipo cacheado cambie de forma.
  - **Bug encontrado (2026-10-21):** con Redis, el contenedor podía crear **dos** singletons de HybridCache. `RedisCacheImpl` (el `IDistributedCache` por defecto) pide `HybridCache` al contenedor al conectar, y HybridCache conecta a Redis mientras se construye: la pregunta reentra en su propia construcción. Lo invalidado en uno no lo veía el otro, y la tienda mostraba productos viejos tras editarlos en el panel. Solución en `Program.cs`: registrar `IDistributedCache` como `RedisCache` (clase pública, no consulta el contenedor). Tests con Redis real (`HybridCacheRedisTests`) que fallan sin la solución.
  - Stock: el panel solo hace **ajustes relativos con motivo** (`+10 reposição`, `-2 avaria`), nunca "el stock ahora es N", para no pisar una compra simultánea. `UPDATE ... SET Stock = Stock + n WHERE Stock + n >= 0` atómico y, en la misma transacción, una fila en `catalog.stock_adjustments` (cantidad, motivo, stock resultante, quién, cuándo): historial auditable.
  - Sin control de concurrencia entre dos admins editando el mismo producto (gana el último): aceptable para la demo.
