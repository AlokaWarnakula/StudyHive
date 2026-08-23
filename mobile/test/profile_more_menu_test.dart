import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/booking_requests_api.dart';
import 'package:mobile/api/student_profiles_api.dart';
import 'package:mobile/screens/quotation/booking_history_screen.dart';
import 'package:mobile/screens/consumables/browse_consumables_screen.dart';
import 'package:mobile/screens/profile_screen.dart';
import 'package:mobile/state/auth_provider.dart';
import 'package:mobile/state/booking_requests_provider.dart';
import 'package:mobile/state/profile_provider.dart';
import 'package:mobile/state/token_store.dart';

class InMemoryTokenStore implements TokenStore {
  final Map<String, String> _values = {};
  @override
  Future<String?> read(String key) async => _values[key];
  @override
  Future<void> write(String key, String value) async => _values[key] = value;
  @override
  Future<void> delete(String key) async => _values.remove(key);
}

void main() {
  testWidgets('Profile reaches the seeded booking history and cost preview',
      (tester) async {
    final mockClient = MockClient((request) async {
      if (request.url.path == '/api/student-profiles/me') {
        return http.Response(
          jsonEncode({
            'id': 'profile-1',
            'userId': '11111111-1111-1111-1111-111111111111',
            'studentNumber': 'S12345',
            'department': 'Computing',
            'yearOfStudy': 2,
            'maxBookingsPerWeek': 3,
            'penaltyPoints': 0,
            'suspendedUntil': null,
            'isActive': true,
          }),
          200,
          headers: {'content-type': 'application/json'},
        );
      }
      throw Exception(
          'unexpected request: ${request.method} ${request.url.path}');
    });

    final authProvider = AuthProvider(
        apiClient: ApiClient(client: mockClient),
        tokenStore: InMemoryTokenStore());

    await tester.pumpWidget(
      MultiProvider(
        providers: [
          ChangeNotifierProvider.value(value: authProvider),
          ChangeNotifierProvider(
              create: (_) =>
                  ProfileProvider(StudentProfilesApi(authProvider.apiClient))),
          ChangeNotifierProvider(
              create: (_) => BookingRequestsProvider(
                  BookingRequestsApi(authProvider.apiClient))),
        ],
        child: const MaterialApp(home: Scaffold(body: ProfileScreen())),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.text('S12345'), findsOneWidget);

    await tester.tap(find.text('See past bookings and costs'));
    await tester.pumpAndSettle();

    expect(find.byType(BookingHistoryScreen), findsOneWidget);
    expect(find.text('Group study'), findsOneWidget);

    await tester.pageBack();
    await tester.pumpAndSettle();
    await tester.scrollUntilVisible(find.text('Browse consumables'), 240);
    await tester.tap(find.text('Browse consumables'));
    await tester.pumpAndSettle();

    expect(find.byType(BrowseConsumablesScreen), findsOneWidget);
  });
}
