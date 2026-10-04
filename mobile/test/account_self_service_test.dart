import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/booking_requests_api.dart';
import 'package:mobile/api/student_profiles_api.dart';
import 'package:mobile/models/booking_request.dart';
import 'package:mobile/screens/change_password_screen.dart';
import 'package:mobile/screens/edit_profile_screen.dart';
import 'package:mobile/screens/profile_screen.dart';
import 'package:mobile/screens/quotation/booking_history_screen.dart';
import 'package:mobile/screens/quotation/quotation_view_screen.dart';
import 'package:mobile/state/auth_provider.dart';
import 'package:mobile/state/booking_requests_provider.dart';
import 'package:mobile/state/profile_provider.dart';
import 'package:mobile/state/token_store.dart';
import 'package:mobile/widgets/studyhive_ui.dart';

import 'support/consumables.dart';
import 'support/finders.dart';

/// PLAN.md 3.2: Edit profile, Change password, and the payment state of approved bookings.

class _MemoryTokenStore implements TokenStore {
  final Map<String, String> values = {};
  @override
  Future<String?> read(String key) async => values[key];
  @override
  Future<void> write(String key, String value) async => values[key] = value;
  @override
  Future<void> delete(String key) async => values.remove(key);
}

http.Response _json(Object body, [int status = 200]) => http.Response(
      jsonEncode(body),
      status,
      headers: {
        'content-type':
            status >= 400 ? 'application/problem+json' : 'application/json',
      },
    );

Map<String, dynamic> _profile({
  String fullName = 'Asha Perera',
  String studentNumber = 'REG-1234ABCD',
  String department = 'Computing',
  int yearOfStudy = 2,
}) =>
    {
      'id': 'profile-1',
      'userId': 'user-1',
      'fullName': fullName,
      'email': 'asha@studyhive.test',
      'studentNumber': studentNumber,
      'department': department,
      'yearOfStudy': yearOfStudy,
      'maxBookingsPerWeek': 3,
      'penaltyPoints': 0,
      'suspendedUntil': null,
      'isActive': true,
    };

Map<String, dynamic> _quote({String? paidAt, String? reference}) => {
      'id': 'q-1',
      'status': 'Approved',
      'version': 1,
      'totalAmount': 450,
      'currency': 'LKR',
      'budgetSnapshot': 1000,
      'withinBudget': true,
      'paidAt': paidAt,
      'paymentReference': reference,
    };

Map<String, dynamic> _request(String id, String objective, Map<String, dynamic> quote) => {
      'id': id,
      'studentId': 'profile-1',
      'objective': objective,
      'groupSize': 3,
      'preferredDateFrom': '2026-10-13',
      'preferredDateTo': '2026-10-13',
      'preferredTimeFrom': '10:00:00',
      'preferredTimeTo': '12:00:00',
      'sessionsRequired': 1,
      'sessionDurationMinutes': 120,
      'budget': 1000,
      'notes': null,
      'status': 'Approved',
      'items': [],
      'latestWorkflowId': null,
      'latestQuotation': quote,
      'latestDecision': null,
      'createdAt': '2026-10-04T08:00:00Z',
      'updatedAt': '2026-10-04T08:00:00Z',
    };

Future<(AuthProvider, ProfileProvider)> _pump(
  WidgetTester tester,
  MockClient client,
  Widget home, {
  _MemoryTokenStore? tokens,
}) async {
  final auth = AuthProvider(
    apiClient: ApiClient(client: client),
    tokenStore: tokens ?? _MemoryTokenStore(),
  );
  final profile = ProfileProvider(StudentProfilesApi(auth.apiClient));
  await profile.refresh();
  await tester.pumpWidget(
    MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: auth),
        ChangeNotifierProvider.value(value: profile),
        ChangeNotifierProvider(
          create: (_) => BookingRequestsProvider(BookingRequestsApi(auth.apiClient)),
        ),
        ChangeNotifierProvider(create: (_) => consumablesProviderFor()),
      ],
      child: MaterialApp(home: home),
    ),
  );
  await tester.pumpAndSettle();
  return (auth, profile);
}

final _submit = find.widgetWithText(PrimaryButton, 'Change password');

