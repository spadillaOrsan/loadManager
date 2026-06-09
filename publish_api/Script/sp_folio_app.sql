CREATE OR ALTER PROCEDURE dbo.sp_folio_app
    @intFolioSecuencia BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @TransaccionPropia BIT = 0;

    BEGIN TRY
        IF @@TRANCOUNT = 0
        BEGIN
            BEGIN TRANSACTION;
            SET @TransaccionPropia = 1;
        END;

        IF (SELECT COUNT_BIG(*) FROM dbo.tblParametros WITH (UPDLOCK, HOLDLOCK)) <> 1
            THROW 50010, 'tblParametros debe contener exactamente un registro para generar el folio.', 1;

        UPDATE dbo.tblParametros WITH (UPDLOCK, HOLDLOCK)
        SET @intFolioSecuencia = intFolioSecuencia = ISNULL(intFolioSecuencia, 0) + 1;

        IF @intFolioSecuencia IS NULL
            THROW 50011, 'No se pudo generar el folio de secuencia.', 1;

        IF @TransaccionPropia = 1
            COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @TransaccionPropia = 1 AND @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
