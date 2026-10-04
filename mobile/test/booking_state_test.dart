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
import 'package:mobile/screens/booking_detail_screen.dart';
import 'package:mobile/screens/create_request_screen.dart';
import 'package:mobile/screens/track_screen.dart';
import 'package:mobile/state/booking_requests_provider.dart';
import 'package:mobile/state/profile_provider.dart';

import 'support/finders.dart';

/// PLAN.md B2: booking state from roomBookings (C-06/C-14), cancel confirmation (C-07),
/// edit and resend (C-02), draft actions and retry (C-11), the prefilled form (C-09) and fresh
/// data (C-08).

http.Response _json(Object body, [int status = 200]) => http.Response(
    jsonEncode(body), status,
    headers: {'content-type': 'application/json'});

Map<String, dynamic> _request(String id, String status,
        {List<Map<String, dynamic>> roomBookings = const [],
        Map<String, dynamic>? decision,
        String objective = 'Database revision'}) =>
    {
      'id': id,
      'studentId': 'profile-1',
      'objective': objective,
      'groupSize': 4,
      'preferredDateFrom': '2030-10-06',
      'preferredDateTo': '2030-10-06',
      'preferredTimeFrom': '14:00:00',
      'preferredTimeTo': '16:00:00',
      'sessionsRequired': 1,
      'sessionDurationMinutes': 120,
      'budget': 1000.0,
      'notes': null,
      'status': status,
      'items': [],
      'latestWorkflowId': null,
      'latestDecision': decision,
      'roomBookings': roomBookings,
      'createdAt': '2030-10-01T04:00:00Z',
      'updatedAt': '2030-10-01T04:00:00Z',
    };

/// 14:00–16:00 Colombo on Sun 6 Oct 2030 is 08:30–10:30 UTC.
Map<String, dynamic> _booking(
        {String status = 'Confirmed',
        String startsAt = '2030-10-06T08:30:00Z',
        String endsAt = '2030-10-06T10:30:00Z',
        String? checkedInAt}) =>
    {
      'id': 'rb-1',
      'roomId': 'room-1',
      'roomName': 'Quiet Study 101',
      'startsAt': startsAt,
      'endsAt': endsAt,
      'status': status,
      'checkedInAt': checkedInAt,
    };

Map<String, dynamic> _page(List<Map<String, dynamic>> items) =>
    {'items': items, 'page': 1, 'pageSize': 100, 'totalItems': items.length, 'totalPages': 1};

class _Harness {
  final List<String> calls = [];
  final Map<String, dynamic>? Function(http.Request request)? handler;
  _Harness(this.handler);

  late final ApiClient client = ApiClient(
    client: MockClient((request) async {
      calls.add('${request.method} ${request.url.path}');
      final response = handler?.call(request);
      if (response == null) return _json({'title': 'Not Found'}, 404);
      return _json(response['body'] as Object, response['status'] as int? ?? 200);
    }),
  );

  Widget wrap(Widget child) => MultiProvider(
        providers: [
          ChangeNotifierProvider(
              create: (_) => BookingRequestsProvider(BookingRequestsApi(client))),
          ChangeNotifierProvider(
              create: (_) => ProfileProvider(StudentProfilesApi(client))),
        ],
        child: MaterialApp(home: child),
      );
}

