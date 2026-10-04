-- StudyHive demo data (fake, for local development and the demo).
--
-- Safe to run more than once: every row has a fixed id and uses ON CONFLICT DO NOTHING.
-- Dates are relative to now(), so "tomorrow's clashing booking" stays tomorrow whenever you run it.
--
-- Run it AFTER the API has started once (it applies the migrations). Locally the API's DevDataSeeder
-- creates the role logins, the dev student and the preview consumables this script builds on; on
-- Railway, run production-bootstrap.sql first instead.
--
--   Local Docker:  docker exec -i studyhive-db psql -U studyhive -d studyhive < infra/seed/demo-data.sql
--   Railway:       psql "$DATABASE_PUBLIC_URL" -v ON_ERROR_STOP=1 -f infra/seed/demo-data.sql
--
-- Every demo id starts with "d" so it is easy to spot (and delete) later.

BEGIN;

-- ---------------------------------------------------------------------------------------------
-- S2: rooms, equipment, maintenance
-- ---------------------------------------------------------------------------------------------
INSERT INTO study_rooms (id, name, building, floor, capacity, hourly_rate, qr_code) VALUES
  ('d2000000-0000-0000-0000-000000000001', 'A-101 Silent Pod',      'Main Library', 1,  2,  300.00, 'STUDYHIVE-A-101'),
  ('d2000000-0000-0000-0000-000000000002', 'A-201 Group Room',      'Main Library', 2,  6,  600.00, 'STUDYHIVE-A-201'),
  ('d2000000-0000-0000-0000-000000000003', 'B-105 Seminar Room',    'Science Wing', 1, 20, 1500.00, 'STUDYHIVE-B-105'),
  ('d2000000-0000-0000-0000-000000000004', 'B-210 Media Lab',       'Science Wing', 2, 10, 1200.00, 'STUDYHIVE-B-210'),
  ('d2000000-0000-0000-0000-000000000005', 'C-003 Discussion Room', 'Engineering',  0,  8,  700.00, 'STUDYHIVE-C-003'),
  ('d2000000-0000-0000-0000-000000000006', 'C-110 Old Reading Room','Engineering',  1, 12,  500.00, 'STUDYHIVE-C-110')
ON CONFLICT DO NOTHING;

-- One inactive room, so "deactivated rooms are hidden" can be shown.
UPDATE study_rooms SET is_active = false WHERE id = 'd2000000-0000-0000-0000-000000000006';

INSERT INTO equipment_types (id, name, category, description) VALUES
  ('d2100000-0000-0000-0000-000000000001', 'Smart TV',          'Presentation',  '65-inch screen with HDMI and screen mirroring.'),
  ('d2100000-0000-0000-0000-000000000002', 'Video Conference Kit','Presentation','Camera, microphone and speaker bar.'),
  ('d2100000-0000-0000-0000-000000000003', 'Desktop PC',        'Computing',     'Lab PC with standard software.'),
  ('d2100000-0000-0000-0000-000000000004', 'Speakers',          'Audio',         'Portable powered speakers.')
ON CONFLICT DO NOTHING;

-- Projector / Whiteboard (70000000-...-01 / -02) already exist in older local databases, but
-- nothing else creates them, so a fresh (production) database needs them here.
INSERT INTO equipment_types (id, name, category, description) VALUES
  ('70000000-0000-0000-0000-000000000001', 'Projector',  'Presentation', 'Ceiling-mounted HD projector.'),
  ('70000000-0000-0000-0000-000000000002', 'Whiteboard', 'Furniture',    'Large erasable whiteboard.')
ON CONFLICT DO NOTHING;

