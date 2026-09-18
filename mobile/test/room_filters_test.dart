import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/rooms_api.dart';
import 'package:mobile/screens/rooms/browse_rooms_screen.dart';
import 'package:mobile/state/rooms_provider.dart';

void main() {
  testWidgets('mobile room filter accepts any positive minimum capacity', (
    tester,
  ) async {
    final requested = <Uri>[];
    final client = MockClient((request) async {
      requested.add(request.url);
      return _jsonResponse({
        'items': [],
        'page': 1,
        'pageSize': 100,
        'totalItems': 0,
        'totalPages': 0,
      });
    });

    await tester.pumpWidget(
      ChangeNotifierProvider(
        create: (_) => RoomsProvider(RoomsApi(ApiClient(client: client))),
        child: const MaterialApp(home: BrowseRoomsScreen()),
      ),
    );
    await tester.pumpAndSettle();

    await tester.enterText(find.bySemanticsLabel('Minimum seats'), '3');
    await tester.tap(find.widgetWithText(FilledButton, 'Apply'));
    await tester.pumpAndSettle();

    final filteredRequest = requested.lastWhere(
      (uri) =>
          uri.path == '/api/rooms' &&
          uri.queryParameters.containsKey('capacity'),
    );
    expect(filteredRequest.queryParameters['capacity'], '3');
  });
}

http.Response _jsonResponse(Object body) => http.Response(
  jsonEncode(body),
  200,
  headers: {'content-type': 'application/json'},
);
