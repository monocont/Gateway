# 🌐 API Gateway — MonoCont

Punto de entrada único para todos los microservicios del ecosistema **MonoCont**.  
Construido con **ASP.NET Core 8** y **Ocelot**, este gateway centraliza el enrutamiento, la autenticación JWT (RS256) y la validación de firma HMAC-SHA256, evitando que los clientes accedan directamente a los servicios internos.

---

## 🗂️ Estructura del proyecto

```
GatewaySolution.sln
└── src/
    └── Gateway/
        ├── Program.cs                          # Bootstrap, DI y pipeline HTTP
        ├── ocelot.json                         # Rutas hacia los microservicios
        ├── appsettings.json                    # Configuración JWT y firma
        ├── Configuration/
        │   └── GatewaySettings.cs              # Configuración tipada
        ├── Middleware/
        │   └── SignatureValidationMiddleware.cs # Validación HMAC-SHA256
        └── Services/
            └── SignatureValidationService.cs   # Lógica de firma
```

---

## ⚙️ ¿Cómo funciona?

```
Cliente
  │
  ▼
API Gateway  ──►  Validación firma HMAC-SHA256  (X-Api-Key / X-Timestamp / X-Signature)
  │            ──►  Validación JWT Bearer        (RS256 vía JWKS)
  │
  ├──►  /usuario_service/**  ──►  UsuarioService    (https://localhost:5552)
  └──►  /api/auth/**         ──►  AutenticacionService (https://localhost:5550)
```

El gateway levanta en: **`http://localhost:4444`**

---

## 🔐 Seguridad

### 1. Validación de firma HMAC-SHA256

Cada petición (excepto `/api/auth/**`) debe incluir los siguientes headers:

| Header        | Descripción                                      |
|---------------|--------------------------------------------------|
| `X-Api-Key`   | Identificador del cliente/microservicio          |
| `X-Timestamp` | Timestamp Unix en segundos (tolerancia ±5 min)  |
| `X-Signature` | HMAC-SHA256 de `METHOD\nPATH\nTIMESTAMP\nBODY`  |

### 2. Autenticación JWT Bearer (RS256)

Las rutas protegidas exigen un token JWT en el header `Authorization: Bearer <token>`.  
El gateway obtiene la clave pública automáticamente desde el endpoint JWKS del servicio de autenticación (configurado en `appsettings.json`).

---

## ▶️ Cómo ejecutar

### Prerrequisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Los microservicios referenciados deben estar corriendo en sus puertos respectivos.

### Pasos

```bash
# 1. Clonar el repositorio
git clone <repo-url>
cd Gateway

# 2. Restaurar dependencias
dotnet restore

# 3. Ejecutar el gateway
dotnet run --project src/Gateway/Gateway.csproj
```

El gateway estará disponible en: `http://localhost:4444`

### Health check

```
GET http://localhost:4444/health
```

---

## ➕ Cómo agregar un nuevo microservicio

Para registrar un nuevo microservicio en el gateway sólo debes editar el archivo **`src/Gateway/ocelot.json`** y agregar una nueva entrada al array `Routes`.

### Plantilla de ruta

```json
{
  "UpstreamPathTemplate": "/<prefijo-del-servicio>/{everything}",
  "UpstreamHttpMethod": ["GET", "POST", "PUT", "DELETE"],
  "DownstreamPathTemplate": "/api/<ControladorInterno>/{everything}",
  "DownstreamScheme": "https",
  "DownstreamHostAndPorts": [
    {
      "Host": "localhost",
      "Port": <PUERTO_DEL_SERVICIO>
    }
  ],
  "RouteIsCaseSensitive": false,
  "AuthenticationOptions": {
    "AuthenticationProviderKey": "Bearer",
    "AllowedScopes": []
  }
}
```

> **Nota:** Omite `AuthenticationOptions` si la ruta debe ser pública (sin JWT).

### Ejemplo de `ocelot.json` con múltiples servicios

```json
{
  "Routes": [
    {
      "UpstreamPathTemplate": "/api/auth/{everything}",
      "UpstreamHttpMethod": ["GET", "POST"],
      "DownstreamPathTemplate": "/api/auth/{everything}",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 5550 }],
      "RouteIsCaseSensitive": false
    },
    {
      "UpstreamPathTemplate": "/usuario_service/{everything}",
      "UpstreamHttpMethod": ["GET", "POST", "PUT", "DELETE"],
      "DownstreamPathTemplate": "/api/Usuarios/{everything}",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 5552 }],
      "RouteIsCaseSensitive": false,
      "AuthenticationOptions": {
        "AuthenticationProviderKey": "Bearer",
        "AllowedScopes": []
      }
    },
    {
      "UpstreamPathTemplate": "/nuevo_servicio/{everything}",
      "UpstreamHttpMethod": ["GET", "POST", "PUT", "DELETE"],
      "DownstreamPathTemplate": "/api/NuevoServicio/{everything}",
      "DownstreamScheme": "https",
      "DownstreamHostAndPorts": [{ "Host": "localhost", "Port": 5553 }],
      "RouteIsCaseSensitive": false,
      "AuthenticationOptions": {
        "AuthenticationProviderKey": "Bearer",
        "AllowedScopes": []
      }
    }
  ],
  "GlobalConfiguration": {
    "BaseUrl": "http://localhost:4444"
  }
}
```

> Os cambios en `ocelot.json` se aplican **en caliente** (hot-reload) sin necesidad de reiniciar el gateway.

---

## 🛠️ Configuración (`appsettings.json`)

```json
{
  "JwtSettings": {
    "Issuer":   "AutenticacionService",
    "Audience": "GatewayClients",
    "JwksUri":  "https://localhost:5550/.well-known/jwks.json"
  },
  "GatewaySettings": {
    "ApiKey":    "<clave-secreta-compartida>",
    "SecretKey": "<clave-hmac-sha256>"
  }
}
```

---

## 📦 Microservicios registrados

| Prefijo upstream          | Servicio              | Puerto |
|---------------------------|-----------------------|--------|
| `/api/auth/**`            | AutenticacionService  | 5550   |
| `/usuario_service/**`     | UsuarioService        | 5552   |

---

## 📄 Licencia

MIT
