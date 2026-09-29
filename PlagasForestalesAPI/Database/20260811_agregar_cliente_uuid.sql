/* Ejecutar una vez en SQL Server antes de publicar la API. */
IF COL_LENGTH('dbo.Reportes', 'ClienteUuid') IS NULL
BEGIN
    ALTER TABLE dbo.Reportes ADD ClienteUuid nvarchar(36) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_Reportes_UsuarioId_ClienteUuid'
      AND object_id = OBJECT_ID('dbo.Reportes')
)
BEGIN
    CREATE UNIQUE INDEX UX_Reportes_UsuarioId_ClienteUuid
        ON dbo.Reportes(UsuarioId, ClienteUuid)
        WHERE ClienteUuid IS NOT NULL;
END
GO
