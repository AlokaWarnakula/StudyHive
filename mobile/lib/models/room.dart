/// S2 (Rooms & Availability) view models — mirrors web/src/types/rooms.ts and the locked schema
/// (api/src/StudyHive.Api/Data/Entities/S2/*.cs). No API client exists for these yet.
class RoomListItem {
  final String id;
  final String name;
  final String building;
  final int capacity;
  final double hourlyRate;
  final bool isActive;

  /// The free/busy line the room list and detail header show. Supplied by the
  /// availability source; falls back to the room's active flag.
  final String? availability;

  const RoomListItem({
    required this.id,
    required this.name,
    required this.building,
    required this.capacity,
    required this.hourlyRate,
    required this.isActive,
    this.availability,
  });

  String get availabilityLabel =>
      availability ?? (isActive ? 'Free now' : 'Maintenance today');
}

class RoomEquipmentItem {
  final String equipmentTypeId;
  final String name;
  final int quantity;

  const RoomEquipmentItem(
      {required this.equipmentTypeId,
      required this.name,
      required this.quantity});
}

class RoomDetail extends RoomListItem {
  final String qrCode;
  final List<RoomEquipmentItem> equipment;

  const RoomDetail({
    required super.id,
    required super.name,
    required super.building,
    required super.capacity,
    required super.hourlyRate,
    required super.isActive,
    super.availability,
    required this.qrCode,
    required this.equipment,
  });
}

class RoomScheduleSlot {
  final String roomId;
  final String startsAt;
  final String endsAt;
  final String status;

  const RoomScheduleSlot(
      {required this.roomId,
      required this.startsAt,
      required this.endsAt,
      required this.status});
}
