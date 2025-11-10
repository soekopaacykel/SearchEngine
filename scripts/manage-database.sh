#!/bin/bash

# Database Management Script
# This script handles PostgreSQL schema initialization and data import/export operations

set -euo pipefail  # Exit on any error, undefined variables, or pipe failures

# Colors for output
readonly RED='\033[0;31m'
readonly GREEN='\033[0;32m'
readonly YELLOW='\033[1;33m'
readonly BLUE='\033[0;34m'
readonly NC='\033[0m' # No Color

# Configuration
readonly SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
readonly PROJECT_ROOT="$(dirname "${SCRIPT_DIR}")"
readonly DEFAULT_SQLITE_PATH="${PROJECT_ROOT}/../DB/DB.db"
readonly DEFAULT_OUTPUT_DIR="/tmp/searchengine-data"

# Database connection defaults
readonly DEFAULT_PG_HOST="localhost"
readonly DEFAULT_PG_PORT="5432"
readonly DEFAULT_PG_USER="ameliavalentin"
readonly DEFAULT_PG_DATABASE="search"

# Function to print colored messages
print_info() { echo -e "${BLUE}$1${NC}"; }
print_success() { echo -e "${GREEN}$1${NC}"; }
print_warning() { echo -e "${YELLOW}$1${NC}"; }
print_error() { echo -e "${RED}$1${NC}"; }

# Function to display usage
show_usage() {
    echo "Usage: $0 <command> [OPTIONS]"
    echo ""
    echo "Commands:"
    echo "  init-schema          Initialize PostgreSQL database schema"
    echo "  export-sqlite        Export data from SQLite to CSV files"
    echo "  import-data          Import CSV data into PostgreSQL"
    echo "  full-migration       Complete migration: export from SQLite and import to PostgreSQL"
    echo ""
    echo "Schema Initialization Options:"
    echo "  -h, --host HOST      PostgreSQL host (default: ${DEFAULT_PG_HOST})"
    echo "  -p, --port PORT      PostgreSQL port (default: ${DEFAULT_PG_PORT})"
    echo "  -U, --user USER      PostgreSQL user (default: ${DEFAULT_PG_USER})"
    echo "  -d, --database DB    PostgreSQL database (default: ${DEFAULT_PG_DATABASE})"
    echo "  --drop-existing      Drop existing tables before creating new ones"
    echo ""
    echo "Export/Import Options:"
    echo "  -s, --sqlite PATH    Path to SQLite database (default: ${DEFAULT_SQLITE_PATH})"
    echo "  -o, --output DIR     Output directory for CSV files (default: ${DEFAULT_OUTPUT_DIR})"
    echo ""
    echo "Examples:"
    echo "  $0 init-schema                           # Initialize schema with defaults"
    echo "  $0 init-schema -h postgres-pod -U admin  # Custom connection"
    echo "  $0 export-sqlite -s /path/to/db.db       # Export from custom SQLite"
    echo "  $0 import-data -o /custom/data/dir       # Import from custom directory"
    echo "  $0 full-migration                        # Complete migration process"
}

