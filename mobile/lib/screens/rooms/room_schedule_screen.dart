import 'package:flutter/material.dart';

import '../../data/demo_seed.dart';
import '../../models/room.dart';
import '../../theme/app_theme.dart';
import '../../widgets/studyhive_ui.dart';
import '../create_request_screen.dart';

/// M-11 "Free times" — GET /api/rooms/{id}/schedule?date=. Booked and
/// maintenance slots are greyed and not selectable.
class RoomScheduleScreen extends StatefulWidget {
  final RoomDetail? room;
  final bool previewEnabled;

  const RoomScheduleScreen({
    super.key,
    this.room,
    this.previewEnabled = demoPreviewEnabled,
  });

  @override
  State<RoomScheduleScreen> createState() => _RoomScheduleScreenState();
}

class _RoomScheduleScreenState extends State<RoomScheduleScreen> {
  static const _days = [
    ('Mon', '24'),
    ('Tue', '25'),
    ('Wed', '26'),
    ('Thu', '27'),
    ('Fri', '28'),
  ];
  static const _slots = [
    ('8:00 – 10:00 AM', 'Free'),
    ('10:00 – 12:00 PM', 'Booked'),
    ('12:00 – 2:00 PM', 'Free'),
    ('2:00 – 4:00 PM', 'Free'),
    ('4:00 – 6:00 PM', 'Maintenance'),
    ('6:00 – 8:00 PM', 'Free'),
  ];
  static const _unavailable = {'Booked', 'Maintenance'};

  int _day = 0;
  int _slot = 3;

  @override
  Widget build(BuildContext context) {
    if (!widget.previewEnabled) {
      return Scaffold(
        appBar: AppBar(title: const Text('Room free times')),
        body: const PreviewUnavailable(
          message:
              'Room schedules will appear when the Availability API is connected.',
        ),
      );
    }
    final room = widget.room ?? demoRooms.first;

    return Scaffold(
      appBar: AppBar(title: Text('${room.name} · free times')),
      body: ScreenBody(
        children: [
          const DemoPreviewBanner(),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [
                for (var i = 0; i < _days.length; i++) ...[
                  if (i > 0) const SizedBox(width: 8),
                  SizedBox(
                    width: 62,
                    child: Tile(
                      padding:
                          const EdgeInsets.symmetric(horizontal: 8, vertical: 10),
                      gap: 2,
                      tinted: i == _day,
                      onTap: () => setState(() => _day = i),
                      children: [
                        Center(child: FNote(_days[i].$1)),
                        Center(
                          child: Text(_days[i].$2,
                              style: const TextStyle(
                                  fontSize: 14, fontWeight: FontWeight.w500)),
                        ),
                      ],
                    ),
                  ),
                ],
              ],
            ),
          ),
          Column(
            children: [
              for (var i = 0; i < _slots.length; i++) ...[
                if (i > 0) const SizedBox(height: 8),
                _SlotRow(
                  label: _slots[i].$1,
                  status: i == _slot ? 'Your pick' : _slots[i].$2,
                  picked: i == _slot,
                  disabled: _unavailable.contains(_slots[i].$2),
                  onTap: _unavailable.contains(_slots[i].$2)
                      ? null
                      : () => setState(() => _slot = i),
                ),
              ],
            ],
          ),
          PrimaryButton(
            'Use ${_slots[_slot].$1}',
            onPressed: () => Navigator.of(context).push(
                MaterialPageRoute(builder: (_) => const CreateRequestScreen())),
          ),
        ],
      ),
    );
  }
}

class _SlotRow extends StatelessWidget {
  final String label;
  final String status;
  final bool picked;
  final bool disabled;
  final VoidCallback? onTap;

  const _SlotRow({
    required this.label,
    required this.status,
    required this.picked,
    required this.disabled,
    required this.onTap,
  });

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
                    color: AppColors.text)),
          ),
          ShTag.forStatus(status),
        ],
      );
}
