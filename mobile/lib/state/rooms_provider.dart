import 'package:flutter/foundation.dart';

import '../api/rooms_api.dart';
import '../models/room.dart';

/// S2 (Rooms & Availability) state for the student app. SCAFFOLD.
///
/// The state machine is complete and mirrors `BookingRequestsProvider`, which is the working S1
/// reference. What is missing is the endpoint underneath: every call currently lands on a 501, so
/// [error] is set and [rooms] stays empty. That is the correct behaviour until S2 builds the API —
/// the screens show their real error state rather than pretending.
///
/// S2, to bring this to life:
///   1. Implement `api/src/StudyHive.Api/Controllers/Rooms/RoomsController.cs`.
///   2. Register this provider in `main.dart`'s MultiProvider, sharing `authProvider.apiClient`
///      the same way `BookingRequestsProvider` is registered.
///   3. Point M-09 / M-10 / M-11 at it instead of their Debug-only preview source.
class RoomsProvider extends ChangeNotifier {
  final RoomsApi _api;
  RoomsProvider(this._api);

  /// Exposed for screens that need a single room or a schedule directly, without adding
  /// single-item state to this list-oriented provider — same escape hatch S1 uses.
  RoomsApi get api => _api;

  List<RoomListItem> _rooms = [];
  RoomDetail? _selected;
  List<RoomScheduleSlot> _schedule = [];
  bool _loading = false;
  String? _error;

  List<RoomListItem> get rooms => _rooms;
  RoomDetail? get selected => _selected;
  List<RoomScheduleSlot> get schedule => _schedule;
  bool get loading => _loading;
  String? get error => _error;

  /// M-09. Filter chips only — the reference deliberately has no advanced search.
  Future<void> refresh({int? capacity, String? equipmentTypeId}) async {
    await _run(() async {
      _rooms = await _api.list(capacity: capacity, equipmentTypeId: equipmentTypeId);
    });
  }

  /// The availability search, once S2 has built it.
  Future<void> searchAvailable({
    required String from,
    required String to,
    int? capacity,
  }) async {
    await _run(() async {
      _rooms = await _api.searchAvailable(from: from, to: to, capacity: capacity);
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
  Future<void> loadSchedule(String roomId, String date) async {
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
