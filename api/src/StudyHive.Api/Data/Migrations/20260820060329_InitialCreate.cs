using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StudyHive.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "consumables",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    stock_quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    reserved_quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    available_quantity = table.Column<int>(type: "integer", nullable: false, computedColumnSql: "stock_quantity - reserved_quantity", stored: true),
                    min_stock_level = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consumables", x => x.id);
                    table.CheckConstraint("chk_never_oversold", "reserved_quantity <= stock_quantity");
                    table.CheckConstraint("ck_consumables_min_stock_level", "min_stock_level >= 0");
                    table.CheckConstraint("ck_consumables_reserved_quantity", "reserved_quantity >= 0");
                    table.CheckConstraint("ck_consumables_stock_quantity", "stock_quantity >= 0");
                    table.CheckConstraint("ck_consumables_unit_price", "unit_price >= 0");
                });

            migrationBuilder.CreateTable(
                name: "equipment_types",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipment_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "study_rooms",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    building = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    floor = table.Column<int>(type: "integer", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    hourly_rate = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    qr_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_study_rooms", x => x.id);
                    table.CheckConstraint("ck_study_rooms_capacity", "capacity > 0");
                    table.CheckConstraint("ck_study_rooms_hourly_rate", "hourly_rate >= 0");
                });

            migrationBuilder.CreateTable(
                name: "suppliers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    contact_email = table.Column<string>(type: "citext", nullable: false),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_suppliers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "citext", nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    full_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_role", "role IN ('Student','Librarian','StoreOfficer','Admin')");
                });

            migrationBuilder.CreateTable(
                name: "maintenance_windows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_maintenance_windows", x => x.id);
                    table.CheckConstraint("chk_mw_order", "ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_maintenance_windows_study_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "study_rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_equipment",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    installed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_equipment", x => new { x.room_id, x.equipment_type_id });
                    table.CheckConstraint("ck_room_equipment_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_room_equipment_equipment_types_equipment_type_id",
                        column: x => x.equipment_type_id,
                        principalTable: "equipment_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_room_equipment_study_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "study_rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consumable_suppliers",
                columns: table => new
                {
                    consumable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supplier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supply_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    is_preferred = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consumable_suppliers", x => new { x.consumable_id, x.supplier_id });
                    table.CheckConstraint("ck_consumable_suppliers_supply_price", "supply_price >= 0");
                    table.ForeignKey(
                        name: "fk_consumable_suppliers_consumables_consumable_id",
                        column: x => x.consumable_id,
                        principalTable: "consumables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_consumable_suppliers_suppliers_supplier_id",
                        column: x => x.supplier_id,
                        principalTable: "suppliers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: true),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_audit_logs_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    department = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    year_of_study = table.Column<int>(type: "integer", nullable: false),
                    max_bookings_per_week = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    penalty_points = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    suspended_until = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_student_profiles", x => x.id);
                    table.CheckConstraint("ck_student_profiles_max_bookings", "max_bookings_per_week > 0");
                    table.CheckConstraint("ck_student_profiles_penalty_points", "penalty_points >= 0");
                    table.CheckConstraint("ck_student_profiles_year", "year_of_study BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_student_profiles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    objective = table.Column<string>(type: "text", nullable: false),
                    group_size = table.Column<int>(type: "integer", nullable: false),
                    preferred_date_from = table.Column<DateOnly>(type: "date", nullable: false),
                    preferred_date_to = table.Column<DateOnly>(type: "date", nullable: false),
                    preferred_time_from = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    preferred_time_to = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    sessions_required = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    session_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    budget = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Draft"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_requests", x => x.id);
                    table.CheckConstraint("chk_date_order", "preferred_date_to >= preferred_date_from");
                    table.CheckConstraint("chk_time_order", "preferred_time_to > preferred_time_from");
                    table.CheckConstraint("ck_booking_requests_budget", "budget > 0");
                    table.CheckConstraint("ck_booking_requests_duration", "session_duration_minutes BETWEEN 30 AND 480");
                    table.CheckConstraint("ck_booking_requests_group_size", "group_size BETWEEN 1 AND 50");
                    table.CheckConstraint("ck_booking_requests_sessions", "sessions_required BETWEEN 1 AND 7");
                    table.CheckConstraint("ck_booking_requests_status", "status IN ('Draft','Submitted','Processing','PendingApproval','Approved','Rejected','RevisionRequested','Completed','Cancelled','Failed')");
                    table.ForeignKey(
                        name: "fk_booking_requests_student_profiles_student_id",
                        column: x => x.student_id,
                        principalTable: "student_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_request_equipment",
                columns: table => new
                {
                    booking_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_required = table.Column<int>(type: "integer", nullable: false, defaultValue: 1)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_request_equipment", x => new { x.booking_request_id, x.equipment_type_id });
                    table.CheckConstraint("ck_booking_request_equipment_quantity", "quantity_required > 0");
                    table.ForeignKey(
                        name: "fk_booking_request_equipment_booking_requests_booking_request_",
                        column: x => x.booking_request_id,
                        principalTable: "booking_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_booking_request_equipment_equipment_types_equipment_type_id",
                        column: x => x.equipment_type_id,
                        principalTable: "equipment_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "booking_request_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_booking_request_items", x => x.id);
                    table.CheckConstraint("ck_booking_request_items_quantity", "quantity > 0");
                    table.ForeignKey(
                        name: "fk_booking_request_items_booking_requests_booking_request_id",
                        column: x => x.booking_request_id,
                        principalTable: "booking_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_booking_request_items_consumables_consumable_id",
                        column: x => x.consumable_id,
                        principalTable: "consumables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "email_notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_email = table.Column<string>(type: "citext", nullable: false),
                    template = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    booking_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Queued"),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    max_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_notifications", x => x.id);
                    table.CheckConstraint("ck_email_notifications_status", "status IN ('Queued','Sent','Failed','DeadLettered')");
                    table.ForeignKey(
                        name: "fk_email_notifications_booking_requests_booking_request_id",
                        column: x => x.booking_request_id,
                        principalTable: "booking_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "quotations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    room_fee = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    consumable_cost = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false, computedColumnSql: "room_fee + consumable_cost", stored: true),
                    budget_snapshot = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    within_budget = table.Column<bool>(type: "boolean", nullable: false, computedColumnSql: "room_fee + consumable_cost <= budget_snapshot", stored: true),
                    currency = table.Column<string>(type: "char(3)", nullable: false, defaultValue: "LKR"),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Draft"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotations", x => x.id);
                    table.CheckConstraint("ck_quotations_consumable_cost", "consumable_cost >= 0");
                    table.CheckConstraint("ck_quotations_room_fee", "room_fee >= 0");
                    table.CheckConstraint("ck_quotations_status", "status IN ('Draft','Proposed','Approved','Rejected','Superseded')");
                    table.ForeignKey(
                        name: "fk_quotations_booking_requests_booking_request_id",
                        column: x => x.booking_request_id,
                        principalTable: "booking_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "room_bookings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    checked_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Confirmed"),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_bookings", x => x.id);
                    table.CheckConstraint("chk_rb_order", "ends_at > starts_at");
                    table.CheckConstraint("ck_room_bookings_status", "status IN ('Confirmed','Cancelled','Completed','NoShow')");
                    table.ForeignKey(
                        name: "fk_room_bookings_booking_requests_booking_request_id",
                        column: x => x.booking_request_id,
                        principalTable: "booking_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_room_bookings_study_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "study_rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workflow_executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    objective = table.Column<string>(type: "text", nullable: false),
                    plan_json = table.Column<string>(type: "jsonb", nullable: true),
                    current_step = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total_steps = table.Column<int>(type: "integer", nullable: true),
                    attempt = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Started"),
                    error_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_executions", x => x.id);
                    table.CheckConstraint("ck_workflow_executions_status", "status IN ('Started','InProgress','PendingApproval','Approved','Rejected','Failed','Completed')");
                    table.ForeignKey(
                        name: "fk_workflow_executions_booking_requests_booking_request_id",
                        column: x => x.booking_request_id,
                        principalTable: "booking_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    booking_request_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    reserved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_reservations", x => x.id);
                    table.CheckConstraint("ck_stock_reservations_quantity", "quantity > 0");
                    table.CheckConstraint("ck_stock_reservations_status", "status IN ('Pending','Reserved','Released','Used')");
                    table.ForeignKey(
                        name: "fk_stock_reservations_booking_request_items_booking_request_it",
                        column: x => x.booking_request_item_id,
                        principalTable: "booking_request_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_stock_reservations_consumables_consumable_id",
                        column: x => x.consumable_id,
                        principalTable: "consumables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "approval_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: false),
                    decided_by_role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comments = table.Column<string>(type: "text", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approval_decisions", x => x.id);
                    table.CheckConstraint("ck_approval_decisions_decision", "decision IN ('Approved','Rejected','RevisionRequested')");
                    table.ForeignKey(
                        name: "fk_approval_decisions_quotations_quotation_id",
                        column: x => x.quotation_id,
                        principalTable: "quotations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_approval_decisions_users_decided_by",
                        column: x => x.decided_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quotation_line_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    quotation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    room_booking_id = table.Column<Guid>(type: "uuid", nullable: true),
                    consumable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    line_total = table.Column<decimal>(type: "numeric(12,2)", nullable: false, computedColumnSql: "quantity * unit_price", stored: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotation_line_items", x => x.id);
                    table.CheckConstraint("chk_line_shape", "(item_type = 'Room' AND room_booking_id IS NOT NULL AND consumable_id IS NULL) OR (item_type = 'Consumable' AND consumable_id IS NOT NULL AND room_booking_id IS NULL)");
                    table.CheckConstraint("ck_quotation_line_items_item_type", "item_type IN ('Room','Consumable')");
                    table.CheckConstraint("ck_quotation_line_items_quantity", "quantity > 0");
                    table.CheckConstraint("ck_quotation_line_items_unit_price", "unit_price >= 0");
                    table.ForeignKey(
                        name: "fk_quotation_line_items_consumables_consumable_id",
                        column: x => x.consumable_id,
                        principalTable: "consumables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_quotation_line_items_quotations_quotation_id",
                        column: x => x.quotation_id,
                        principalTable: "quotations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_quotation_line_items_room_bookings_room_booking_id",
                        column: x => x.room_booking_id,
                        principalTable: "room_bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "workflow_step_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step_number = table.Column<int>(type: "integer", nullable: false),
                    attempt = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    agent_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    tool_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    input_json = table.Column<string>(type: "jsonb", nullable: true),
                    output_json = table.Column<string>(type: "jsonb", nullable: true),
                    validation_result = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    validation_details = table.Column<string>(type: "text", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workflow_step_logs", x => x.id);
                    table.CheckConstraint("ck_workflow_step_logs_validation_result", "validation_result IN ('Pass','Fail','Warning')");
                    table.ForeignKey(
                        name: "fk_workflow_step_logs_workflow_executions_workflow_execution_id",
                        column: x => x.workflow_execution_id,
                        principalTable: "workflow_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stock_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    consumable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    balance_after = table.Column<int>(type: "integer", nullable: false),
                    booking_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    stock_reservation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_transactions", x => x.id);
                    table.CheckConstraint("ck_stock_transactions_quantity", "quantity <> 0");
                    table.CheckConstraint("ck_stock_transactions_type", "transaction_type IN ('StockIn','StockOut','Reserve','Release','Adjust')");
                    table.ForeignKey(
                        name: "fk_stock_transactions_booking_requests_booking_request_id",
                        column: x => x.booking_request_id,
                        principalTable: "booking_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_stock_transactions_consumables_consumable_id",
                        column: x => x.consumable_id,
                        principalTable: "consumables",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_transactions_stock_reservations_stock_reservation_id",
                        column: x => x.stock_reservation_id,
                        principalTable: "stock_reservations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_stock_transactions_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ad_quote",
                table: "approval_decisions",
                columns: new[] { "quotation_id", "decided_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_approval_decisions_decided_by",
                table: "approval_decisions",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entity",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_user",
                table: "audit_logs",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_booking_request_equipment_equipment_type_id",
                table: "booking_request_equipment",
                column: "equipment_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_booking_request_items_consumable_id",
                table: "booking_request_items",
                column: "consumable_id");

            migrationBuilder.CreateIndex(
                name: "uq_bri",
                table: "booking_request_items",
                columns: new[] { "booking_request_id", "consumable_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_br_status",
                table: "booking_requests",
                columns: new[] { "status", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_br_student",
                table: "booking_requests",
                columns: new[] { "student_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_consumable_suppliers_supplier_id",
                table: "consumable_suppliers",
                column: "supplier_id");

            migrationBuilder.CreateIndex(
                name: "ux_preferred",
                table: "consumable_suppliers",
                column: "consumable_id",
                unique: true,
                filter: "is_preferred");

            migrationBuilder.CreateIndex(
                name: "ix_cons_low",
                table: "consumables",
                column: "is_active",
                filter: "stock_quantity <= min_stock_level");

            migrationBuilder.CreateIndex(
                name: "ix_consumables_name",
                table: "consumables",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_email_due",
                table: "email_notifications",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_email_notifications_booking_request_id",
                table: "email_notifications",
                column: "booking_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_types_name",
                table: "equipment_types",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_maintenance_windows_room_id",
                table: "maintenance_windows",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_qli_quote",
                table: "quotation_line_items",
                column: "quotation_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotation_line_items_consumable_id",
                table: "quotation_line_items",
                column: "consumable_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotation_line_items_room_booking_id",
                table: "quotation_line_items",
                column: "room_booking_id");

            migrationBuilder.CreateIndex(
                name: "uq_quote_version",
                table: "quotations",
                columns: new[] { "booking_request_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_quote_active",
                table: "quotations",
                column: "booking_request_id",
                unique: true,
                filter: "status IN ('Proposed','Approved')");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_user",
                table: "refresh_tokens",
                columns: new[] { "user_id", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_rb_request",
                table: "room_bookings",
                column: "booking_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_rb_room",
                table: "room_bookings",
                columns: new[] { "room_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_room_equipment_equipment_type_id",
                table: "room_equipment",
                column: "equipment_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_res_cons",
                table: "stock_reservations",
                columns: new[] { "consumable_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_booking_request_item_id",
                table: "stock_reservations",
                column: "booking_request_item_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_booking_request_id",
                table: "stock_transactions",
                column: "booking_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_created_by",
                table: "stock_transactions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_stock_transactions_stock_reservation_id",
                table: "stock_transactions",
                column: "stock_reservation_id");

            migrationBuilder.CreateIndex(
                name: "ix_tx_cons",
                table: "stock_transactions",
                columns: new[] { "consumable_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_student_profiles_student_number",
                table: "student_profiles",
                column: "student_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_profiles_user_id",
                table: "student_profiles",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rooms_search",
                table: "study_rooms",
                columns: new[] { "is_active", "capacity" });

            migrationBuilder.CreateIndex(
                name: "ix_study_rooms_name",
                table: "study_rooms",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_study_rooms_qr_code",
                table: "study_rooms",
                column: "qr_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_suppliers_name",
                table: "suppliers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_wf_active",
                table: "workflow_executions",
                column: "booking_request_id",
                unique: true,
                filter: "status IN ('Started','InProgress','PendingApproval')");

            migrationBuilder.CreateIndex(
                name: "ix_step_wf",
                table: "workflow_step_logs",
                columns: new[] { "workflow_execution_id", "step_number" });

            migrationBuilder.CreateIndex(
                name: "uq_step",
                table: "workflow_step_logs",
                columns: new[] { "workflow_execution_id", "step_number", "attempt" },
                unique: true);

            // Generated tstzrange columns and the GiST-backed constraints that use them.
            // EF Core cannot express GENERATED ALWAYS AS ... STORED tstzrange columns or
            // EXCLUDE USING gist constraints, so both are added here with raw SQL exactly as
            // specified in DOCS/StudyHive_Master_Project_Relay_Plan.html §10. starts_at/ends_at
            // remain the only columns EF Core manages on these two tables.
            migrationBuilder.Sql(
                "ALTER TABLE maintenance_windows ADD COLUMN \"window\" tstzrange " +
                "GENERATED ALWAYS AS (tstzrange(starts_at, ends_at, '[)')) STORED;");
            migrationBuilder.Sql(
                "CREATE INDEX ix_mw_room ON maintenance_windows USING gist (room_id, \"window\");");

            migrationBuilder.Sql(
                "ALTER TABLE room_bookings ADD COLUMN slot tstzrange " +
                "GENERATED ALWAYS AS (tstzrange(starts_at, ends_at, '[)')) STORED;");
            migrationBuilder.Sql(
                "ALTER TABLE room_bookings ADD CONSTRAINT no_double_booking " +
                "EXCLUDE USING gist (room_id WITH =, slot WITH &&) WHERE (status = 'Confirmed');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approval_decisions");

            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "booking_request_equipment");

            migrationBuilder.DropTable(
                name: "consumable_suppliers");

            migrationBuilder.DropTable(
                name: "email_notifications");

            migrationBuilder.DropTable(
                name: "maintenance_windows");

            migrationBuilder.DropTable(
                name: "quotation_line_items");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "room_equipment");

            migrationBuilder.DropTable(
                name: "stock_transactions");

            migrationBuilder.DropTable(
                name: "workflow_step_logs");

            migrationBuilder.DropTable(
                name: "suppliers");

            migrationBuilder.DropTable(
                name: "quotations");

            migrationBuilder.DropTable(
                name: "room_bookings");

            migrationBuilder.DropTable(
                name: "equipment_types");

            migrationBuilder.DropTable(
                name: "stock_reservations");

            migrationBuilder.DropTable(
                name: "workflow_executions");

            migrationBuilder.DropTable(
                name: "study_rooms");

            migrationBuilder.DropTable(
                name: "booking_request_items");

            migrationBuilder.DropTable(
                name: "booking_requests");

            migrationBuilder.DropTable(
                name: "consumables");

            migrationBuilder.DropTable(
                name: "student_profiles");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
