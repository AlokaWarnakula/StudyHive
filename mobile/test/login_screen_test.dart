import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/booking_requests_api.dart';
import 'package:mobile/api/student_profiles_api.dart';
import 'package:mobile/app.dart';
import 'package:mobile/state/auth_provider.dart';
import 'package:mobile/state/booking_requests_provider.dart';
import 'package:mobile/state/profile_provider.dart';
import 'package:mobile/state/token_store.dart';

import 'support/finders.dart';

class InMemoryTokenStore implements TokenStore {
  final Map<String, String> _values = {};

  @override
  Future<String?> read(String key) async => _values[key];

  @override
  Future<void> write(String key, String value) async => _values[key] = value;

  @override
  Future<void> delete(String key) async => _values.remove(key);
}

Map<String, dynamic> _tokenResponse(String role) => {
      'accessToken': 'access-token',
      'accessTokenExpiresAt': '2026-01-01T00:00:00Z',
      'refreshToken': 'refresh-token',
      'refreshTokenExpiresAt': '2026-02-01T00:00:00Z',
      'user': {
        'id': '11111111-1111-1111-1111-111111111111',
        'email': 'test@studyhive.test',
        'fullName': 'Test User',
        'role': role,
        'isActive': true,
        'createdAt': '2026-01-01T00:00:00Z',
      },
    };

Future<void> _pumpApp(WidgetTester tester, MockClient mockClient) async {
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
      child: const StudyHiveApp(),
    ),
  );
}

Future<void> _fillAndSubmit(WidgetTester tester) async {
  await tester.enterText(field('University email'), 'student@studyhive.test');
  await tester.enterText(field('Password'), 'correct-password');
  await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('a Student account signs in and reaches the home screen',
      (tester) async {
    final mockClient = MockClient((request) async {
      expect(request.url.path, '/api/auth/login');
      return http.Response(jsonEncode(_tokenResponse('Student')), 200,
          headers: {'content-type': 'application/json'});
    });

    await _pumpApp(tester, mockClient);
    await _fillAndSubmit(tester);

    expect(find.text('Book a study room in a few taps.'), findsNothing);
    expect(find.text('Home'), findsOneWidget);
    expect(find.text('Rooms'), findsOneWidget);
    expect(find.text('Bookings'), findsOneWidget);
  });

  testWidgets('a staff account is rejected with a clear message',
      (tester) async {
    final mockClient = MockClient((request) async {
      return http.Response(jsonEncode(_tokenResponse('Librarian')), 200,
          headers: {'content-type': 'application/json'});
    });

    await _pumpApp(tester, mockClient);
    await _fillAndSubmit(tester);

    expect(find.textContaining('staff dashboard'), findsOneWidget);
    expect(find.text('Book a study room in a few taps.'), findsOneWidget);
  });

  testWidgets('invalid credentials show the API error message', (tester) async {
    final mockClient = MockClient((request) async {
      return http.Response(
        jsonEncode({
          'title': 'Invalid credentials',
          'status': 401,
          'detail': 'The email or password is incorrect.'
        }),
        401,
        headers: {'content-type': 'application/json'},
      );
    });

    await _pumpApp(tester, mockClient);
    await _fillAndSubmit(tester);

    expect(find.text('The email or password is incorrect.'), findsOneWidget);
    expect(find.text('Book a study room in a few taps.'), findsOneWidget);
  });

  testWidgets('empty fields fail local validation without calling the API',
      (tester) async {
    final mockClient = MockClient((request) async {
      fail('the API must not be called when client-side validation fails');
    });

    await _pumpApp(tester, mockClient);
    await tester.tap(find.widgetWithText(FilledButton, 'Sign in'));
    await tester.pumpAndSettle();

    expect(find.text('University email is required'), findsOneWidget);
    expect(find.text('Password is required'), findsOneWidget);
  });
}
