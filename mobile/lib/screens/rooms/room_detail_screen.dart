import 'package:flutter/material.dart';

import '../../data/demo_seed.dart';
import '../../models/room.dart';
import '../../widgets/studyhive_ui.dart';
import '../create_request_screen.dart';
import 'room_schedule_screen.dart';

/// M-10 "Room detail" — GET /api/rooms/{id} plus installed equipment. The photo
/// runs edge to edge above the body, so this frame has no app bar of its own.
class RoomDetailScreen extends StatelessWidget {
  final RoomDetail? room;
  final bool previewEnabled;

  const RoomDetailScreen({
    super.key,
    this.room,
    this.previewEnabled = demoPreviewEnabled,
  });

  @override
  Widget build(BuildContext context) {
    if (room == null && !previewEnabled) {
      return Scaffold(
        appBar: AppBar(title: const Text('Room details')),
        body: const PreviewUnavailable(
          message: 'Room details will appear when the Rooms API is connected.',
        ),
      );
    }
    final selected = room ?? demoRooms.first;

    return Scaffold(
      body: Stack(
        children: [
          ScreenBody(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 24),
            children: [
              const Ph(label: 'room photo', height: 170),
              if (previewEnabled) const DemoPreviewBanner(),
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Heading('Room ${selected.name}'),
                        FNote(selected.building),
                      ],
                    ),
                  ),
                  const SizedBox(width: 12),
                  ShTag.forStatus(selected.availabilityLabel),
                ],
              ),
              Tile(
                children: [
                  Kv('Seats', '${selected.capacity} people'),
                  Kv('Rate',
                      'Rs. ${selected.hourlyRate.toStringAsFixed(0)} per hour'),
                  const Kv('Opening hours', '8 AM – 8 PM'),
                ],
              ),
              const Lbl('Equipment in this room'),
              Column(
                children: [
                  for (final item in selected.equipment) ...[
                    Kv.widget(
                      label: item.name.split(' ·').first,
                      trailing: ShTag(
                        item.name.contains('repair') ? 'Under repair' : 'Working',
                        tone: item.name.contains('repair')
                            ? TagTone.neutral
                            : TagTone.accent,
                      ),
                    ),
                    const SizedBox(height: 8),
                  ],
                ],
              ),
              PrimaryButton(
                'Book this room',
                onPressed: selected.isActive
                    ? () => Navigator.of(context).push(MaterialPageRoute(
                        builder: (_) => const CreateRequestScreen()))
                    : null,
              ),
              SecondaryButton(
                'See free times',
                onPressed: () => Navigator.of(context).push(
                  MaterialPageRoute(
                      builder: (_) => RoomScheduleScreen(room: selected)),
                ),
              ),
            ],
          ),
          SafeArea(
            child: IconButton(
              onPressed: () => Navigator.of(context).maybePop(),
              tooltip: 'Back',
              icon: const Icon(Icons.chevron_left, size: 26),
            ),
          ),
        ],
      ),
    );
  }
}
