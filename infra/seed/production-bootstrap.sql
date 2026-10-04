-- StudyHive production bootstrap (Railway): the rows the Development seeder would have created.
--
-- Production never runs DevDataSeeder, so a fresh deploy has no logins. Run this ONCE after the
-- API has started (it applies the migrations), then run demo-data.sql.
--
--   psql "$DATABASE_URL" -v staff_password='<a fresh password>' -f infra/seed/production-bootstrap.sql
--   psql "$DATABASE_URL" -f infra/seed/demo-data.sql
--
-- The password is passed on the command line, never stored in this file. It is hashed here with
-- pgcrypto's bcrypt (cost 11), which the API's BCrypt.Net verifier accepts. All five accounts get
-- the same password; demo-data.sql copies the student's hash to its demo students.
-- Safe to re-run: existing accounts and rows are left alone.

\if :{?staff_password}
\else
  \echo 'Pass -v staff_password=... (a fresh password, not the development one).'
  \quit
\endif

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

INSERT INTO users (id, email, password_hash, full_name, role) VALUES
  (gen_random_uuid(), 'student@studyhive.dev',      crypt(:'staff_password', gen_salt('bf', 11)), 'Demo Student',       'Student'),
  (gen_random_uuid(), 'librarian@studyhive.dev',    crypt(:'staff_password', gen_salt('bf', 11)), 'Demo Librarian',     'Librarian'),
  (gen_random_uuid(), 'storeofficer@studyhive.dev', crypt(:'staff_password', gen_salt('bf', 11)), 'Demo Store Officer', 'StoreOfficer'),
  (gen_random_uuid(), 'admin@studyhive.dev',        crypt(:'staff_password', gen_salt('bf', 11)), 'Demo Admin',         'Admin')
ON CONFLICT (email) DO NOTHING;

INSERT INTO student_profiles (id, user_id, student_number, department, year_of_study)
SELECT gen_random_uuid(), u.id, 'DEMO-STUDENT-001', 'Computing', 2
FROM users u
WHERE u.email = 'student@studyhive.dev'
  AND NOT EXISTS (SELECT 1 FROM student_profiles p WHERE p.user_id = u.id);

-- The three consumables demo-data.sql links to suppliers (same ids as DevDataSeeder).
INSERT INTO consumables (id, name, description, unit, unit_price, stock_quantity, min_stock_level) VALUES
  ('30000000-0000-0000-0000-000000000001', 'Whiteboard markers', 'Black and blue dry-erase markers.', 'marker', 60.00,   42,  10),
  ('30000000-0000-0000-0000-000000000002', 'A4 printouts',       'Black-and-white A4 printing.',      'page',    5.00, 1200, 200),
  ('30000000-0000-0000-0000-000000000003', 'HDMI cable',         'Temporarily out of stock.',         'cable',   0.00,    0,   2)
ON CONFLICT (id) DO NOTHING;

COMMIT;
