/// Times the library works in. Asia/Colombo is a fixed UTC+05:30 with no daylight saving, so a
/// fixed offset is exact and does not depend on the phone's own time zone (AUDIT C-14).
const colomboOffset = Duration(hours: 5, minutes: 30);

const _weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
const _months = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
];

/// The instant as a wall-clock DateTime in Colombo (its fields read as Colombo time).
DateTime toColombo(DateTime instant) => instant.toUtc().add(colomboOffset);

String _two(int value) => value.toString().padLeft(2, '0');

/// "14:05"
String colomboHhmm(DateTime instant) {
  final c = toColombo(instant);
  return '${_two(c.hour)}:${_two(c.minute)}';
}

/// "Mon 6 Oct"
String colomboDay(DateTime instant) {
  final c = toColombo(instant);
  return '${_weekdays[c.weekday - 1]} ${c.day} ${_months[c.month - 1]}';
}

/// "Mon 6 Oct · 14:00–16:00"
String colomboSlot(DateTime startsAt, DateTime endsAt) =>
    '${colomboDay(startsAt)} · ${colomboHhmm(startsAt)}–${colomboHhmm(endsAt)}';

/// AUDIT C-18: "Good morning" until noon, "Good afternoon" until 17:00, then "Good evening",
/// all by the Colombo clock.
String greetingAt(DateTime instant) {
  final hour = toColombo(instant).hour;
  if (hour < 12) return 'Good morning';
  if (hour < 17) return 'Good afternoon';
  return 'Good evening';
}

final _isoInstant = RegExp(
  r'\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?(?:Z|[+-]\d{2}:\d{2})',
);

/// CW-11: quotation room lines arrive as "Quiet Study 101 2026-10-13T10:00:00+05:30"; show
/// "Quiet Study 101 · Tue 13 Oct, 10:00" (Colombo). Names without a timestamp are unchanged.
String formatItemName(String name) => name
    .replaceAllMapped(_isoInstant, (m) {
      final parsed = DateTime.tryParse(m[0]!);
      return parsed == null
          ? m[0]!
          : '· ${colomboDay(parsed)}, ${colomboHhmm(parsed)}';
    })
    .replaceAll(RegExp(r'\s+·'), ' ·');
