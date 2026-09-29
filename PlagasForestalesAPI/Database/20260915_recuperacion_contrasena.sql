/* Ejecutar una vez en la base de datos de producción antes de publicar la API. */
IF OBJECT_ID(N'dbo.CodigosRecuperacionContrasena', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CodigosRecuperacionContrasena (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UsuarioId INT NOT NULL,
        CodigoHash NVARCHAR(64) NOT NULL,
        Canal NVARCHAR(10) NOT NULL,
        FechaCreacionUtc DATETIME2 NOT NULL,
        ExpiraUtc DATETIME2 NOT NULL,
        UsadoUtc DATETIME2 NULL,
        IntentosFallidos INT NOT NULL DEFAULT 0,
        CONSTRAINT FK_CodigosRecuperacionContrasena_Usuarios
            FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuarios(Id)
    );
    CREATE INDEX IX_CodigosRecuperacionContrasena_UsuarioId_ExpiraUtc
        ON dbo.CodigosRecuperacionContrasena(UsuarioId, ExpiraUtc);
END
GO
