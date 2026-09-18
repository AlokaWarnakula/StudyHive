import 'package:flutter/foundation.dart';

import '../api/rooms_api.dart';
import '../models/room.dart';

/// S2 (Rooms & Availability) live state for the student app.
class RoomsProvider extends ChangeNotifier {
  final RoomsApi _api;
  RoomsProvider(this._api);

  /// Exposed for screens that need a single room or a schedule directly, without adding
  /// single-item state to this list-oriented provider — same escape hatch S1 uses.
  RoomsApi get api => _api;

  List<RoomListItem> _rooms = [];
  List<RoomEquipmentType> _equipmentTypes = [];
  RoomDetail? _selected;
  List<RoomScheduleSlot> _schedule = [];
  bool _loading = false;
  String? _error;

  List<RoomListItem> get rooms => _rooms;
  List<RoomEquipmentType> get equipmentTypes => _equipmentTypes;
  RoomDetail? get selected => _selected;
  List<RoomScheduleSlot> get schedule => _schedule;
  bool get loading => _loading;
  String? get error => _error;

  /// M-09. Filter chips only — the reference deliberately has no advanced search.
  Future<void> refresh({int? capacity, String? equipmentTypeId}) async {
    await _run(() async {
      if (_equipmentTypes.isEmpty) {
        _equipmentTypes = await _api.listEquipmentTypes();
      }
      _rooms = await _api.list(
        capacity: capacity,
        equipmentTypeId: equipmentTypeId,
      );
    });
  }

  /// The availability search, once S2 has built it.
  Future<void> searchAvailable({
    required String from,
    required String to,
    int? capacity,
    String? equipmentTypeId,
  }) async {
    await _run(() async {
      _rooms = await _api.searchAvailable(
        from: from,
        to: to,
        capacity: capacity,
        equipmentTypeId: equipmentTypeId,
      );
    });
  }

  /// M-10.
  Future<void> select(String roomId) async {
    await _run(() async {
      _selected = await _api.getById(roomId);
    });
  }

  /// M-11. Booked and maintenance slots are kept, not filtered out: the screen greys them so a
  /// student can see why a time is unavailable.
  Future<void> loadSchedule(String roomId, DateTime date) async {
    await _run(() async {
      _schedule = await _api.schedule(roomId, date);
    });
  }

  Future<void> _run(Future<void> Function() action) async {
    _loading = true;
    _error = null;
    notifyListeners();
    try {
      await action();
    } catch (e) {
      _error = e.toString();
    } finally {
      _loading = false;
      notifyListeners();
    }
  }
}
