CREATE OR ALTER PROCEDURE dbo.sp_HistorialFolioCorte
    @intFolioCorte INT,
    @intTop        INT = 3
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@intTop)
        b.intFolioCorte,
        b.intSecuencia,
        b.datFechaHora,
        b.intDispensario,
        b.intManguera,
        b.intProducto,
        b.dblProgramado,
        b.dblVendido,
        b.dblVolumenVendido,
        b.bitCerrada,
        b.strObservaciones,
        tp.strDescripcion  AS ProductoDescripcion,
        tp.dblPrecioU      AS ProductoPrecio,
        tparam.strMarcaGasolinera AS MarcaGasolinera,
        tparam.strRFC             AS Rfc,
        tparam.strNomEmpresa      AS NombreEmpresa,
        tparam.intEstUG           AS EstacionUG,
        CASE WHEN EXISTS (
            SELECT 1 FROM dbo.tblTransacciones t WHERE t.intSecuencia = b.intSecuencia
        ) THEN 1 ELSE 0 END AS EstaCerrada
    FROM dbo.tblBitacora b
    LEFT JOIN  dbo.tblProductos  tp     ON tp.intProducto = b.intProducto
    CROSS JOIN dbo.tblParametros tparam
    WHERE b.intFolioCorte = @intFolioCorte
    ORDER BY b.datFechaHora DESC;
END
