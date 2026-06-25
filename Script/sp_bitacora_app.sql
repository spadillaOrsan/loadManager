CREATE OR ALTER PROCEDURE dbo.sp_bitacora_app
    @intTPV            INT,
    @intTipoVenta      INT            = 1,
    @intDispensario    INT,
    @intManguera       INT,
    @intProducto       INT,
    @intUsuario        INT            = 1,
    @strTarjeta        VARCHAR(255)   = '',
    @intTipoProgramado INT,
    @dblProgramado     DECIMAL(18,3),
    @strCliente        VARCHAR(255)   = '',
    @strBandaMagnetica VARCHAR(255)   = '',
    @strVehiculo       VARCHAR(50)    = '',
    @strOdometro       VARCHAR(50)    = '',
    @strPie1           VARCHAR(255)   = '',
    @strPie2           VARCHAR(255)   = '',
    @strPie3           VARCHAR(255)   = '',
    @strPie4           VARCHAR(255)   = ''
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE
        @intFolioSecuencia   BIGINT,
        @strTipoTransaccion  VARCHAR(1),
        @intFolioCorte       BIGINT,
        @UsuarioDispensario  INT,
        @DispensarioManual   VARCHAR(100),
        @DispensarioManualId INT,
        @LimiteLitros        DECIMAL(18,3),
        @LimiteImporte       DECIMAL(18,2),
        @PrecioProducto      DECIMAL(18,2);

    SET @intTipoVenta      = ISNULL(@intTipoVenta, 1);
    SET @intUsuario        = ISNULL(@intUsuario, 1);
    SET @strTarjeta        = ISNULL(@strTarjeta, '');
    SET @strCliente        = ISNULL(@strCliente, '');
    SET @strBandaMagnetica = ISNULL(@strBandaMagnetica, '');
    SET @strVehiculo       = ISNULL(@strVehiculo, '');
    SET @strOdometro       = ISNULL(@strOdometro, '');
    SET @strPie1           = ISNULL(@strPie1, '');
    SET @strPie2           = ISNULL(@strPie2, '');
    SET @strPie3           = ISNULL(@strPie3, '');
    SET @strPie4           = ISNULL(@strPie4, '');
    SET @strTipoTransaccion = 'D';

    IF @intTPV IS NULL OR @intTPV <= 0
    BEGIN
        RAISERROR('El TPV es obligatorio.', 16, 1);
        RETURN;
    END;

    IF @intDispensario IS NULL OR @intDispensario <= 0
    BEGIN
        RAISERROR('El dispensario es obligatorio.', 16, 1);
        RETURN;
    END;

    IF @intManguera IS NULL OR @intManguera <= 0
    BEGIN
        RAISERROR('La manguera es obligatoria.', 16, 1);
        RETURN;
    END;

    IF @intProducto IS NULL OR @intProducto < 0
    BEGIN
        RAISERROR('El producto no es valido.', 16, 1);
        RETURN;
    END;

    IF @intTipoProgramado NOT IN (1, 2, 3)
    BEGIN
        RAISERROR('El tipo programado no es valido.', 16, 1);
        RETURN;
    END;

    IF @dblProgramado IS NULL OR @dblProgramado <= 0
    BEGIN
        RAISERROR('La cantidad programada debe ser mayor que cero.', 16, 1);
        RETURN;
    END;

    BEGIN TRY
        -- Sin BEGIN TRANSACTION propio: el caller (C#) ya gestiona la transaccion.
        -- XACT_ABORT ON garantiza que cualquier error aborte la transaccion exterior.
        EXEC dbo.sp_folio_app
            @intFolioSecuencia = @intFolioSecuencia OUTPUT;

        SELECT TOP (1)
            @intFolioCorte = intFolioCorte
        FROM dbo.tblParametros;

        IF (@intTipoVenta = 0 AND @strVehiculo <> '00000')
            SET @intTipoVenta = 1;

        IF (@intTipoVenta = 0 AND @strVehiculo = '00000')
            SET @intTipoVenta = 2;

        IF @intTipoVenta = 1 AND UPPER(LTRIM(RTRIM(@strPie4))) LIKE 'MANUAL%'
        BEGIN
            SET @DispensarioManual = LTRIM(RTRIM(REPLACE(UPPER(@strPie4), 'MANUAL', '')));

            -- Conversion segura compatible con SQL Server 2008 (sin TRY_CONVERT)
            SET @DispensarioManualId = NULL;
            IF @DispensarioManual NOT LIKE '%[^0-9]%' AND LEN(@DispensarioManual) > 0
                SET @DispensarioManualId = CAST(@DispensarioManual AS INT);

            SELECT TOP (1)
                @UsuarioDispensario = ui.intUsuario
            FROM dbo.tblUsuarioIsla ui
            WHERE ui.intFolioCorte = @intFolioCorte
              AND ui.intIsla IN
              (
                  SELECT d.intIsla
                  FROM dbo.tblDispensarios d
                  WHERE d.intDispensario = @DispensarioManualId
              );

            IF @UsuarioDispensario IS NOT NULL
                SET @intUsuario = @UsuarioDispensario;
        END;

        IF EXISTS
        (
            SELECT 1
            FROM dbo.tblTiposVenta
            WHERE intTipoVenta = @intTipoVenta
              AND bitFidelidad = 1
        )
        AND LTRIM(@strBandaMagnetica) <> ''
        AND LTRIM(@strBandaMagnetica) <> '0'
        AND LEN(LTRIM(@strBandaMagnetica)) > 20
        BEGIN
            SET @strPie1 = 'GoBenefits';
            SET @strPie2 = 'Consulte sus puntos en la WEB';
            SET @strPie3 = '1';
        END;

        IF @intTipoVenta = 9
            SET @strTipoTransaccion = 'N';

        SELECT
            @LimiteLitros  = ISNULL(dblLimiteLitros, 0),
            @LimiteImporte = ISNULL(dblLimiteImporte, 0)
        FROM dbo.tblMangueras
        WHERE intDispensario = @intDispensario
          AND intManguera    = @intManguera
          AND intProducto    = @intProducto;

        SELECT
            @PrecioProducto = ISNULL(dblPrecioU, 0)
        FROM dbo.tblProductos
        WHERE intProducto = @intProducto;

        SET @LimiteLitros   = ISNULL(@LimiteLitros, 0);
        SET @LimiteImporte  = ISNULL(@LimiteImporte, 0);
        SET @PrecioProducto = ISNULL(@PrecioProducto, 0);

        IF @intProducto <> 0
        BEGIN
            IF @PrecioProducto <= 0
                RAISERROR('El producto no tiene un precio valido.', 16, 1);

            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.tblMangueras
                WHERE intDispensario = @intDispensario
                  AND intManguera    = @intManguera
                  AND intProducto    = @intProducto
            )
                RAISERROR('La manguera y el producto no pertenecen al dispensario.', 16, 1);
        END;

        IF @intTipoProgramado = 1
        BEGIN
            IF @intProducto = 0
            BEGIN
                IF @LimiteImporte > 0
                    SET @dblProgramado = @LimiteImporte;
            END
            ELSE IF @LimiteImporte = 0
            BEGIN
                IF @LimiteLitros > 0
                   AND (@dblProgramado / @PrecioProducto) > @LimiteLitros
                    RAISERROR('El programado supera el limite de litros.', 16, 1);
            END
            ELSE IF @dblProgramado > @LimiteImporte
                RAISERROR('El programado supera el limite de importe.', 16, 1);
        END;

        IF @intTipoProgramado = 2
        BEGIN
            IF @intProducto = 0
            BEGIN
                IF @LimiteLitros > 0
                    SET @dblProgramado = @LimiteLitros;
            END
            ELSE IF @LimiteLitros = 0
            BEGIN
                IF @LimiteImporte > 0
                   AND (@dblProgramado * @PrecioProducto) > @LimiteImporte
                    RAISERROR('El programado supera el limite de importe.', 16, 1);
            END
            ELSE IF @dblProgramado > @LimiteLitros
                RAISERROR('El programado supera el limite de litros.', 16, 1);
        END;

        IF @intTipoProgramado = 3
        BEGIN
            IF @LimiteImporte > 0 AND @dblProgramado > @LimiteImporte
                RAISERROR('El programado supera el limite de importe.', 16, 1);

            IF @LimiteImporte <= 0
               AND @LimiteLitros > 0
               AND @dblProgramado > FLOOR(@LimiteLitros * @PrecioProducto)
                RAISERROR('El programado supera el limite calculado.', 16, 1);
        END;

        IF @intTipoProgramado = 3 AND @intTipoVenta = 4
            RAISERROR('Tipo de programado no permitido para este tipo de venta.', 16, 1);

        INSERT INTO dbo.tblBitacora
        (
            strTipoTransaccion,
            intSecuencia,
            datFechaHora,
            intTPV,
            intTipoVenta,
            intDispensario,
            intManguera,
            intProducto,
            intUsuario,
            strTarjeta,
            intTipoProgramado,
            dblProgramado,
            strCliente,
            strBandaMagnetica,
            strVehiculo,
            strOdometro,
            strPie1,
            strPie2,
            strPie3,
            strPie4,
            strTotalizador,
            intFolioCorte
        )
        VALUES
        (
            @strTipoTransaccion,
            @intFolioSecuencia,
            GETDATE(),
            @intTPV,
            @intTipoVenta,
            @intDispensario,
            @intManguera,
            @intProducto,
            @intUsuario,
            @strTarjeta,
            @intTipoProgramado,
            @dblProgramado,
            @strCliente,
            @strBandaMagnetica,
            @strVehiculo,
            @strOdometro,
            @strPie1,
            @strPie2,
            @strPie3,
            @strPie4,
            '0.0',
            @intFolioCorte
        );

        IF @intTipoVenta IN (3, 33)
        BEGIN
            INSERT INTO dbo.tblConsultasUG
            (
                intSecuencia,
                strIdConsulta,
                datFechaAlta,
                bitProcesada,
                intTipo
            )
            VALUES
            (
                @intFolioSecuencia,
                @strPie3,
                GETDATE(),
                0,
                @intTipoVenta
            );
        END;

        SELECT @intFolioSecuencia AS intFolioSecuencia;
    END TRY
    BEGIN CATCH
        -- No hacemos ROLLBACK aqui: el caller es dueno de la transaccion.
        -- Solo re-lanzamos el error para que C# lo capture y haga rollback.
        DECLARE @ErrMsg  NVARCHAR(2048) = ERROR_MESSAGE();
        DECLARE @ErrSev  INT            = ERROR_SEVERITY();
        DECLARE @ErrSta  INT            = ERROR_STATE();
        RAISERROR(@ErrMsg, @ErrSev, @ErrSta);
    END CATCH;
END;
