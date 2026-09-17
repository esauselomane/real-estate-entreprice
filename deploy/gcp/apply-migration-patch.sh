#!/usr/bin/env bash
# apply-migration-patch.sh: Manually mark the EpilepsySafeMode migration as completed in IdentityDB.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=common.sh
source "${SCRIPT_DIR}/common.sh"

# Attempt to add standard mssql-tools path if not already present
if ! command -v sqlcmd >/dev/null 2>&1; then
  export PATH="$PATH:/opt/mssql-tools18/bin:/opt/mssql-tools/bin"
fi

require_command gcloud
require_command sqlcmd

PROJECT_ID="${PROJECT_ID:-}"
REGION="${REGION:-africa-south1}"
SQL_INSTANCE="${SQL_INSTANCE:-realestate-sql-${REGION}}"

if [ -z "${PROJECT_ID}" ]; then
  error "Set PROJECT_ID environment variable (e.g., export PROJECT_ID=\"your-project-id\")"
fi

log "Connecting to IdentityDB on ${SQL_INSTANCE} to apply migration metadata..."
log "When prompted for a password, use the one stored in Secret Manager (mssql-password)."

gcloud sql connect "${SQL_INSTANCE}" --project="${PROJECT_ID}" --user=sqlserver --database=IdentityDB <<EOF
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] ([MigrationId] nvarchar(150) NOT NULL, [ProductVersion] nvarchar(32) NOT NULL, CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId]));
END;
IF NOT EXISTS(SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20260910025358_AddEpilepsySafeModeToUserAccount')
BEGIN
    INSERT INTO [__EFMigrationsHistory] (MigrationId, ProductVersion) VALUES ('20260910025358_AddEpilepsySafeModeToUserAccount', '8.0.0');
END;
GO
EOF