void main() {
  testWidgets('a checked-in booking shows its room, slot and check-in time and no actions', (tester) async {
    final harness = _Harness((request) {
      if (request.url.path == '/api/booking-requests/req-1') {
        return {
          'body': _request('req-1', 'Approved',
              roomBookings: [_booking(checkedInAt: '2030-10-06T08:35:00Z')],
              decision: {'decision': 'Approved', 'comments': 'Enjoy', 'decidedAt': '2030-10-02T05:00:00Z'})
        };
      }
      return null;
    });
    await tester.pumpWidget(harness.wrap(const BookingDetailScreen(requestId: 'req-1')));
    await tester.pumpAndSettle();

    expect(find.text('Sun 6 Oct · 14:00–16:00'), findsOneWidget);
    expect(find.text('Quiet Study 101'), findsOneWidget);
    expect(find.text('Checked in at 14:05'), findsOneWidget);
    expect(find.text('Approved by librarian'), findsOneWidget);
    expect(find.text('Check in with QR'), findsNothing);
    expect(find.text('Cancel booking'), findsNothing);
  });

  testWidgets('cancelling asks first and keeping the booking sends nothing', (tester) async {
    final harness = _Harness((request) {
      if (request.url.path == '/api/booking-requests/req-1') {
        return {'body': _request('req-1', 'Approved', roomBookings: [_booking()])};
      }
      return null;
    });
    await tester.pumpWidget(harness.wrap(const BookingDetailScreen(requestId: 'req-1')));
    await tester.pumpAndSettle();

    expect(find.text('Check in with QR'), findsOneWidget);
    await tapAndSettle(tester, find.widgetWithText(OutlinedButton, 'Cancel booking'));
    expect(find.text('Cancel this booking?'), findsOneWidget);
    await tapAndSettle(tester, find.text('Keep booking'));

    expect(harness.calls.where((c) => c.startsWith('DELETE')), isEmpty);
  });

  testWidgets('ended bookings move to Past and a Completed one is there too', (tester) async {
    final harness = _Harness((request) {
      if (request.url.path == '/api/booking-requests' && request.method == 'GET') {
        return {
          'body': _page([
            _request('upcoming', 'Approved', objective: 'Upcoming session', roomBookings: [_booking()]),
            _request('ended', 'Approved', objective: 'Ended session', roomBookings: [
              _booking(status: 'NoShow', startsAt: '2020-01-01T08:30:00Z', endsAt: '2020-01-01T10:30:00Z'),
            ]),
            _request('done', 'Completed', objective: 'Completed session'),
          ])
        };
      }
      return null;
    });
    await tester.pumpWidget(harness.wrap(const Scaffold(body: TrackScreen())));
    await tester.pumpAndSettle();

    expect(find.text('Upcoming session'), findsOneWidget);
    expect(find.text('Sun 6 Oct · 14:00–16:00 · Quiet Study 101'), findsOneWidget);
    expect(find.text('Ended session'), findsNothing);

    await tapAndSettle(tester, find.text('Past'));
    expect(find.text('Ended session'), findsOneWidget);
    expect(find.text('Completed session'), findsOneWidget);
    expect(find.text('Upcoming session'), findsNothing);
  });

  testWidgets('a revision shows the comment and is edited and sent again with PUT then submit', (tester) async {
    useReferenceFrame(tester);
    Map<String, dynamic>? putBody;
    final harness = _Harness((request) {
      final path = request.url.path;
      if (path == '/api/booking-requests/req-9' && request.method == 'GET') {
        return {
          'body': _request('req-9', 'RevisionRequested', objective: 'Old purpose', decision: {
            'decision': 'RevisionRequested',
            'comments': 'Please make it a smaller group.',
            'decidedAt': '2030-10-02T05:00:00Z',
          })
        };
      }
      if (path == '/api/booking-requests/req-9' && request.method == 'PUT') {
        putBody = jsonDecode(request.body) as Map<String, dynamic>;
        return {'body': _request('req-9', 'Draft')};
      }
      if (path == '/api/booking-requests/req-9/submit') {
        return {'body': {'workflowId': 'wf-9'}, 'status': 202};
      }
      if (path == '/api/booking-requests' && request.method == 'GET') return {'body': _page([])};
      if (path == '/api/booking-requests/req-9/status') {
        return {'body': {'workflowId': 'wf-9', 'bookingRequestId': 'req-9', 'status': 'PendingApproval', 'steps': []}};
      }
      return null;
    });
    await tester.pumpWidget(harness.wrap(const BookingDetailScreen(requestId: 'req-9')));
    await tester.pumpAndSettle();

    expect(find.text('Please make it a smaller group.'), findsOneWidget);
    await tapAndSettle(tester, find.widgetWithText(FilledButton, 'Edit and resend'));
    expect(find.text('Edit request'), findsOneWidget);
    expect(find.text('Old purpose'), findsOneWidget, reason: 'the form opens pre-filled');

    await tester.enterText(field('What do you need the room for?'), 'New purpose');
    await tapAndSettle(tester, find.widgetWithText(FilledButton, 'Next: add items'));
    await tapAndSettle(tester, find.widgetWithText(FilledButton, 'Next: review'));
    await tapAndSettle(tester, find.widgetWithText(FilledButton, 'Send again'));

    expect(putBody!['objective'], 'New purpose');
    expect(harness.calls, containsAllInOrder(['PUT /api/booking-requests/req-9', 'POST /api/booking-requests/req-9/submit']));
    expect(harness.calls.where((c) => c == 'POST /api/booking-requests'), isEmpty, reason: 'no new request is created');
  });

  testWidgets('a Draft tile can be sent straight from My bookings', (tester) async {
    var submitted = false;
    final harness = _Harness((request) {
      final path = request.url.path;
      if (path == '/api/booking-requests' && request.method == 'GET') {
        return {'body': _page([_request('draft-1', submitted ? 'Submitted' : 'Draft', objective: 'Forgotten draft')])};
      }
      if (path == '/api/booking-requests/draft-1/submit') {
        submitted = true;
        return {'body': {'workflowId': 'wf-1'}, 'status': 202};
      }
      if (path == '/api/booking-requests/draft-1/status') {
        return {'body': {'workflowId': 'wf-1', 'bookingRequestId': 'draft-1', 'status': 'PendingApproval', 'steps': []}};
      }
      return null;
    });
    await tester.pumpWidget(harness.wrap(const Scaffold(body: TrackScreen())));
    await tester.pumpAndSettle();
    await tapAndSettle(tester, find.text('Waiting'));

    expect(find.text('Forgotten draft'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Delete'), findsOneWidget);
    await tapAndSettle(tester, find.widgetWithText(FilledButton, 'Send'));

    expect(submitted, isTrue);
  });

  testWidgets('the form starts on the chosen slot and room, and a failed send retries the same draft', (tester) async {
    useReferenceFrame(tester);
    var submitAttempts = 0;
    Map<String, dynamic>? createBody;
    final harness = _Harness((request) {
      final path = request.url.path;
      if (path == '/api/booking-requests' && request.method == 'POST') {
        createBody = jsonDecode(request.body) as Map<String, dynamic>;
        return {'body': _request('new-1', 'Draft'), 'status': 201};
      }
      if (path == '/api/booking-requests/new-1' && request.method == 'PUT') {
        return {'body': _request('new-1', 'Draft')};
      }
      if (path == '/api/booking-requests/new-1/submit') {
        submitAttempts++;
        return submitAttempts == 1
            ? {'body': {'title': 'Weekly booking limit reached (3 per week).'}, 'status': 422}
            : {'body': {'workflowId': 'wf-1'}, 'status': 202};
      }
      if (path == '/api/booking-requests' && request.method == 'GET') return {'body': _page([])};
      if (path == '/api/booking-requests/new-1/status') {
        return {'body': {'workflowId': 'wf-1', 'bookingRequestId': 'new-1', 'status': 'PendingApproval', 'steps': []}};
      }
      return null;
    });
    await tester.pumpWidget(harness.wrap(CreateRequestScreen(
      initialDate: DateTime(2030, 10, 7),
      initialFrom: const TimeOfDay(hour: 10, minute: 0),
      initialTo: const TimeOfDay(hour: 12, minute: 0),
      roomName: 'Quiet Study 101',
    )));
    await tester.pumpAndSettle();

    expect(find.text('Mon, 7 Oct 2030'), findsOneWidget);
    expect(find.text('Quiet Study 101'), findsOneWidget);
    expect(find.text('The librarian confirms the final room.'), findsOneWidget);

    await tester.enterText(field('What do you need the room for?'), 'Group study');
    await tapAndSettle(tester, find.widgetWithText(FilledButton, 'Next: add items'));
    await tapAndSettle(tester, find.widgetWithText(FilledButton, 'Next: review'));
    await tapAndSettle(tester, find.widgetWithText(FilledButton, 'Send request'));
    expect(find.text('Weekly booking limit reached (3 per week).'), findsOneWidget);

    await tapAndSettle(tester, find.widgetWithText(FilledButton, 'Send request'));

    expect(createBody!['preferredTimeFrom'], '10:00:00');
    expect(createBody!['notes'], 'Preferred room: Quiet Study 101');
    expect(harness.calls.where((c) => c == 'POST /api/booking-requests'), hasLength(1), reason: 'one draft, retried');
    expect(harness.calls, contains('PUT /api/booking-requests/new-1'));
    expect(submitAttempts, 2);
  });

  test('a booking that has started can no longer be cancelled; one in the future can', () {
    final future = BookingRequest.fromJson(_request('a', 'Approved', roomBookings: [_booking()]));
    final started = BookingRequest.fromJson(_request('b', 'Approved', roomBookings: [
      _booking(startsAt: '2020-01-01T08:30:00Z', endsAt: '2099-01-01T10:30:00Z'),
    ]));
    expect(future.canCancel, isTrue);
    expect(started.canCancel, isFalse);
    expect(started.canCheckIn, isTrue);
  });
}
