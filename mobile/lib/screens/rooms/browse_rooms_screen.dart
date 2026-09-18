import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:provider/provider.dart';

import '../../data/demo_seed.dart';
import '../../models/room.dart';
import '../../state/rooms_provider.dart';
import '../../widgets/studyhive_ui.dart';
import 'room_detail_screen.dart';

/// M-09 "Browse rooms" — live room list and availability filters.
class BrowseRoomsScreen extends StatefulWidget {
  final bool embedded;
  final bool previewEnabled;

  const BrowseRoomsScreen({
    super.key,
    this.embedded = false,
    this.previewEnabled = false,
  });

  @override
  State<BrowseRoomsScreen> createState() => _BrowseRoomsScreenState();
}

class _BrowseRoomsScreenState extends State<BrowseRoomsScreen> {
  static const _filters = ['All rooms', 'Available next 2 hours'];
  final TextEditingController _capacityController = TextEditingController();
  String _filter = 'All rooms';
  int? _minimumCapacity;
  String? _capacityError;
  String? _equipmentTypeId;
  bool _requested = false;

  @override
  void dispose() {
    _capacityController.dispose();
    super.dispose();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (!widget.previewEnabled && !_requested) {
      _requested = true;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) context.read<RoomsProvider>().refresh();
      });
    }
  }

  Future<void> _applyFilter(String value) async {
    setState(() => _filter = value);
    final provider = context.read<RoomsProvider>();
    if (value == 'Available next 2 hours') {
      final from = DateTime.now();
      await provider.searchAvailable(
        from: from.toUtc().toIso8601String(),
        to: from.add(const Duration(hours: 2)).toUtc().toIso8601String(),
        capacity: _minimumCapacity,
        equipmentTypeId: _equipmentTypeId,
      );
    } else {
      await provider.refresh(
        capacity: _minimumCapacity,
        equipmentTypeId: _equipmentTypeId,
      );
    }
  }

  Future<void> _applyCapacityFilter() async {
    final text = _capacityController.text.trim();
    final capacity = text.isEmpty ? null : int.tryParse(text);
    if (text.isNotEmpty && (capacity == null || capacity <= 0)) {
      setState(() => _capacityError = 'Enter a number above 0.');
      return;
    }

    setState(() {
      _minimumCapacity = capacity;
      _capacityError = null;
    });
    if (!widget.previewEnabled) await _applyFilter(_filter);
  }

  Future<void> _applyEquipmentFilter(String? value) async {
    setState(() => _equipmentTypeId = value?.isEmpty == true ? null : value);
    await _applyFilter(_filter);
  }

  @override
  Widget build(BuildContext context) {
    final body =
        widget.previewEnabled
            ? ScreenBody(
              gap: 12,
              children: [
                const DemoPreviewBanner(),
                FilterTags(
                  options: _filters,
                  value: _filter,
                  onChanged: (value) => setState(() => _filter = value),
                ),
                _capacityFilter(enabled: true),
                for (final room in demoRooms)
                  _RoomRow(room: room, preview: true),
              ],
            )
            : Consumer<RoomsProvider>(
              builder: (context, provider, _) {
                return ScreenBody(
                  gap: 12,
                  children: [
                    FilterTags(
                      options: _filters,
                      value: _filter,
                      onChanged: _applyFilter,
                    ),
                    _capacityFilter(enabled: !provider.loading),
                    DropdownButtonFormField<String>(
                      initialValue: _equipmentTypeId ?? '',
                      decoration: const InputDecoration(
                        labelText: 'Equipment type',
                        border: OutlineInputBorder(),
                      ),
                      items: [
                        const DropdownMenuItem(
                          value: '',
                          child: Text('All equipment'),
                        ),
                        for (final item in provider.equipmentTypes)
                          DropdownMenuItem(
                            value: item.id,
                            child: Text(item.name),
                          ),
                      ],
                      onChanged:
                          provider.loading ? null : _applyEquipmentFilter,
                    ),
                    if (provider.loading && provider.rooms.isEmpty)
                      const Center(child: CircularProgressIndicator())
                    else if (provider.error != null)
                      Tile(
                        children: [
                          Text(
                            provider.error!,
                            style: const TextStyle(color: Colors.red),
                          ),
                          TextButton(
                            onPressed: () => _applyFilter(_filter),
                            child: const Text('Try again'),
                          ),
                        ],
                      )
                    else if (provider.rooms.isEmpty)
                      const Tile(
                        children: [Text('No rooms match this filter.')],
                      )
                    else
                      for (final room in provider.rooms) _RoomRow(room: room),
                  ],
                );
              },
            );

    if (widget.embedded) return body;
    return Scaffold(appBar: AppBar(title: const Text('Rooms')), body: body);
  }

  Widget _capacityFilter({required bool enabled}) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: TextField(
            controller: _capacityController,
            keyboardType: TextInputType.number,
            inputFormatters: [FilteringTextInputFormatter.digitsOnly],
            decoration: InputDecoration(
              labelText: 'Minimum seats',
              hintText: 'e.g. 3',
              errorText: _capacityError,
              border: const OutlineInputBorder(),
            ),
            onSubmitted: (_) {
              if (enabled) _applyCapacityFilter();
            },
          ),
        ),
        const SizedBox(width: 8),
        FilledButton(
          onPressed: enabled ? _applyCapacityFilter : null,
          child: const Text('Apply'),
        ),
      ],
    );
  }
}

class _RoomRow extends StatelessWidget {
  final RoomListItem room;
  final bool preview;
  const _RoomRow({required this.room, this.preview = false});

  @override
  Widget build(BuildContext context) {
    return Tile.row(
      onTap:
          () => Navigator.of(context).push(
            MaterialPageRoute(
              builder:
                  (_) => RoomDetailScreen(
                    roomId: preview ? null : room.id,
                    room:
                        preview && room is RoomDetail
                            ? room as RoomDetail
                            : null,
                    previewEnabled: preview,
                  ),
            ),
          ),
      children: [
        const Ph(label: 'photo', width: 76, height: 76),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                room.name,
                style: const TextStyle(
                  fontSize: 17,
                  fontWeight: FontWeight.w600,
                ),
              ),
              const SizedBox(height: 4),
              FNote('${room.building} · ${room.capacity} seats'),
              const SizedBox(height: 4),
              FNote('Rs. ${room.hourlyRate.toStringAsFixed(0)} per hour'),
              const SizedBox(height: 6),
              ShTag.forStatus(room.availabilityLabel),
            ],
          ),
        ),
      ],
    );
  }
}
