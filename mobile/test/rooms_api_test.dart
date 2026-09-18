import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/rooms_api.dart';

http.Response jsonResponse(Object body) => http.Response(
  jsonEncode(body),
  200,
  headers: {'content-type': 'application/json'},
);

void main() {
  test(
    'room list uses the paging contract and maps live room fields',
    () async {
      late Uri requested;
      final api = RoomsApi(
        ApiClient(
          client: MockClient((request) async {
            requested = request.url;
            return jsonResponse({
              'items': [
                {
                  'id': 'room-1',
                  'name': 'B-204',
                  'building': 'Main library',
                  'capacity': 6,
                  'hourlyRate': 150,
                  'isActive': true,
                },
              ],
              'page': 1,
              'pageSize': 100,
              'totalItems': 1,
              'totalPages': 1,
            });
          }),
        ),
      );

      final rooms = await api.list(capacity: 3, equipmentTypeId: 'projector-1');

      expect(requested.path, '/api/rooms');
      expect(requested.queryParameters['pageSize'], '100');
      expect(requested.queryParameters['sortBy'], 'name');
      expect(requested.queryParameters['capacity'], '3');
      expect(requested.queryParameters['equipmentTypeId'], 'projector-1');
      expect(rooms.single.name, 'B-204');
      expect(rooms.single.availabilityLabel, 'Active');
    },
  );

  test(
    'equipment filter options come from the active equipment catalogue',
    () async {
      final api = RoomsApi(
        ApiClient(
          client: MockClient((request) async {
            return jsonResponse({
              'items': [
                {'id': 'projector-1', 'name': 'Projector', 'isActive': true},
                {
                  'id': 'retired-1',
                  'name': 'Retired monitor',
                  'isActive': false,
                },
              ],
              'page': 1,
              'pageSize': 100,
              'totalItems': 2,
              'totalPages': 1,
            });
          }),
        ),
      );

      final equipment = await api.listEquipmentTypes();

      expect(equipment, hasLength(1));
      expect(equipment.single.name, 'Projector');
    },
  );

  test('availability sends from, to and capacity', () async {
    late Uri requested;
    final api = RoomsApi(
      ApiClient(
        client: MockClient((request) async {
          requested = request.url;
          return jsonResponse({
            'items': [],
            'page': 1,
            'pageSize': 100,
            'totalItems': 0,
            'totalPages': 0,
          });
        }),
      ),
    );

    await api.searchAvailable(
      from: '2026-09-10T08:00:00Z',
      to: '2026-09-10T10:00:00Z',
      capacity: 4,
    );

    expect(requested.path, '/api/rooms/available');
    expect(requested.queryParameters['capacity'], '4');
    expect(requested.queryParameters['from'], '2026-09-10T08:00:00Z');
  });

  test('schedule uses from/to and maps the API kind field', () async {
    late Uri requested;
    final api = RoomsApi(
      ApiClient(
        client: MockClient((request) async {
          requested = request.url;
          return jsonResponse([
            {
              'roomId': 'room-1',
              'roomName': 'B-204',
              'startsAt': '2026-09-10T08:00:00Z',
              'endsAt': '2026-09-10T10:00:00Z',
              'kind': 'Maintenance',
            },
          ]);
        }),
      ),
    );

    final slots = await api.schedule('room-1', DateTime(2026, 9, 10));

    expect(requested.path, '/api/rooms/room-1/schedule');
    expect(requested.queryParameters, containsPair('from', isNotEmpty));
    expect(requested.queryParameters, containsPair('to', isNotEmpty));
    expect(slots.single.kind, 'Maintenance');
  });

  test('check-in posts the QR code and maps the confirmed booking', () async {
    late http.Request sent;
    final api = RoomsApi(
      ApiClient(
        client: MockClient((request) async {
          sent = request;
          return jsonResponse({
            'bookingId': 'booking-1',
            'roomId': 'room-1',
            'roomName': 'B-204',
            'startsAt': '2026-09-10T08:00:00Z',
            'endsAt': '2026-09-10T10:00:00Z',
            'checkedInAt': '2026-09-10T07:55:00Z',
            'status': 'Confirmed',
          });
        }),
      ),
    );

    final result = await api.checkIn('booking-1', 'room-b-204');

    expect(sent.method, 'POST');
    expect(sent.url.path, '/api/room-bookings/booking-1/check-in');
    expect(jsonDecode(sent.body), {'qrCode': 'room-b-204'});
    expect(result.roomName, 'B-204');
    expect(result.status, 'Confirmed');
  });
}