INSERT INTO room_equipment (room_id, equipment_type_id, quantity) VALUES
  ('d2000000-0000-0000-0000-000000000001', '70000000-0000-0000-0000-000000000002', 1),
  ('d2000000-0000-0000-0000-000000000002', '70000000-0000-0000-0000-000000000002', 1),
  ('d2000000-0000-0000-0000-000000000002', 'd2100000-0000-0000-0000-000000000001', 1),
  ('d2000000-0000-0000-0000-000000000003', '70000000-0000-0000-0000-000000000001', 1),
  ('d2000000-0000-0000-0000-000000000003', '70000000-0000-0000-0000-000000000002', 2),
  ('d2000000-0000-0000-0000-000000000003', 'd2100000-0000-0000-0000-000000000004', 1),
  ('d2000000-0000-0000-0000-000000000004', '70000000-0000-0000-0000-000000000001', 1),
  ('d2000000-0000-0000-0000-000000000004', 'd2100000-0000-0000-0000-000000000002', 1),
  ('d2000000-0000-0000-0000-000000000004', 'd2100000-0000-0000-0000-000000000003', 10),
  ('d2000000-0000-0000-0000-000000000005', '70000000-0000-0000-0000-000000000002', 1),
  ('d2000000-0000-0000-0000-000000000005', 'd2100000-0000-0000-0000-000000000001', 1)
ON CONFLICT DO NOTHING;

-- A maintenance window on the Media Lab, the day after tomorrow 09:00-13:00 Colombo time,
-- so the Scheduling agent has something to avoid.
INSERT INTO maintenance_windows (id, room_id, starts_at, ends_at, reason) VALUES
  ('d2200000-0000-0000-0000-000000000001', 'd2000000-0000-0000-0000-000000000004',
   (date_trunc('day', now() AT TIME ZONE 'Asia/Colombo') + interval '2 days 9 hours')  AT TIME ZONE 'Asia/Colombo',
   (date_trunc('day', now() AT TIME ZONE 'Asia/Colombo') + interval '2 days 13 hours') AT TIME ZONE 'Asia/Colombo',
   'Projector bulb replacement and PC updates')
ON CONFLICT DO NOTHING;

-- The bookings below hang off DevDataSeeder's request #4 (Approved) and #7 (Completed). Production
-- never runs DevDataSeeder, so create those two for the demo student when they are missing.
INSERT INTO booking_requests (id, student_id, objective, group_size, preferred_date_from, preferred_date_to,
                              preferred_time_from, preferred_time_to, sessions_required,
                              session_duration_minutes, budget, notes, status, created_at, updated_at)
SELECT r.id, p.id, r.objective, r.group_size, current_date + r.from_days, current_date + r.from_days + 1,
       '09:00', '12:00', 2, 90, r.budget, 'Demo data', r.status, now() - r.age, now() - r.age
FROM student_profiles p
JOIN users u ON u.id = p.user_id AND u.email = 'student@studyhive.dev'
CROSS JOIN (VALUES
  ('10000000-0000-0000-0000-000000000004'::uuid, 'Host a data structures exam review session',  4, 75.00, 'Approved',  interval '18 days', -10),
  ('10000000-0000-0000-0000-000000000007'::uuid, 'Complete a distributed systems study workshop', 8, 40.00, 'Completed', interval '32 days', -30)
) AS r(id, objective, group_size, budget, status, age, from_days)
ON CONFLICT DO NOTHING;

