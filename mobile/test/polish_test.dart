import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/rooms_api.dart';
import 'package:mobile/state/rooms_provider.dart';
import 'package:mobile/utils/colombo_time.dart';

http.Response _json(Object body, [int status = 200]) => http.Response(
  jsonEncode(body),
  status,
  headers: {'content-type': 'application/json'},
);

Map<String, dynamic> _room(String id, String name, {bool active = true}) => {
  'id': id,
  'name': name,
  'building': 'Main library',
  'capacity': 6,
  'hourlyRate': 150,
  'isActive': active,
};

Map<String, dynamic> _page(List<Map<String, dynamic>> items) => {
  'items': items,
  'page': 1,
  'pageSize': 100,
  'totalItems': items.length,
  'totalPages': 1,
};

RoomsProvider _provider({required bool availableFails}) {
  return RoomsProvider(
    RoomsApi(
      ApiClient(
        client: MockClient((request) async {
          switch (request.url.path) {
            case '/api/equipment':
              return _json(_page(const []));
            case '/api/rooms':
              return _json(
                _page([
                  _room('free', 'B-204'),
                  _room('busy', 'B-118'),
                  _room('off', 'C-301', active: false),
                ]),
              );
            case '/api/rooms/available':
              if (availableFails) return _json({'title': 'boom'}, 500);
              // The next hour, from now.
              final from = DateTime.parse(request.url.queryParameters['from']!);
              final to = DateTime.parse(request.url.queryParameters['to']!);
              expect(to.difference(from), const Duration(hours: 1));
              return _json(_page([_room('free', 'B-204')]));
          }
          return _json({'title': 'unexpected ${request.url.path}'}, 404);
        }),
      ),
    ),
  );
}

void main() {
  group('C-17 Free now', () {
    test('only rooms the availability search returns are Free now', () async {
      final provider = _provider(availableFails: false);
      await provider.refresh();

      final labels = {
        for (final room in provider.rooms)
          room.name: provider.availabilityLabelFor(room),
      };
      expect(labels, {'B-204': 'Free now', 'B-118': null, 'C-301': 'Inactive'});
    });

    test('a failed availability search shows no Free now tag at all', () async {
      final provider = _provider(availableFails: true);
      await provider.refresh();

      expect(provider.error, isNull);
      expect(provider.rooms, hasLength(3));
      expect(provider.rooms.map(provider.availabilityLabelFor), [
        null,
        null,
        'Inactive',
      ]);
    });
  });

  group('C-18 greeting', () {
    // Colombo is UTC+05:30.
    test('follows the Colombo clock', () {
      expect(
        greetingAt(DateTime.utc(2026, 10, 4, 1, 0)),
        'Good morning',
      ); // 06:30
      expect(
        greetingAt(DateTime.utc(2026, 10, 4, 6, 29)),
        'Good morning',
      ); // 11:59
      expect(
        greetingAt(DateTime.utc(2026, 10, 4, 6, 30)),
        'Good afternoon',
      ); // 12:00
      expect(
        greetingAt(DateTime.utc(2026, 10, 4, 11, 29)),
        'Good afternoon',
      ); // 16:59
      expect(
        greetingAt(DateTime.utc(2026, 10, 4, 11, 30)),
        'Good evening',
      ); // 17:00
      expect(
        greetingAt(DateTime.utc(2026, 10, 4, 14, 30)),
        'Good evening',
      ); // 20:00, the audit case
    });
  });

  group('CW-11 quotation room lines', () {
    test('an ISO instant becomes a Colombo day and time', () {
      expect(
        formatItemName('Quiet Study 101 2026-10-13T10:00:00+05:30'),
        'Quiet Study 101 · Tue 13 Oct, 10:00',
      );
      expect(
        formatItemName('Quiet Study 101 2026-10-13T04:30:00Z'),
        'Quiet Study 101 · Tue 13 Oct, 10:00',
      );
      expect(formatItemName('A4 printouts'), 'A4 printouts');
    });
  });
}
