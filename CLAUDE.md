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
- PostgreSQL + EF Core; Redis para caché de catálogo y carrito
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
- [ ] Fase 1 — Catálogo
- [ ] Fase 2 — Carrito y clientes
- [ ] Fase 3 — Pedidos y stock
- [ ] Fase 4 — Pagos Pix
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