void main() {
  group('Edit profile', () {
    testWidgets('Profile has Edit profile and Change password, not the desk note',
        (tester) async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/student-profiles/me') return _json(_profile());
        if (request.url.path == '/api/booking-requests') {
          return _json({'items': [], 'page': 1, 'pageSize': 100, 'totalItems': 0, 'totalPages': 0});
        }
        return _json({'title': 'Not Found', 'status': 404}, 404);
      });
      await _pump(tester, client, const Scaffold(body: ProfileScreen()));

      await tester.scrollUntilVisible(find.text('Change password'), 240);
      expect(find.text('Edit profile'), findsOneWidget);
      expect(find.text('Change password'), findsOneWidget);
      expect(find.textContaining('To change your password'), findsNothing);

      await tester.tap(find.text('Edit profile'));
      await tester.pumpAndSettle();
      expect(find.byType(EditProfileScreen), findsOneWidget);
    });

    testWidgets('saves the edited fields with PUT /me and updates the profile',
        (tester) async {
      Map<String, dynamic>? sent;
      final client = MockClient((request) async {
        if (request.url.path == '/api/student-profiles/me' && request.method == 'PUT') {
          sent = jsonDecode(request.body) as Map<String, dynamic>;
          return _json(_profile(
            fullName: sent!['fullName'] as String,
            studentNumber: sent!['studentNumber'] as String,
            department: sent!['department'] as String,
            yearOfStudy: sent!['yearOfStudy'] as int,
          ));
        }
        if (request.url.path == '/api/student-profiles/me') return _json(_profile());
        return _json({'title': 'Not Found', 'status': 404}, 404);
      });
      final (auth, profile) = await _pump(tester, client, const EditProfileScreen());

      expect(find.widgetWithText(TextFormField, 'REG-1234ABCD'), findsOneWidget);
      await tester.enterText(find.widgetWithText(TextFormField, 'REG-1234ABCD'), 'CS/2024/017');
      await tester.enterText(find.widgetWithText(TextFormField, '2'), '3');
      await tester.tap(find.text('Save changes'));
      await tester.pumpAndSettle();

      expect(sent, {
        'fullName': 'Asha Perera',
        'studentNumber': 'CS/2024/017',
        'department': 'Computing',
        'yearOfStudy': 3,
      });
      expect(profile.profile!.studentNumber, 'CS/2024/017');
      expect(profile.profile!.yearOfStudy, 3);
      expect(auth.studentName, 'Asha Perera');
    });

    testWidgets('a 409 puts the error on the student number field', (tester) async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/student-profiles/me' && request.method == 'PUT') {
          return _json({
            'title': 'Student number already registered',
            'status': 409,
            'detail': 'This student number is already registered to another account.',
          }, 409);
        }
        if (request.url.path == '/api/student-profiles/me') return _json(_profile());
        return _json({'title': 'Not Found', 'status': 404}, 404);
      });
      await _pump(tester, client, const EditProfileScreen());

      await tester.tap(find.text('Save changes'));
      await tester.pumpAndSettle();

      expect(find.text('This student number is already registered'), findsOneWidget);
      expect(find.byType(EditProfileScreen), findsOneWidget);
    });

    testWidgets('validates year and blank fields before calling the API', (tester) async {
      var puts = 0;
      final client = MockClient((request) async {
        if (request.method == 'PUT') puts++;
        if (request.url.path == '/api/student-profiles/me') return _json(_profile());
        return _json({'title': 'Not Found', 'status': 404}, 404);
      });
      await _pump(tester, client, const EditProfileScreen());

      await tester.enterText(find.widgetWithText(TextFormField, 'Computing'), '  ');
      await tester.enterText(find.widgetWithText(TextFormField, '2'), '9');
      await tester.tap(find.text('Save changes'));
      await tester.pumpAndSettle();

      expect(find.text('Department is required'), findsOneWidget);
      expect(find.text('Enter a year between 1 and 5'), findsOneWidget);
      expect(puts, 0);
    });
  });

  group('Change password', () {
    testWidgets('mismatched or short passwords never reach the API', (tester) async {
      var calls = 0;
      final client = MockClient((request) async {
        if (request.url.path == '/api/auth/change-password') calls++;
        if (request.url.path == '/api/student-profiles/me') return _json(_profile());
        return _json({'title': 'Not Found', 'status': 404}, 404);
      });
      await _pump(tester, client, const ChangePasswordScreen());

      await tester.enterText(field('Current password'), 'old-password-1');
      await tester.enterText(field('New password'), 'short');
      await tester.enterText(field('Confirm new password'), 'different');
      await tapAndSettle(tester, _submit);

      expect(find.text('Use at least 8 characters'), findsOneWidget);
      expect(find.text('Passwords do not match'), findsOneWidget);
      expect(calls, 0);
    });

    testWidgets('success stores the fresh tokens', (tester) async {
      Map<String, dynamic>? sent;
      final client = MockClient((request) async {
        if (request.url.path == '/api/auth/change-password') {
          sent = jsonDecode(request.body) as Map<String, dynamic>;
          return _json({
            'accessToken': 'access-new',
            'accessTokenExpiresAt': '2026-10-05T10:00:00Z',
            'refreshToken': 'refresh-new',
            'refreshTokenExpiresAt': '2026-10-19T10:00:00Z',
            'user': {
              'id': 'user-1',
              'email': 'asha@studyhive.test',
              'fullName': 'Asha Perera',
              'role': 'Student',
              'isActive': true,
              'createdAt': '2026-10-01T00:00:00Z',
            },
          });
        }
        if (request.url.path == '/api/student-profiles/me') return _json(_profile());
        return _json({'title': 'Not Found', 'status': 404}, 404);
      });
      final tokens = _MemoryTokenStore();
      final (auth, _) = await _pump(tester, client, const ChangePasswordScreen(), tokens: tokens);

      await tester.enterText(field('Current password'), 'old-password-1');
      await tester.enterText(field('New password'), 'new-password-2');
      await tester.enterText(field('Confirm new password'), 'new-password-2');
      await tapAndSettle(tester, _submit);

      expect(sent, {'currentPassword': 'old-password-1', 'newPassword': 'new-password-2'});
      expect(tokens.values['refresh_token'], 'refresh-new');
      expect(tokens.values['access_token'], 'access-new');
      expect(auth.isAuthenticated, isTrue);
    });

    testWidgets('a wrong current password shows on that field', (tester) async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/auth/change-password') {
          return _json({
            'title': 'One or more validation errors occurred.',
            'status': 400,
            'errors': {
              'CurrentPassword': ['Current password is incorrect.'],
            },
          }, 400);
        }
        if (request.url.path == '/api/student-profiles/me') return _json(_profile());
        return _json({'title': 'Not Found', 'status': 404}, 404);
      });
      await _pump(tester, client, const ChangePasswordScreen());

      await tester.enterText(field('Current password'), 'wrong-password');
      await tester.enterText(field('New password'), 'new-password-2');
      await tester.enterText(field('Confirm new password'), 'new-password-2');
      await tapAndSettle(tester, _submit);

      expect(find.text('Current password is incorrect.'), findsOneWidget);
      expect(find.byType(ChangePasswordScreen), findsOneWidget);
    });
  });

  group('Payment status', () {
    test('paymentLine says Paid with date and receipt, or Unpaid with the total', () {
      expect(paymentLine(BookingQuotationSummary.fromJson(_quote())),
          'Unpaid — pay Rs. 450 at the library desk');
      expect(
          paymentLine(BookingQuotationSummary.fromJson(
              _quote(paidAt: '2026-10-06T04:00:00Z', reference: 'R-77'))),
          'Paid · Tue 6 Oct · receipt R-77');
      expect(
          paymentLine(BookingQuotationSummary.fromJson({..._quote(), 'status': 'Proposed'})),
          isNull);
      expect(paymentLine(null), isNull);
    });

    testWidgets('booking history shows both payment states', (tester) async {
      final client = MockClient((request) async {
        if (request.url.path == '/api/booking-requests') {
          return _json({
            'items': [
              _request('r-1', 'Paid session', _quote(paidAt: '2026-10-06T04:00:00Z', reference: 'R-77')),
              _request('r-2', 'Unpaid session', _quote()),
            ],
            'page': 1,
            'pageSize': 100,
            'totalItems': 2,
            'totalPages': 1,
          });
        }
        if (request.url.path == '/api/student-profiles/me') return _json(_profile());
        return _json({'title': 'Not Found', 'status': 404}, 404);
      });
      await _pump(tester, client, const BookingHistoryScreen(live: true));

      expect(find.text('Paid · Tue 6 Oct · receipt R-77'), findsOneWidget);
      expect(find.text('Unpaid — pay Rs. 450 at the library desk'), findsOneWidget);
    });
  });
}
