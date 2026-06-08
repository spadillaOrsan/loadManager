CREATE OR ALTER PROCEDURE dbo.sp_bitacora_app
    @Json NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE
        @intTPV INT,
        @intTipoVenta INT,
        @intDispensario INT,
        @intManguera INT,
        @intProducto INT,
        @intUsuario INT,
        @strTarjeta VARCHAR(255),
        @intTipoProgramado INT,
        @dblProgramado DECIMAL(18,3),
        @intFolioSecuencia BIGINT,
        @strCliente VARCHAR(255),
        @strBandaMagnetica VARCHAR(255),
        @strVehiculo VARCHAR(50),
        @strOdometro VARCHAR(50),
        @strPie1 VARCHAR(255),
        @strPie2 VARCHAR(255),
        @strPie3 VARCHAR(255),
        @strPie4 VARCHAR(255),
        @strTotalizador VARCHAR(500),
        @strTipoTransaccion VARCHAR(1),
        @intFolioCorte BIGINT,
        @UsuarioDispensario INT,
        @DispensarioManual VARCHAR(100),
        @LimiteLitros DECIMAL(18,3),
        @LimiteImporte DECIMAL(18,2),
        @PrecioProducto DECIMAL(18,2);

    IF ISJSON(@Json) <> 1
        THROW 50020, 'El JSON de autorizacion no es valido.', 1;

    SELECT
        @intTPV = intTPV,
        @intTipoVenta = ISNULL(intTipoVenta, 1),
        @intDispensario = intDispensario,
        @intManguera = intManguera,
        @intProducto = intProducto,
        @intUsuario = ISNULL(intUsuario, 1),
        @strTarjeta = ISNULL(strTarjeta, ''),
        @intTipoProgramado = intTipoProgramado,
        @dblProgramado = dblProgramado,
        @strCliente = ISNULL(strCliente, ''),
        @strBandaMagnetica = ISNULL(strBandaMagnetica, ''),
        @strVehiculo = ISNULL(strVehiculo, ''),
        @strOdometro = ISNULL(strOdometro, ''),
        @strPie1 = ISNULL(strPie1, ''),
        @strPie2 = ISNULL(strPie2, ''),
        @strPie3 = ISNULL(strPie3, ''),
        @strPie4 = ISNULL(strPie4, ''),
        @strTotalizador = ISNULL(strTotalizador, '0.0'),
        @strTipoTransaccion = ISNULL(strTipoTransaccion, 'D')
    FROM OPENJSON(@Json)
    WITH
    (
        intTPV INT,
        intTipoVenta INT,
        intDispensario INT,
        intManguera INT,
        intProducto INT,
        intUsuario INT,
        strTarjeta VARCHAR(255),
        intTipoProgramado INT,
        dblProgramado DECIMAL(18,3),
        strCliente VARCHAR(255),
        strBandaMagnetica VARCHAR(255),
        strVehiculo VARCHAR(50),
        strOdometro VARCHAR(50),
        strPie1 VARCHAR(255),
        strPie2 VARCHAR(255),
        strPie3 VARCHAR(255),
        strPie4 VARCHAR(255),
        strTotalizador VARCHAR(500),
        strTipoTransaccion VARCHAR(1)
    );

    IF @intTPV IS NULL OR @intTPV <= 0
        THROW 50021, 'El TPV es obligatorio.', 1;

    IF @intDispensario IS NULL OR @intDispensario <= 0
        THROW 50022, 'El dispensario es obligatorio.', 1;

    IF @intManguera IS NULL OR @intManguera <= 0
        THROW 50023, 'La manguera es obligatoria.', 1;

    IF @intProducto IS NULL OR @intProducto < 0
        THROW 50024, 'El producto no es valido.', 1;

    IF @intTipoProgramado NOT IN (1, 2, 3)
        THROW 50025, 'El tipo programado no es valido.', 1;

    IF @dblProgramado IS NULL OR @dblProgramado <= 0
        THROW 50026, 'La cantidad programada debe ser mayor que cero.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

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

            SELECT TOP (1)
                @UsuarioDispensario = ui.intUsuario
            FROM dbo.tblUsuarioIsla ui
            WHERE ui.intFolioCorte = @intFolioCorte
              AND ui.intIsla IN
              (
                  SELECT d.intIsla
                  FROM dbo.tblDispensarios d
                  WHERE d.intDispensario = TRY_CONVERT(INT, @DispensarioManual)
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
            @LimiteLitros = ISNULL(dblLimiteLitros, 0),
            @LimiteImporte = ISNULL(dblLimiteImporte, 0)
        FROM dbo.tblMangueras
        WHERE intDispensario = @intDispensario
          AND intManguera = @intManguera
          AND intProducto = @intProducto;

        SELECT
            @PrecioProducto = ISNULL(dblPrecioU, 0)
        FROM dbo.tblProductos
        WHERE intProducto = @intProducto;

        SET @LimiteLitros = ISNULL(@LimiteLitros, 0);
        SET @LimiteImporte = ISNULL(@LimiteImporte, 0);
        SET @PrecioProducto = ISNULL(@PrecioProducto, 0);

        IF @intProducto <> 0
        BEGIN
            IF @PrecioProducto <= 0
                THROW 50027, 'El producto no tiene un precio valido.', 1;

            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.tblMangueras
                WHERE intDispensario = @intDispensario
                  AND intManguera = @intManguera
                  AND intProducto = @intProducto
            )
                THROW 50028, 'La manguera y el producto no pertenecen al dispensario.', 1;
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
                    THROW 50001, 'El programado supera el limite de litros.', 1;
            END
            ELSE IF @dblProgramado > @LimiteImporte
                THROW 50002, 'El programado supera el limite de importe.', 1;
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
                    THROW 50003, 'El programado supera el limite de importe.', 1;
            END
            ELSE IF @dblProgramado > @LimiteLitros
                THROW 50004, 'El programado supera el limite de litros.', 1;
        END;

        IF @intTipoProgramado = 3
        BEGIN
            IF @LimiteImporte > 0 AND @dblProgramado > @LimiteImporte
                THROW 50005, 'El programado supera el limite de importe.', 1;

            IF @LimiteImporte <= 0
               AND @LimiteLitros > 0
               AND @dblProgramado > FLOOR(@LimiteLitros * @PrecioProducto)
                THROW 50006, 'El programado supera el limite calculado.', 1;
        END;

        IF @intTipoProgramado = 3 AND @intTipoVenta = 4
            THROW 50007, 'Tipo de programado no permitido para este tipo de venta.', 1;

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
            @strTotalizador,
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

        COMMIT TRANSACTION;

        SELECT @intFolioSecuencia AS intFolioSecuencia;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH;
END;
