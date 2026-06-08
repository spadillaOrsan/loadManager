# Despliegue de LoadManagerApi en IIS

Servidor: **172.20.11.40**

| Entorno | Puerto | Carpeta física sugerida | ASPNETCORE_ENVIRONMENT |
|---|---|---|---|
| Producción | 8083 | `C:\inetpub\LoadManagerApi` | `Production` |
| Dev | 8084 | `C:\inetpub\LoadManagerApi-dev` | `Development` |

---

## 1. Requisito previo en el servidor

Instalar el **ASP.NET Core 8 Hosting Bundle** (incluye el módulo ANCM para IIS):
> https://dotnet.microsoft.com/download/dotnet/8.0 → "Hosting Bundle"

Después de instalarlo, reiniciar IIS:
```cmd
net stop was /y && net start w3svc
```

---

## 2. Publicar la API

Desde la máquina de desarrollo (o el servidor):

```bash
# Dev (puerto 8084)
dotnet publish LoadManagerApi/LoadManagerApi.csproj -c Release -o C:\publish\LoadManagerApi-dev

# Producción (puerto 8083) — cuando toque actualizar
dotnet publish LoadManagerApi/LoadManagerApi.csproj -c Release -o C:\publish\LoadManagerApi
```

Copiar el contenido de la carpeta publicada al servidor (`C:\inetpub\LoadManagerApi-dev`).

---

## 3. Crear el sitio "dev" en IIS (puerto 8084)

En el servidor, abrir **IIS Manager**:

1. **Application Pools** → *Add Application Pool*
   - Name: `LoadManagerApi-dev`
   - .NET CLR version: **No Managed Code**  ← (ASP.NET Core no usa el CLR de IIS)
   - Start mode: AlwaysRunning (opcional)

2. **Sites** → *Add Website*
   - Site name: `LoadManagerApi-dev`
   - Application pool: `LoadManagerApi-dev`
   - Physical path: `C:\inetpub\LoadManagerApi-dev`
   - Binding: Type `http`, IP `172.20.11.40` (o "All Unassigned"), **Port `8084`**, Host name vacío

3. Marcar el entorno como Development. Editar `web.config` en `C:\inetpub\LoadManagerApi-dev`, dentro de `<aspNetCore ...>`:
   ```xml
   <aspNetCore processPath="dotnet" arguments=".\LoadManagerApi.dll" ...>
     <environmentVariables>
       <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Development" />
     </environmentVariables>
   </aspNetCore>
   ```
   (Para el sitio de producción usar `value="Production"`.)

4. Permisos de escritura para los logs: dar al identity del App Pool
   (`IIS AppPool\LoadManagerApi-dev`) permiso de **Modificar** sobre la carpeta
   `C:\inetpub\LoadManagerApi-dev\Logs`.

---

## 4. Abrir el firewall del servidor

En el servidor `172.20.11.40` (PowerShell como administrador):
```powershell
New-NetFirewallRule -DisplayName "LoadManagerApi-dev 8084" -Direction Inbound -Protocol TCP -LocalPort 8084 -Action Allow
New-NetFirewallRule -DisplayName "LoadManagerApi 8083"     -Direction Inbound -Protocol TCP -LocalPort 8083 -Action Allow
```

---

## 5. Verificar

Desde el servidor:
- `http://localhost:8084/`  → debe abrir Swagger
- `http://localhost:8084/api/database/health` → `{"isSuccess":true,...}`

Desde la red / terminal:
- `http://172.20.11.40:8084/api/database/health`

---

## 6. Apuntar la terminal (app MAUI)

La app ya trae por defecto `http://172.20.11.40:8084/`.

Si necesitas cambiarla sin recompilar, hazlo desde el módulo **Configuración → URL Service**
(campos Host y Puerto API) dentro de la app.

> Recuerda: la app Android tiene `network_security_config.xml` que ya permite tráfico
> HTTP (cleartext) hacia `172.20.11.40`, así que no necesita HTTPS.

---

## Notas

- Las dos instancias (8083 y 8084) pueden apuntar a la **misma base de datos** (`dbctg`)
  o, si quieres aislar pruebas, cambia el `ConnectionStrings:GasStationDatabase`
  en el `appsettings.json` de la carpeta dev.
- La consola TCP de despacho sigue siendo `172.20.11.40:8005` (no cambia con esto).
