import '../models/room.dart';
import 'api_client.dart';

/// S2 (Rooms & Availability) — student-facing room API. SCAFFOLD.
///
/// The calls are written and typed against the locked schema
/// (`api/src/StudyHive.Api/Data/Entities/S2/`), but the endpoints behind them return 501 until S2
/// implements `api/src/StudyHive.Api/Controllers/Rooms/`. Calling one today throws an
/// [ApiException] with status 501 — the honest answer. Nothing here invents data.
///
/// Screens this backs: M-09 Browse rooms, M-10 Room detail, M-11 Free times.
///
/// S2, to switch a screen on:
///   1. Implement the endpoint.
///   2. Register [RoomsProvider] in `main.dart`'s MultiProvider, sharing `authProvider.apiClient`
///      exactly the way `BookingRequestsProvider` is registered.
///   3. Replace the screen's Debug-only preview source with the provider's state.
///
/// `booking_requests_api.dart` is the working reference for all of this — it is a real S1 client
/// against a real endpoint.
class RoomsApi {
  final ApiClient _client;
  const RoomsApi(this._client);

  /// TODO(S2): GET /api/rooms — backs M-09.
  Future<List<RoomListItem>> list({int? capacity, String? equipmentTypeId}) async {
    final query = <String, String>{
      'pageSize': '100',
      if (capacity != null) 'capacity': '$capacity',
      if (equipmentTypeId != null) 'equipmentTypeId': equipmentTypeId,
    };
    final qs = query.entries.map((e) => '${e.key}=${e.value}').join('&');
    final response = await _client.get('/api/rooms?$qs') as Map<String, dynamic>;
    final items = response['items'] as List<dynamic>;
    return items.map((e) => _roomFromJson(e as Map<String, dynamic>)).toList();
  }

  /// TODO(S2): GET /api/rooms/available — the availability search.
  ///
  /// Capacity, equipment, existing bookings and maintenance windows all at once. The plan calls
  /// this the hardest single query in the system; it is also the one an examiner will ask S2 about.
  Future<List<RoomListItem>> searchAvailable({
    required String from,
    required String to,
    int? capacity,
  }) async {
    final query = <String, String>{
      'from': from,
      'to': to,
      if (capacity != null) 'capacity': '$capacity',
    };
    final qs = query.entries.map((e) => '${e.key}=${e.value}').join('&');
    final response = await _client.get('/api/rooms/available?$qs') as Map<String, dynamic>;
    final items = response['items'] as List<dynamic>;
    return items.map((e) => _roomFromJson(e as Map<String, dynamic>)).toList();
  }

  /// TODO(S2): GET /api/rooms/{id} — backs M-10, room plus installed equipment.
  Future<RoomDetail> getById(String id) async {
    final json = await _client.get('/api/rooms/$id') as Map<String, dynamic>;
    final equipment = (json['equipment'] as List<dynamic>? ?? const [])
        .map((e) => RoomEquipmentItem(
              equipmentTypeId: (e as Map<String, dynamic>)['equipmentTypeId'] as String,
              name: e['name'] as String,
              quantity: e['quantity'] as int,
            ))
        .toList();
    return RoomDetail(
      id: json['id'] as String,
      name: json['name'] as String,
      building: json['building'] as String,
      capacity: json['capacity'] as int,
      hourlyRate: (json['hourlyRate'] as num).toDouble(),
      isActive: json['isActive'] as bool,
      qrCode: json['qrCode'] as String,
      equipment: equipment,
    );
  }

  /// TODO(S2): GET /api/rooms/{id}/schedule?date= — backs M-11.
  ///
  /// Booked and maintenance slots come back so the screen can grey them out; they are not
  /// filtered away, because M-11 shows why a time is unavailable.
  Future<List<RoomScheduleSlot>> schedule(String roomId, String date) async {
    final response =
        await _client.get('/api/rooms/$roomId/schedule?date=$date') as List<dynamic>;
    return response
        .map((e) => RoomScheduleSlot(
              roomId: (e as Map<String, dynamic>)['roomId'] as String,
              startsAt: e['startsAt'] as String,
              endsAt: e['endsAt'] as String,
              status: e['status'] as String,
            ))
        .toList();
  }

  /// TODO(S2): POST /api/room-bookings/{id}/check-in — backs M-14 / M-15.
  Future<void> checkIn(String bookingId, String qrCode) async {
    await _client.post('/api/room-bookings/$bookingId/check-in', body: {'qrCode': qrCode});
  }

  static RoomListItem _roomFromJson(Map<String, dynamic> json) => RoomListItem(
        id: json['id'] as String,
        name: json['name'] as String,
        building: json['building'] as String,
        capacity: json['capacity'] as int,
        hourlyRate: (json['hourlyRate'] as num).toDouble(),
        isActive: json['isActive'] as bool,
        availability: json['availability'] as String?,
      );
}
