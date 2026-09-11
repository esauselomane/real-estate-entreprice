#!/bin/zsh
# ============================================================================
# 🔄 DROP & RESEED — Development Database Reset Script
# ============================================================================
# Drops the three application databases (Identity, CRM, Property) and re-runs
# schema creation + idempotent seed data. Intended for local dev environments
# backed by the docker-compose SQL Server container.
#
# Usage:
#   ./drop-and-reseed.sh          # drop + reseed via .NET services (default)
#   ./drop-and-reseed.sh --sql    # drop + reseed via raw scripts.sql + seed-*.sql
# ============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

# 🔑 SQL Server connection parameters (match docker-compose.yml)
SQL_HOST="${SQL_HOST:-127.0.0.1}"
SQL_PORT="${SQL_PORT:-1433}"
SQL_USER="sa"
SQL_PASS="SecureRealEstatePass123!"

DATABASES=("RealEstateIdentityDB" "RealEstateCRMDB" "RealEstatePropertyDB")

# ----------------------------------------------------------------------------
# Helper: run a SQL command against the SQL Server instance
# ----------------------------------------------------------------------------
run_sql() {
    local db="${1:-}"
    local sql="${2}"
    local db_param=""
    if [ -n "$db" ]; then
        db_param="-d $db"
    fi
    /usr/local/bin/sqlcmd -S "$SQL_HOST,$SQL_PORT" -U "$SQL_USER" -P "$SQL_PASS" \
        $db_param -Q "$sql" -b
}

# ----------------------------------------------------------------------------
# Helper: check if sqlcmd is available
# ----------------------------------------------------------------------------
check_sqlcmd() {
    if ! command -v sqlcmd &>/dev/null; then
        echo "⚠️  sqlcmd not found. Install via:"
        echo "   brew tap microsoft/mssql-release https://github.com/Microsoft/homebrew-mssql-release"
        echo "   brew install sqlcli"
        echo "   (or use --sql flag with docker exec)"
        return 1
    fi
    return 0
}

# ----------------------------------------------------------------------------
# 1. DROP DATABASES
# ----------------------------------------------------------------------------
echo "📨 Dropping existing databases..."

for db in "${DATABASES[@]}"; do
    echo "  → Dropping [$db]..."
    if check_sqlcmd; then
        run_sql "" "IF EXISTS (SELECT name FROM sys.databases WHERE name = N'$db') ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE IF EXISTS [$db];"
    else
        # Fallback: use docker exec
        docker exec realestate-sqlserver-db /opt/mssql-tools/bin/sqlcmd \
            -S localhost -U "$SQL_USER" -P "$SQL_PASS" \
            -Q "IF EXISTS (SELECT name FROM sys.databases WHERE name = N'$db') ALTER DATABASE [$db] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE IF EXISTS [$db];" -b
    fi
done

echo "   ✅ Databases dropped."

# ----------------------------------------------------------------------------
# 2. RESEED
# ----------------------------------------------------------------------------
if [ "${1:-}" = "--sql" ]; then
    # ── SQL-mode: run scripts.sql then seed scripts ──
    echo "📨 Creating schemas and seeding via raw SQL scripts..."

    if check_sqlcmd; then
        echo "  → Running scripts.sql..."
        sqlcmd -S "$SQL_HOST,$SQL_PORT" -U "$SQL_USER" -P "$SQL_PASS" -i "$SCRIPT_DIR/scripts.sql" -b
        echo "  → Running seed-identity.sql..."
        sqlcmd -S "$SQL_HOST,$SQL_PORT" -U "$SQL_USER" -P "$SQL_PASS" -i "$SCRIPT_DIR/scripts/seed-identity.sql" -b
        echo "  → Running seed-crm.sql..."
        sqlcmd -S "$SQL_HOST,$SQL_PORT" -U "$SQL_USER" -P "$SQL_PASS" -i "$SCRIPT_DIR/scripts/seed-crm.sql" -b
    else
        echo "  → Running scripts.sql via docker..."
        docker cp "$SCRIPT_DIR/scripts.sql" realestate-sqlserver-db:/tmp/scripts.sql
        docker cp "$SCRIPT_DIR/scripts/seed-identity.sql" realestate-sqlserver-db:/tmp/seed-identity.sql
        docker cp "$SCRIPT_DIR/scripts/seed-crm.sql" realestate-sqlserver-db:/tmp/seed-crm.sql
        docker exec realestate-sqlserver-db /opt/mssql-tools/bin/sqlcmd -S localhost -U "$SQL_USER" -P "$SQL_PASS" -i /tmp/scripts.sql -b
        docker exec realestate-sqlserver-db /opt/mssql-tools/bin/sqlcmd -S localhost -U "$SQL_USER" -P "$SQL_PASS" -i /tmp/seed-identity.sql -b
        docker exec realestate-sqlserver-db /opt/mssql-tools/bin/sqlcmd -S localhost -U "$SQL_USER" -P "$SQL_PASS" -i /tmp/seed-crm.sql -b
    fi

    echo "   ✅ Schema and seed data loaded."
else
    # ── .NET-mode: start microservices so EnsureCreated() + ExecuteSqlRaw + seed runs ──
    echo "📨 Starting microservices to recreate databases and seed data..."
    echo "   (Services call Database.EnsureCreated() → ExecuteSqlRaw backfills → Database.Migrate() → seed data)"
    echo "   Starting backend-services..."
    "$SCRIPT_DIR/launch-ecosystem.sh" &
    LOCAL_PID=$!
    echo "   ⏳ Waiting 15s for services to boot and seed..."
    sleep 15
    echo "   ⚠️  Services are running in background (PID $LOCAL_PID)."
    echo "   Stop with: kill $LOCAL_PID"
    echo "   Or use docker: docker compose stop"
fi

echo ""
echo "✅ Drop and reseed complete!"
echo "   Databases: ${DATABASES[*]}"