# Function to create PostgreSQL schema
create_schema() {
    local pg_host="$1"
    local pg_port="$2"
    local pg_user="$3"
    local pg_database="$4"
    local drop_existing="$5"
    
    print_info "Creating PostgreSQL schema..."
    
    local schema_sql=""
    
    if [[ "$drop_existing" == "true" ]]; then
        schema_sql+="-- Drop existing tables
DROP TABLE IF EXISTS Occ CASCADE;
DROP TABLE IF EXISTS document CASCADE;
DROP TABLE IF EXISTS word CASCADE;

"
    fi
    
    schema_sql+="-- Create SearchEngine database schema
CREATE TABLE IF NOT EXISTS document(
    id SERIAL PRIMARY KEY, 
    url TEXT, 
    idxTime TEXT, 
    creationTime TEXT
);

CREATE TABLE IF NOT EXISTS word(
    id SERIAL PRIMARY KEY, 
    name TEXT UNIQUE
);

CREATE TABLE IF NOT EXISTS Occ(
    wordId INTEGER, 
    docId INTEGER, 
    FOREIGN KEY (wordId) REFERENCES word(id) ON DELETE CASCADE, 
    FOREIGN KEY (docId) REFERENCES document(id) ON DELETE CASCADE
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS idx_occ_word ON Occ (wordId);
CREATE INDEX IF NOT EXISTS idx_occ_doc ON Occ (docId);
CREATE INDEX IF NOT EXISTS idx_word_name ON word (name);
CREATE INDEX IF NOT EXISTS idx_document_url ON document (url);

-- Display table information
SELECT 'Schema created successfully' as status;
SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_name IN ('document', 'word', 'occ');
"
    
    # Execute the schema creation
    if command -v psql >/dev/null 2>&1; then
        if echo "$schema_sql" | PGPASSWORD="${PGPASSWORD:-}" psql -h "$pg_host" -p "$pg_port" -U "$pg_user" -d "$pg_database"; then
            print_success "✓ PostgreSQL schema created successfully"
            return 0
        else
            print_error "Failed to create PostgreSQL schema"
            return 1
        fi
    else
        print_error "psql command not found. Please install PostgreSQL client tools."
        return 1
    fi
}

# Function to check prerequisites for SQLite export
check_sqlite_prerequisites() {
    local sqlite_path="$1"
    
    # Check if sqlite3 is installed
    if ! command -v sqlite3 >/dev/null 2>&1; then
        print_error "sqlite3 is not installed. Please install it first."
        print_info "On macOS: brew install sqlite"
        print_info "On Ubuntu: sudo apt-get install sqlite3"
        return 1
    fi
    
    # Check if database exists
    if [[ ! -f "$sqlite_path" ]]; then
        print_error "SQLite database not found at: $sqlite_path"
        return 1
    fi
    
    # Check if database is accessible
    if ! sqlite3 "$sqlite_path" "SELECT 1;" >/dev/null 2>&1; then
        print_error "Cannot access SQLite database at: $sqlite_path"
        return 1
    fi
    
    return 0
}

# Function to export data from SQLite
export_sqlite_data() {
    local sqlite_path="$1"
    local output_dir="$2"
    
    print_info "Exporting data from SQLite database..."
    
    # Create output directory
    mkdir -p "$output_dir" || {
        print_error "Failed to create output directory: $output_dir"
        return 1
    }
    
    # Export documents
    if sqlite3 "$sqlite_path" <<EOF
.mode csv
.headers off
.output ${output_dir}/documents.csv
SELECT id, url, idxTime, creationTime FROM document;
.quit
EOF
    then
        print_success "✓ Documents exported"
    else
        print_error "Failed to export documents"
        return 1
    fi
    
    # Export words
    if sqlite3 "$sqlite_path" <<EOF
.mode csv
.headers off
.output ${output_dir}/words.csv
SELECT id, name FROM word;
.quit
EOF
    then
        print_success "✓ Words exported"
    else
        print_error "Failed to export words"
        return 1
    fi
    
    # Export occurrences
    if sqlite3 "$sqlite_path" <<EOF
.mode csv
.headers off
.output ${output_dir}/occ.csv
SELECT wordId, docId FROM occ;
.quit
EOF
    then
        print_success "✓ Occurrences exported"
    else
        print_error "Failed to export occurrences"
        return 1
    fi
    
    # Create PostgreSQL import script
    create_import_script "$output_dir"
    
    return 0
}

# Function to create PostgreSQL import script
create_import_script() {
    local output_dir="$1"
    local import_script="${output_dir}/import_to_postgres.sql"
    
    cat > "$import_script" << EOL
-- PostgreSQL Data Import Script
-- Generated on: $(date)

BEGIN;

-- Clear existing data (use with caution!)
TRUNCATE TABLE Occ;
TRUNCATE TABLE document RESTART IDENTITY CASCADE;
TRUNCATE TABLE word RESTART IDENTITY CASCADE;

-- Import documents
COPY document(id, url, idxTime, creationTime) 
FROM '${output_dir}/documents.csv' 
WITH (FORMAT csv, DELIMITER ',');

-- Import words  
COPY word(id, name) 
FROM '${output_dir}/words.csv' 
WITH (FORMAT csv, DELIMITER ',');

-- Import occurrences
COPY Occ(wordId, docId) 
FROM '${output_dir}/occ.csv' 
WITH (FORMAT csv, DELIMITER ',');

-- Update sequences to prevent ID conflicts
SELECT setval('document_id_seq', COALESCE((SELECT MAX(id) FROM document), 1));
SELECT setval('word_id_seq', COALESCE((SELECT MAX(id) FROM word), 1));

-- Display import statistics
SELECT 'Documents imported: ' || COUNT(*) as result FROM document;
SELECT 'Words imported: ' || COUNT(*) as result FROM word;
SELECT 'Occurrences imported: ' || COUNT(*) as result FROM Occ;

COMMIT;
EOL

    print_success "✓ Import script created: $import_script"
}

# Function to import data into PostgreSQL
import_data_to_postgres() {
    local output_dir="$1"
    local pg_host="$2"
    local pg_port="$3"
    local pg_user="$4"
    local pg_database="$5"
    
    local import_script="${output_dir}/import_to_postgres.sql"
    
    if [[ ! -f "$import_script" ]]; then
        print_error "Import script not found: $import_script"
        print_info "Please run 'export-sqlite' first to generate the import script"
        return 1
    fi
    
    print_info "Importing data into PostgreSQL..."
    
    if PGPASSWORD="${PGPASSWORD:-}" psql -h "$pg_host" -p "$pg_port" -U "$pg_user" -d "$pg_database" -f "$import_script"; then
        print_success "✓ Data imported successfully"
        return 0
    else
        print_error "Failed to import data into PostgreSQL"
        return 1
    fi
}

# Function to show summary
show_summary() {
    local operation="$1"
    local output_dir="$2"
    
    echo
    print_success "========================================"
    print_success "   $operation Complete!               "
    print_success "========================================"
    echo
    
    case "$operation" in
        "Schema Creation")
            print_warning "Next steps:"
            print_info "• Import data: $0 import-data"
            print_info "• Or run full migration: $0 full-migration"
            ;;
        "Data Export")
            print_warning "Files created in ${output_dir}:"
            if [[ -f "${output_dir}/documents.csv" ]]; then
                echo -e "  • documents.csv      ($(wc -l < "${output_dir}/documents.csv" | tr -d ' ') lines)"
            fi
            if [[ -f "${output_dir}/words.csv" ]]; then
                echo -e "  • words.csv          ($(wc -l < "${output_dir}/words.csv" | tr -d ' ') lines)"
            fi
            if [[ -f "${output_dir}/occ.csv" ]]; then
                echo -e "  • occ.csv            ($(wc -l < "${output_dir}/occ.csv" | tr -d ' ') lines)"
            fi
            echo -e "  • import_to_postgres.sql"
            echo
            print_warning "Next steps:"
            print_info "• Import to PostgreSQL: $0 import-data -o ${output_dir}"
            ;;
        "Data Import"|"Full Migration")
            print_warning "Migration completed!"
            print_info "• Verify data: Connect to your PostgreSQL database and run queries"
            print_info "• Test application: Start your SearchEngine application"
            ;;
    esac
}

