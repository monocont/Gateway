# Prompt: Crear un API Gateway con Ocelot en .NET

Actúa como un arquitecto y desarrollador senior especializado en .NET y microservicios. Tu tarea es diseñar e implementar un proyecto **API Gateway** utilizando **Ocelot** dentro del ecosistema **ASP.NET Core**.

## Objetivo

Crear un proyecto Gateway que centralice el acceso a múltiples servicios backend mediante **Ocelot**, proporcionando enrutamiento, validación de autenticación por firma y control básico de seguridad.

El proyecto debe generarse completamente desde cero, incluyendo la **solución (.sln)** y la estructura adecuada para un proyecto mantenible y escalable.

## Requisitos técnicos

* Framework: .NET 8 o superior
* Tipo de proyecto: ASP.NET Core Web API
* Gateway: Ocelot
* Configuración mediante archivo `ocelot.json`
* Arquitectura limpia y organizada
* Código claro, modular y documentado

El proyecto debe ejecutarse en:

* **Puerto 4000**

La configuración del servidor debe establecer explícitamente el puerto para que el gateway se levante en:

```
http://localhost:4000
```

## Estructura de solución

El agente debe generar una **solución completa (.sln)** que contenga el proyecto del gateway.

Estructura esperada:

```
GatewaySolution.sln

src/
  Gateway/
    Gateway.csproj
    Program.cs
    ocelot.json

    Middleware/
      SignatureValidationMiddleware.cs

    Services/
      SignatureValidationService.cs

    Configuration/
      GatewaySettings.cs
```

## Alcance funcional

El gateway debe:

1. Actuar como punto único de entrada para los clientes.
2. Enrutar solicitudes hacia múltiples microservicios backend.
3. Validar autenticación basada en firma antes de reenviar la solicitud.
4. Manejar encabezados de autenticación personalizados.
5. Implementar manejo de errores adecuado.
6. Permitir escalar fácilmente agregando nuevas rutas.

## Reglas de autenticación por firma

El gateway debe validar solicitudes basadas en los siguientes headers:

* `X-Api-Key`
* `X-Timestamp`
* `X-Signature`

Proceso de validación:

1. Leer headers de autenticación.
2. Construir el mensaje firmado utilizando método HTTP, ruta, timestamp y cuerpo.
3. Generar una firma utilizando HMAC SHA256.
4. Comparar con la firma recibida.
5. Rechazar la solicitud si la firma no es válida.

## Configuración de rutas

El archivo `ocelot.json` debe definir rutas que:

* Expongan endpoints públicos
* Redirijan a servicios downstream
* Limiten métodos HTTP permitidos
* Permitan futura integración con autenticación adicional

## Buenas prácticas

* Utilizar inyección de dependencias.
* Evitar lógica compleja en `Program.cs`.
* Mantener middleware desacoplado.
* Permitir extensión futura del gateway.
* Preparar el proyecto para despliegue en contenedores.

## Entregables

El agente debe generar:

1. Archivo **GatewaySolution.sln**.
2. Estructura completa del proyecto.
3. Archivos principales del gateway.
4. Configuración inicial de Ocelot.
5. Middleware de validación de firma.
6. Ejemplo de rutas configuradas.
7. Configuración para ejecutar el gateway en **localhost:4000**.
8. Breve explicación de cómo compilar y ejecutar la solución.
