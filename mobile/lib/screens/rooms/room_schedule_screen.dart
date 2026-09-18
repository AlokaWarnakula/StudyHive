import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../data/demo_seed.dart';
import '../../models/room.dart';
import '../../state/rooms_provider.dart';
import '../../theme/app_theme.dart';
import '../../widgets/studyhive_ui.dart';
import '../create_request_screen.dart';

/// M-11 "Free times" — live daily schedule with unavailable periods disabled.
class RoomScheduleScreen extends StatefulWidget {
  final RoomDetail? room;
  final bool previewEnabled;

  const RoomScheduleScreen(
      {super.key, this.room, this.previewEnabled = demoPreviewEnabled});

  @override
  State<RoomScheduleScreen> createState() => _RoomScheduleScreenState();
}

class _RoomScheduleScreenState extends State<RoomScheduleScreen> {
  late final List<DateTime> _days;
  int _day = 0;
  int? _selectedSlot;
  bool _requested = false;

  @override
  void initState() {
    super.initState();
    final today = DateTime.now();
    _days = List.generate(
        5, (index) => DateTime(today.year, today.month, today.day + index));
    if (widget.previewEnabled) _selectedSlot = 3;
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!widget.previewEnabled && widget.room != null && !_requested) {
      _requested = true;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) {
          context
              .read<RoomsProvider>()
              .loadSchedule(widget.room!.id, _days[_day]);
        }
      });
    }
  }

  Future<void> _selectDay(int index) async {
    setState(() {
      _day = index;
      _selectedSlot = null;
    });
    await context
        .read<RoomsProvider>()
        .loadSchedule(widget.room!.id, _days[index]);
  }

  @override
  Widget build(BuildContext context) {
    final room =
        widget.room ?? (widget.previewEnabled ? demoRooms.first : null);
    if (room == null) {
      return Scaffold(
          appBar: AppBar(title: const Text('Room free times')),
          body: const PreviewUnavailable(
              message: 'Select a room to view its schedule.'));
    }
    if (widget.previewEnabled) return _preview(room);
    return Consumer<RoomsProvider>(builder: (context, provider, _) {
      final rows = _rowsFor(_days[_day], provider.schedule);
      return Scaffold(
          appBar: AppBar(title: Text('${room.name} · free times')),
          body: ScreenBody(children: [
            _dayPicker((index) => _selectDay(index)),
            if (provider.loading)
              const Center(child: CircularProgressIndicator())
            else if (provider.error != null)
              Tile(children: [
                Text(provider.error!, style: const TextStyle(color: Colors.red))
              ])
            else
              Column(children: [
                for (var i = 0; i < rows.length; i++) ...[
                  if (i > 0) const SizedBox(height: 8),
                  _SlotRow(
                      label: rows[i].label,
                      status: i == _selectedSlot ? 'Your pick' : rows[i].status,
                      picked: i == _selectedSlot,
                      disabled: rows[i].status != 'Free',
                      onTap: rows[i].status == 'Free'
                          ? () => setState(() => _selectedSlot = i)
                          : null),
                ]
              ]),
            PrimaryButton(
                _selectedSlot == null
                    ? 'Choose a free time'
                    : 'Use ${rows[_selectedSlot!].label}',
                onPressed: _selectedSlot == null
                    ? null
                    : () => Navigator.of(context).push(MaterialPageRoute(
                        builder: (_) => const CreateRequestScreen()))),
          ]));
    });
  }

  Widget _dayPicker(ValueChanged<int> onChanged) => SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(children: [
        for (var i = 0; i < _days.length; i++) ...[
          if (i > 0) const SizedBox(width: 8),
          SizedBox(
              width: 62,
              child: Tile(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 8, vertical: 10),
                  gap: 2,
                  tinted: i == _day,
                  onTap: () => onChanged(i),
                  children: [
                    Center(child: FNote(_weekday(_days[i]))),
                    Center(
                        child: Text('${_days[i].day}',
                            style: const TextStyle(
                                fontSize: 14, fontWeight: FontWeight.w500)))
                  ])),
        ]
      ]));

  Widget _preview(RoomDetail room) {
    const slots = [
      ('8:00 – 10:00 AM', 'Free'),
      ('10:00 – 12:00 PM', 'Booked'),
      ('12:00 – 2:00 PM', 'Free'),
      ('2:00 – 4:00 PM', 'Free'),
      ('4:00 – 6:00 PM', 'Maintenance'),
      ('6:00 – 8:00 PM', 'Free'),
    ];
    return Scaffold(
        appBar: AppBar(title: Text('${room.name} · free times')),
        body: ScreenBody(children: [
          const DemoPreviewBanner(),
          _dayPicker((index) => setState(() => _day = index)),
          Column(children: [
            for (var i = 0; i < slots.length; i++) ...[
              if (i > 0) const SizedBox(height: 8),
              _SlotRow(
                  label: slots[i].$1,
                  status: i == _selectedSlot ? 'Your pick' : slots[i].$2,
                  picked: i == _selectedSlot,
                  disabled: slots[i].$2 != 'Free',
                  onTap: slots[i].$2 == 'Free'
                      ? () => setState(() => _selectedSlot = i)
                      : null),
            ]
          ]),
          PrimaryButton('Use ${slots[_selectedSlot ?? 0].$1}',
              onPressed: () => Navigator.of(context).push(MaterialPageRoute(
                  builder: (_) => const CreateRequestScreen()))),
        ]));
  }
}

List<_ScheduleRow> _rowsFor(DateTime day, List<RoomScheduleSlot> busy) {
  return List.generate(6, (index) {
    final start = DateTime(day.year, day.month, day.day, 8 + index * 2);
    final end = start.add(const Duration(hours: 2));
    final conflict = busy.cast<RoomScheduleSlot?>().firstWhere((slot) {
      if (slot == null) return false;
      final busyStart = DateTime.parse(slot.startsAt).toLocal();
      final busyEnd = DateTime.parse(slot.endsAt).toLocal();
      return busyStart.isBefore(end) && busyEnd.isAfter(start);
    }, orElse: () => null);
    return _ScheduleRow(
        '${_time(start)} – ${_time(end)}', conflict?.kind ?? 'Free');
  });
}

String _weekday(DateTime date) =>
    const ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'][date.weekday - 1];
String _time(DateTime value) {
  final hour = value.hour == 0
      ? 12
      : value.hour > 12
          ? value.hour - 12
          : value.hour;
  return '$hour:00 ${value.hour >= 12 ? 'PM' : 'AM'}';
}

class _ScheduleRow {
  final String label;
  final String status;
  const _ScheduleRow(this.label, this.status);
}

class _SlotRow extends StatelessWidget {
  final String label;
  final String status;
  final bool picked;
  final bool disabled;
  final VoidCallback? onTap;
  const _SlotRow(
      {required this.label,
      required this.status,
      required this.picked,
      required this.disabled,
      required this.onTap});
  @override
  Widget build(BuildContext context) => Tile.row(
          padding: const EdgeInsets.all(14),
          tinted: picked,
          opacity: disabled ? 0.5 : null,
          onTap: onTap,
          children: [
            Expanded(
                child: Text(label,
                    style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w500,
                        color: AppColors.text))),
            ShTag.forStatus(status),
          ]);
}
