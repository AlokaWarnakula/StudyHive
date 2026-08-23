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

const _profileJson = {
  'id': 'profile-1',
  'userId': '11111111-1111-1111-1111-111111111111',
  'studentNumber': 'S12345',
  'department': 'Computing',
  'yearOfStudy': 2,
  'maxBookingsPerWeek': 3,
  'penaltyPoints': 0,
  'suspendedUntil': null,
  'isActive': true,
};

const _bookingJson = {
  'id': 'req-1',
  'studentId': 'profile-1',
  'objective': 'Group study session',
  'groupSize': 4,
  'preferredDateFrom': '2026-09-01',
  'preferredDateTo': '2026-09-01',
  'preferredTimeFrom': '09:00:00',
  'preferredTimeTo': '11:00:00',
  'sessionsRequired': 1,
  'sessionDurationMinutes': 60,
  'budget': 50.0,
  'notes': null,
  'status': 'Draft',
  'items': [],
  'latestWorkflowId': null,
  'createdAt': '2026-08-22T00:00:00Z',
  'updatedAt': '2026-08-22T00:00:00Z',
};

http.Response _json(Object body, [int status = 200]) =>
    http.Response(jsonEncode(body), status,
        headers: {'content-type': 'application/json'});

Future<AuthProvider> _signedInAuthProvider(MockClient mockClient) async {
  final authProvider = AuthProvider(
      apiClient: ApiClient(client: mockClient),
      tokenStore: InMemoryTokenStore());
  await authProvider.login('student@studyhive.test', 'correct-password');
  return authProvider;
}

Future<void> _pumpApp(WidgetTester tester, AuthProvider authProvider) async {
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
  await tester.pumpAndSettle();
}

void main() {
  testWidgets(
      'a student without a profile sees the onboarding form and can save it',
      (tester) async {
    var profileCreated = false;
    final mockClient = MockClient((request) async {
      if (request.url.path == '/api/auth/login') {
        return _json({
          'accessToken': 'access-token',
          'accessTokenExpiresAt': '2026-01-01T00:00:00Z',
          'refreshToken': 'refresh-token',
          'refreshTokenExpiresAt': '2026-02-01T00:00:00Z',
          'user': {
            'id': '11111111-1111-1111-1111-111111111111',
            'email': 'student@studyhive.test',
            'fullName': 'Test Student',
            'role': 'Student',
            'isActive': true,
            'createdAt': '2026-01-01T00:00:00Z',
          },
        });
      }
      if (request.url.path == '/api/student-profiles/me' &&
          request.method == 'GET') {
        return _json({'title': 'Not Found'}, 404);
      }
      if (request.url.path == '/api/student-profiles' &&
          request.method == 'POST') {
        profileCreated = true;
        return _json(_profileJson, 201);
      }
      fail('unexpected request: ${request.method} ${request.url.path}');
    });

    final auth = await _signedInAuthProvider(mockClient);
    await _pumpApp(tester, auth);

    await tapAndSettle(tester, find.text('Profile'));

    expect(find.text('Finish setting up your student profile'), findsOneWidget);

    await tester.enterText(field('Student number'), 'S12345');
    await tester.enterText(field('Department'), 'Computing');
    await tester.enterText(field('Year of study'), '2');
    await tapAndSettle(
        tester, find.widgetWithText(FilledButton, 'Save profile'));

    expect(profileCreated, isTrue);
    expect(find.text('S12345'), findsOneWidget);
    expect(find.text('Active'), findsOneWidget);
  });

  testWidgets(
      'creating and submitting a request shows a confirmation and it appears in Track',
      (tester) async {
    var submitted = false;
    final mockClient = MockClient((request) async {
      if (request.url.path == '/api/auth/login') {
        return _json({
          'accessToken': 'access-token',
          'accessTokenExpiresAt': '2026-01-01T00:00:00Z',
          'refreshToken': 'refresh-token',
          'refreshTokenExpiresAt': '2026-02-01T00:00:00Z',
          'user': {
            'id': '11111111-1111-1111-1111-111111111111',
            'email': 'student@studyhive.test',
            'fullName': 'Test Student',
            'role': 'Student',
            'isActive': true,
            'createdAt': '2026-01-01T00:00:00Z',
          },
        });
      }
      if (request.url.path == '/api/booking-requests' &&
          request.method == 'POST') {
        return _json(_bookingJson, 201);
      }
      if (request.url.path == '/api/booking-requests/req-1/submit') {
        submitted = true;
        return _json({'workflowId': 'wf-1'}, 202);
      }
      if (request.url.path == '/api/booking-requests' &&
          request.method == 'GET') {
        return _json({
          'items': submitted ? [_bookingJson] : [],
          'page': 1,
          'pageSize': 100,
          'totalItems': submitted ? 1 : 0,
          'totalPages': 1,
        });
      }
      fail('unexpected request: ${request.method} ${request.url.path}');
    });

    final auth = await _signedInAuthProvider(mockClient);
    await _pumpApp(tester, auth);

    await tapAndSettle(tester, find.text('Book a room').first);

    await tester.enterText(
        field('What do you need the room for?'), 'Group study session');
    await tester.enterText(field('Budget (Rs.)'), '50');
    await tapAndSettle(
        tester, find.widgetWithText(FilledButton, 'Next: add items'));
    await tapAndSettle(
        tester, find.widgetWithText(FilledButton, 'Next: review'));
    await tapAndSettle(
        tester, find.widgetWithText(FilledButton, 'Send request'));

    expect(submitted, isTrue);
    expect(find.text('Working on it'), findsOneWidget);

    await tapAndSettle(
        tester, find.widgetWithText(OutlinedButton, 'Back to home'));
    await tapAndSettle(tester, find.text('Bookings'));
    await tapAndSettle(tester, find.text('Waiting'));

    expect(find.text('Group study session'), findsOneWidget);
  });
}
