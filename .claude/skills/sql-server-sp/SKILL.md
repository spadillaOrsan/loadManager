# SQL Server Stored Procedure Skill

## Objetivo

Generar, revisar y refactorizar procedimientos almacenados SQL Server siguiendo la estructura estándar del proyecto.

## Reglas obligatorias

- Usar CREATE OR ALTER PROCEDURE.
- Incluir encabezado de documentación.
- Usar SET NOCOUNT ON.
- Usar SET XACT_ABORT ON.
- Usar TRY/CATCH cuando exista lógica de negocio.
- Usar THROW para manejo de errores.
- No usar SELECT *.
- No asumir que todos los procedimientos reciben un ID.
- Validar únicamente parámetros obligatorios.
- Mantener una única responsabilidad por procedimiento.

## Convenciones de nombres

### Procedimientos

Usar prefijo sp_.

### Parámetros

Usar nombres descriptivos orientados al negocio.

Correcto:

- @secuencia
- @folio
- @dispensario
- @manguera
- @producto
- @fechaLectura
- @observaciones
- @importe
- @volumen
- @resultado

Evitar prefijos por tipo de dato:

- @intSecuencia
- @strObservaciones
- @datFecha
- @dblImporte
- @bitActivo

### Variables locales

Usar prefijo @v_.

Ejemplo:

DECLARE
    @v_secuencia INT = @secuencia,
    @v_estatus INT;

## Encabezado estándar

/***************************************************************************************************
PROCEDIMIENTO : dbo.sp_NombreProcedimiento
DESCRIPCIÓN   : Descripción breve de lo que realiza.
AUTOR         : Santiago Padilla
FECHA CREACIÓN: DD/MM/YYYY
****************************************************************************************************
| GENERACIÓN | FECHA      | RESPONSABLE       | DESCRIPCIÓN
|------------|------------|-------------------|-----------------------------------------------|
| 1.0.0      | DD/MM/YYYY | Santiago Padilla  | Creación inicial                              |
***************************************************************************************************/

## Mejora continua

Estas reglas definen formato y consistencia.

Si existe una mejora clara de rendimiento, seguridad, mantenibilidad o arquitectura, proponerla brevemente antes de generar el código final.