-- The deliberate clash: A-201 is already booked tomorrow 14:00-16:00 Colombo time, which is the
-- obvious slot a demo request will ask for. Hangs off the seeded Approved request (#4).
INSERT INTO room_bookings (id, room_id, booking_request_id, starts_at, ends_at, status) VALUES
  ('d2300000-0000-0000-0000-000000000001', 'd2000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000004',
   (date_trunc('day', now() AT TIME ZONE 'Asia/Colombo') + interval '1 day 14 hours') AT TIME ZONE 'Asia/Colombo',
   (date_trunc('day', now() AT TIME ZONE 'Asia/Colombo') + interval '1 day 16 hours') AT TIME ZONE 'Asia/Colombo',
   'Confirmed')
ON CONFLICT DO NOTHING;

-- Past bookings (one checked in, one no-show) so the room utilisation report has history.
INSERT INTO room_bookings (id, room_id, booking_request_id, starts_at, ends_at, checked_in_at, status) VALUES
  ('d2300000-0000-0000-0000-000000000002', 'd2000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000007',
   (date_trunc('day', now() AT TIME ZONE 'Asia/Colombo') - interval '5 days' + interval '10 hours') AT TIME ZONE 'Asia/Colombo',
   (date_trunc('day', now() AT TIME ZONE 'Asia/Colombo') - interval '5 days' + interval '12 hours') AT TIME ZONE 'Asia/Colombo',
   (date_trunc('day', now() AT TIME ZONE 'Asia/Colombo') - interval '5 days' + interval '10 hours 5 minutes') AT TIME ZONE 'Asia/Colombo',
   'Completed'),
  ('d2300000-0000-0000-0000-000000000003', 'd2000000-0000-0000-0000-000000000005', '10000000-0000-0000-0000-000000000007',
   (date_trunc('day', now() AT TIME ZONE 'Asia/Colombo') - interval '3 days' + interval '15 hours') AT TIME ZONE 'Asia/Colombo',
   (date_trunc('day', now() AT TIME ZONE 'Asia/Colombo') - interval '3 days' + interval '17 hours') AT TIME ZONE 'Asia/Colombo',
   NULL, 'NoShow')
ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------------------------------
-- S3: consumables, suppliers, ledger
-- ---------------------------------------------------------------------------------------------
INSERT INTO consumables (id, name, description, unit, unit_price, stock_quantity, min_stock_level) VALUES
  ('d3000000-0000-0000-0000-000000000001', 'A3 colour printouts', 'Colour A3 posters and diagrams.',   'page',   40.00, 150,  30),
  ('d3000000-0000-0000-0000-000000000002', 'Sticky notes pack',   '100-sheet pack, assorted colours.', 'pack',  120.00,  25,  10),
  ('d3000000-0000-0000-0000-000000000003', 'Flip chart paper',    'A1 pad, 20 sheets.',                'pad',   450.00,   3,   5),
  ('d3000000-0000-0000-0000-000000000004', 'Extension cord',      '4-way, 3 metre.',                   'piece',   0.00,   6,   2),
  ('d3000000-0000-0000-0000-000000000005', 'AA batteries',        'For clickers and microphones.',     'pair',   80.00,   1,   8)
ON CONFLICT DO NOTHING;
-- Flip chart paper and AA batteries are deliberately below min_stock_level (low-stock alerts).

INSERT INTO suppliers (id, name, contact_email, phone, address) VALUES
  ('d3100000-0000-0000-0000-000000000001', 'Lanka Office Supplies', 'orders@lankaoffice.example', '+94 11 234 5678', '12 Galle Road, Colombo 03'),
  ('d3100000-0000-0000-0000-000000000002', 'PrintHub Kandy',        'sales@printhub.example',     '+94 81 222 3344', '45 Peradeniya Road, Kandy'),
  ('d3100000-0000-0000-0000-000000000003', 'TechMart Electronics',  'b2b@techmart.example',       '+94 11 555 0101', NULL)
ON CONFLICT DO NOTHING;

INSERT INTO consumable_suppliers (consumable_id, supplier_id, supply_price, is_preferred) VALUES
  ('30000000-0000-0000-0000-000000000001', 'd3100000-0000-0000-0000-000000000001',  45.00, true),
  ('30000000-0000-0000-0000-000000000002', 'd3100000-0000-0000-0000-000000000002',   3.00, true),
  ('30000000-0000-0000-0000-000000000003', 'd3100000-0000-0000-0000-000000000003', 900.00, true),
  ('d3000000-0000-0000-0000-000000000001', 'd3100000-0000-0000-0000-000000000002',  28.00, true),
  ('d3000000-0000-0000-0000-000000000002', 'd3100000-0000-0000-0000-000000000001',  90.00, true),
  ('d3000000-0000-0000-0000-000000000003', 'd3100000-0000-0000-0000-000000000001', 380.00, true),
  ('d3000000-0000-0000-0000-000000000004', 'd3100000-0000-0000-0000-000000000003', 1500.00, true),
  ('d3000000-0000-0000-0000-000000000005', 'd3100000-0000-0000-0000-000000000003',  60.00, true),
  ('d3000000-0000-0000-0000-000000000005', 'd3100000-0000-0000-0000-000000000001',  65.00, false)
ON CONFLICT DO NOTHING;

-- Opening StockIn ledger rows for the demo consumables, by the store officer.
INSERT INTO stock_transactions (id, consumable_id, transaction_type, quantity, balance_after, notes, created_by, created_at)
SELECT v.id::uuid, v.consumable_id::uuid, 'StockIn', v.qty, v.qty, 'Opening stock (demo data)', u.id, now() - interval '14 days'
FROM (VALUES
  ('d3200000-0000-0000-0000-000000000001', 'd3000000-0000-0000-0000-000000000001', 150),
  ('d3200000-0000-0000-0000-000000000002', 'd3000000-0000-0000-0000-000000000002',  25),
  ('d3200000-0000-0000-0000-000000000003', 'd3000000-0000-0000-0000-000000000003',   3),
  ('d3200000-0000-0000-0000-000000000004', 'd3000000-0000-0000-0000-000000000004',   6),
  ('d3200000-0000-0000-0000-000000000005', 'd3000000-0000-0000-0000-000000000005',   1)
) AS v(id, consumable_id, qty)
CROSS JOIN (SELECT id FROM users WHERE email = 'storeofficer@studyhive.dev') AS u
ON CONFLICT DO NOTHING;

-- ---------------------------------------------------------------------------------------------
-- S1: extra students, to demo every eligibility rule.
-- They reuse the dev student's password hash, so they all log in with the same dev password.
-- ---------------------------------------------------------------------------------------------
INSERT INTO users (id, email, password_hash, full_name, role)
SELECT v.id::uuid, v.email, s.password_hash, v.full_name, 'Student'
FROM (VALUES
  ('d1000000-0000-0000-0000-000000000001', 'nimal@studyhive.dev',  'Nimal Perera'),
  ('d1000000-0000-0000-0000-000000000002', 'kavya@studyhive.dev',  'Kavya Fernando'),
  ('d1000000-0000-0000-0000-000000000003', 'suspended@studyhive.dev', 'Ruwan Silva'),
  ('d1000000-0000-0000-0000-000000000004', 'penalty@studyhive.dev',   'Ishara Jayasuriya')
) AS v(id, email, full_name)
CROSS JOIN (SELECT password_hash FROM users WHERE email = 'student@studyhive.dev') AS s
ON CONFLICT DO NOTHING;

INSERT INTO student_profiles (id, user_id, student_number, department, year_of_study, penalty_points, suspended_until) VALUES
  ('d1100000-0000-0000-0000-000000000001', 'd1000000-0000-0000-0000-000000000001', 'IT24100001', 'Computing',   3, 0, NULL),
  ('d1100000-0000-0000-0000-000000000002', 'd1000000-0000-0000-0000-000000000002', 'IT24100002', 'Engineering', 1, 1, NULL),
  ('d1100000-0000-0000-0000-000000000003', 'd1000000-0000-0000-0000-000000000003', 'IT24100003', 'Business',    2, 0, (now() + interval '30 days')::date),
  ('d1100000-0000-0000-0000-000000000004', 'd1000000-0000-0000-0000-000000000004', 'IT24100004', 'Computing',   4, 3, NULL)
ON CONFLICT DO NOTHING;

COMMIT;
