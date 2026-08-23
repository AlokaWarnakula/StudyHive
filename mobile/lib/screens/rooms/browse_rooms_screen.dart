import 'package:flutter/material.dart';

import '../../data/demo_seed.dart';
import '../../models/room.dart';
import '../../widgets/studyhive_ui.dart';
import 'room_detail_screen.dart';

/// M-09 "Browse rooms" — GET /api/rooms?capacity=&equipment=. Filter chips only;
/// the reference deliberately has no advanced search.
class BrowseRoomsScreen extends StatefulWidget {
  final bool embedded;

  const BrowseRoomsScreen({super.key, this.embedded = false});

  @override
  State<BrowseRoomsScreen> createState() => _BrowseRoomsScreenState();
}

class _BrowseRoomsScreenState extends State<BrowseRoomsScreen> {
  static const _filters = ['All', '4+ people', 'Projector', 'Whiteboard'];

  String _filter = 'All';

  @override
  Widget build(BuildContext context) {
    final body = ScreenBody(
      gap: 12,
      children: [
        if (!demoPreviewEnabled)
          const Tile(children: [
            Text(
                'Rooms will appear when the Rooms & Availability API is connected.'),
          ])
        else ...[
          const DemoPreviewBanner(),
          FilterTags(
            options: _filters,
            value: _filter,
            onChanged: (value) => setState(() => _filter = value),
          ),
          for (final room in demoRooms.where(_matches)) _RoomRow(room: room),
        ],
      ],
    );

    if (widget.embedded) return body;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Rooms'),
        actions: [
          IconButton(
              tooltip: 'Search rooms',
              onPressed: () {},
              icon: const Icon(Icons.search, size: 22)),
          const SizedBox(width: 6),
        ],
      ),
      body: body,
    );
  }

  bool _matches(RoomDetail room) {
    return switch (_filter) {
      '4+ people' => room.capacity >= 4,
      'Projector' =>
        room.equipment.any((item) => item.name.contains('Projector')),
      'Whiteboard' =>
        room.equipment.any((item) => item.name.contains('Whiteboard')),
      _ => true,
    };
  }
}

class _RoomRow extends StatelessWidget {
  final RoomDetail room;

  const _RoomRow({required this.room});

  @override
  Widget build(BuildContext context) {
    return Tile.row(
      onTap: () => Navigator.of(context).push(
        MaterialPageRoute(builder: (_) => RoomDetailScreen(room: room)),
      ),
      children: [
        const Ph(label: 'photo', width: 76, height: 76),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(room.name,
                  style: const TextStyle(
                      fontSize: 17, fontWeight: FontWeight.w600)),
              const SizedBox(height: 4),
              FNote('${room.building} · ${room.capacity} seats'),
              const SizedBox(height: 4),
              FNote(
                room.equipment
                    .map((item) => item.name.split(' ·').first)
                    .join(' · '),
                maxLines: 1,
              ),
              const SizedBox(height: 6),
              ShTag.forStatus(room.availabilityLabel),
            ],
          ),
        ),
      ],
    );
  }
}