# Main function
main() {
    if [[ $# -eq 0 ]]; then
        show_usage
        exit 1
    fi
    
    local command="$1"
    shift
    
    # Default values
    local sqlite_path="$DEFAULT_SQLITE_PATH"
    local output_dir="$DEFAULT_OUTPUT_DIR"
    local pg_host="$DEFAULT_PG_HOST"
    local pg_port="$DEFAULT_PG_PORT"
    local pg_user="$DEFAULT_PG_USER"
    local pg_database="$DEFAULT_PG_DATABASE"
    local drop_existing="false"
    
    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            -s|--sqlite)
                sqlite_path="$2"
                shift 2
                ;;
            -o|--output)
                output_dir="$2"
                shift 2
                ;;
            -h|--host)
                pg_host="$2"
                shift 2
                ;;
            -p|--port)
                pg_port="$2"
                shift 2
                ;;
            -U|--user)
                pg_user="$2"
                shift 2
                ;;
            -d|--database)
                pg_database="$2"
                shift 2
                ;;
            --drop-existing)
                drop_existing="true"
                shift
                ;;
            --help)
                show_usage
                exit 0
                ;;
            *)
                print_error "Unknown option: $1"
                show_usage
                exit 1
                ;;
        esac
    done
    
    # Resolve absolute paths
    sqlite_path=$(realpath "$sqlite_path" 2>/dev/null || echo "$sqlite_path")
    output_dir=$(realpath "$output_dir" 2>/dev/null || echo "$output_dir")
    
    # Execute command
    case "$command" in
        init-schema)
            print_info "========================================"
            print_info "   PostgreSQL Schema Initialization     "
            print_info "========================================"
            print_info "Host: $pg_host:$pg_port"
            print_info "Database: $pg_database"
            print_info "User: $pg_user"
            echo
            
            if create_schema "$pg_host" "$pg_port" "$pg_user" "$pg_database" "$drop_existing"; then
                show_summary "Schema Creation" "$output_dir"
            else
                exit 1
            fi
            ;;
            
        export-sqlite)
            print_info "========================================"
            print_info "   SQLite Data Export                   "
            print_info "========================================"
            print_info "SQLite DB: $sqlite_path"
            print_info "Output: $output_dir"
            echo
            
            if check_sqlite_prerequisites "$sqlite_path" && export_sqlite_data "$sqlite_path" "$output_dir"; then
                show_summary "Data Export" "$output_dir"
            else
                exit 1
            fi
            ;;
            
        import-data)
            print_info "========================================"
            print_info "   PostgreSQL Data Import               "
            print_info "========================================"
            print_info "Source: $output_dir"
            print_info "Target: $pg_host:$pg_port/$pg_database"
            echo
            
            if import_data_to_postgres "$output_dir" "$pg_host" "$pg_port" "$pg_user" "$pg_database"; then
                show_summary "Data Import" "$output_dir"
            else
                exit 1
            fi
            ;;
            
        full-migration)
            print_info "========================================"
            print_info "   Full Database Migration              "
            print_info "========================================"
            print_info "SQLite: $sqlite_path"
            print_info "PostgreSQL: $pg_host:$pg_port/$pg_database"
            print_info "Temp dir: $output_dir"
            echo
            
            if check_sqlite_prerequisites "$sqlite_path" && \
               create_schema "$pg_host" "$pg_port" "$pg_user" "$pg_database" "true" && \
               export_sqlite_data "$sqlite_path" "$output_dir" && \
               import_data_to_postgres "$output_dir" "$pg_host" "$pg_port" "$pg_user" "$pg_database"; then
                show_summary "Full Migration" "$output_dir"
            else
                exit 1
            fi
            ;;
            
        *)
            print_error "Unknown command: $command"
            show_usage
            exit 1
            ;;
    esac
}

# Execute main function
main "$@"