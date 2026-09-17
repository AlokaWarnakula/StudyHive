import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../data/demo_seed.dart';
import '../../models/room.dart';
import '../../state/rooms_provider.dart';
import '../../widgets/studyhive_ui.dart';
import '../create_request_screen.dart';
import 'room_schedule_screen.dart';

/// M-10 "Room detail" — live room and installed-equipment data.
class RoomDetailScreen extends StatefulWidget {
  final String? roomId;
  final RoomDetail? room;
  final bool previewEnabled;

  const RoomDetailScreen({
    super.key,
    this.roomId,
    this.room,
    this.previewEnabled = demoPreviewEnabled,
  });

  @override
  State<RoomDetailScreen> createState() => _RoomDetailScreenState();
}

class _RoomDetailScreenState extends State<RoomDetailScreen> {
  bool _requested = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!widget.previewEnabled && widget.roomId != null && !_requested) {
      _requested = true;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) context.read<RoomsProvider>().select(widget.roomId!);
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    if (widget.previewEnabled) {
      return _content(context, widget.room ?? demoRooms.first, preview: true);
    }
    if (widget.roomId == null) {
      return Scaffold(
          appBar: AppBar(title: const Text('Room details')),
          body: const PreviewUnavailable(
              message: 'Select a room to view its details.'));
    }
    return Consumer<RoomsProvider>(builder: (context, provider, _) {
      final selected =
          provider.selected?.id == widget.roomId ? provider.selected : null;
      if (provider.loading && selected == null) {
        return Scaffold(
            appBar: AppBar(title: const Text('Room details')),
            body: const Center(child: CircularProgressIndicator()));
      }
      if (provider.error != null && selected == null) {
        return Scaffold(
            appBar: AppBar(title: const Text('Room details')),
            body: PreviewUnavailable(message: provider.error!));
      }
      if (selected == null) {
        return Scaffold(
            appBar: AppBar(title: const Text('Room details')),
            body: const PreviewUnavailable(
                message: 'Room details are unavailable.'));
      }
      return _content(context, selected);
    });
  }

  Widget _content(BuildContext context, RoomDetail selected,
      {bool preview = false}) {
    return Scaffold(
        body: Stack(children: [
      ScreenBody(padding: const EdgeInsets.fromLTRB(16, 0, 16, 24), children: [
        const Ph(label: 'room photo', height: 170),
        if (preview) const DemoPreviewBanner(),
        Row(crossAxisAlignment: CrossAxisAlignment.start, children: [
          Expanded(
              child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                Heading('Room ${selected.name}'),
                FNote(selected.building)
              ])),
          const SizedBox(width: 12),
          ShTag.forStatus(selected.availabilityLabel),
        ]),
        Tile(children: [
          Kv('Seats', '${selected.capacity} people'),
          Kv('Rate', 'Rs. ${selected.hourlyRate.toStringAsFixed(0)} per hour'),
          if (preview) const Kv('Opening hours', '8 AM – 8 PM'),
          Kv('QR code', selected.qrCode),
        ]),
        const Lbl('Equipment in this room'),
        if (selected.equipment.isEmpty)
          const Tile(children: [Text('No equipment is assigned to this room.')])
        else
          Column(children: [
            for (final item in selected.equipment) ...[
              Kv.widget(
                  label: item.name.split(' ·').first,
                  trailing: preview
                      ? ShTag(
                          item.name.contains('repair')
                              ? 'Under repair'
                              : 'Working',
                          tone: item.name.contains('repair')
                              ? TagTone.neutral
                              : TagTone.accent)
                      : ShTag('${item.quantity} installed',
                          tone: TagTone.accent)),
              const SizedBox(height: 8),
            ]
          ]),
        PrimaryButton('Book this room',
            onPressed: selected.isActive
                ? () => Navigator.of(context).push(MaterialPageRoute(
                    builder: (_) => const CreateRequestScreen()))
                : null),
        SecondaryButton('See free times',
            onPressed: () => Navigator.of(context).push(MaterialPageRoute(
                  builder: (_) => RoomScheduleScreen(
                      room: selected, previewEnabled: preview),
                ))),
      ]),
      SafeArea(
          child: IconButton(
              onPressed: () => Navigator.of(context).maybePop(),
              tooltip: 'Back',
              icon: const Icon(Icons.chevron_left, size: 26))),
    ]));
  }
}
