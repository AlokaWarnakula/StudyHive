import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/booking_requests_api.dart';
import 'package:mobile/screens/quotation/approval_status_screen.dart';
import 'package:mobile/screens/quotation/booking_history_screen.dart';
import 'package:mobile/screens/quotation/quotation_view_screen.dart';
import 'package:mobile/state/booking_requests_provider.dart';
import 'package:mobile/widgets/studyhive_ui.dart';

/// A BookingRequestResponse as the API sends it to its owning student.
Map<String, dynamic> requestJson({
  String id = 'r-1',
  String objective = 'Networks exam revision',
  String status = 'PendingApproval',
  Map<String, dynamic>? latestQuotation,
  Map<String, dynamic>? latestDecision,
}) => {
  'id': id,
  'studentId': 's-1',
  'objective': objective,
  'groupSize': 4,
  'preferredDateFrom': '2026-10-05',
  'preferredDateTo': '2026-10-05',
  'preferredTimeFrom': '09:00:00',
  'preferredTimeTo': '12:00:00',
  'sessionsRequired': 1,
  'sessionDurationMinutes': 120,
  'budget': 500,
  'notes': null,
  'status': status,
  'items': [],
  'latestWorkflowId': 'w-1',
  'latestQuotation': latestQuotation,
  'latestDecision': latestDecision,
  'createdAt': '2026-09-30T03:00:00Z',
  'updatedAt': '2026-09-30T03:00:00Z',
};

Map<String, dynamic> quotationSummary({
  String id = 'q-1',
  String status = 'Proposed',
  double total = 120,
}) => {
  'id': id,
  'status': status,
  'version': 1,
  'totalAmount': total,
  'currency': 'LKR',
  'budgetSnapshot': 500,
  'withinBudget': true,
};

Map<String, dynamic> workflowJson({
  String status = 'PendingApproval',
  String? errorCode,
  String? errorMessage,
}) => {
  'workflowId': 'w-1',
  'bookingRequestId': 'r-1',
  'status': status,
  'errorCode': errorCode,
  'errorMessage': errorMessage,
  'steps': [],
};

http.Response json(Object body, [int status = 200]) => http.Response(
  jsonEncode(body),
  status,
  headers: {'content-type': 'application/json'},
);

/// Hosts [screen] with a BookingRequestsProvider whose API answers from [routes] (path → body).
Widget host(Widget screen, Map<String, Object> routes, {List<String>? calls}) {
  final client = MockClient((request) async {
    calls?.add(request.url.path);
    final body = routes[request.url.path];
    if (body == null) return json({'title': 'Not found'}, 404);
    return json(body);
  });
  return ChangeNotifierProvider(
    create:
        (_) => BookingRequestsProvider(
          BookingRequestsApi(ApiClient(client: client)),
        ),
    child: MaterialApp(home: screen),
  );
}

void main() {
  testWidgets(
    'M-08 loads the quotation named by the request and shows the live cost breakdown',
    (tester) async {
      final calls = <String>[];
      await tester.pumpWidget(
        host(const QuotationViewScreen(requestId: 'r-1'), {
          '/api/booking-requests/r-1': requestJson(
            latestQuotation: quotationSummary(),
          ),
          '/api/quotations/q-1': {
            'id': 'q-1',
            'bookingRequestId': 'r-1',
            'version': 1,
            'roomFee': 0,
            'consumableCost': 120,
            'totalAmount': 120,
            'budgetSnapshot': 500,
            'withinBudget': true,
            'currency': 'LKR',
            'status': 'Proposed',
            'lineItems': [
              {
                'itemType': 'Room',
                'itemName': 'Quiet Study 101',
                'quantity': 2,
                'unitPrice': 0,
                'lineTotal': 0,
              },
              {
                'itemType': 'Consumable',
                'itemName': 'Whiteboard markers',
                'quantity': 2,
                'unitPrice': 60,
                'lineTotal': 120,
              },
            ],
          },
        }, calls: calls),
      );
      await tester.pumpAndSettle();

      // The quotation id came from the student's own request, not a guess.
      expect(calls, ['/api/booking-requests/r-1', '/api/quotations/q-1']);
      expect(find.text('Waiting for librarian'), findsOneWidget);
      expect(find.text('Quiet Study 101 · 2 h'), findsOneWidget);
      expect(find.text('Whiteboard markers · × 2'), findsOneWidget);
      expect(find.text('Rs. 120'), findsWidgets);
      expect(find.text('Within budget by'), findsOneWidget);
      expect(find.text('Rs. 380'), findsOneWidget);
      // Live data, not the seeded preview.
      expect(find.byType(DemoPreviewBanner), findsNothing);
    },
  );

  testWidgets('M-08 says there is no quotation yet instead of guessing one', (
    tester,
  ) async {
    await tester.pumpWidget(
      host(const QuotationViewScreen(requestId: 'r-1'), {
        '/api/booking-requests/r-1': requestJson(status: 'Processing'),
      }),
    );
    await tester.pumpAndSettle();

    expect(find.text('No quotation yet'), findsOneWidget);
  });

  testWidgets('approval status promises an email while the quotation awaits a decision', (
    tester,
  ) async {
    await tester.pumpWidget(
      host(const ApprovalStatusScreen(requestId: 'r-1'), {
        '/api/booking-requests/r-1': requestJson(
          latestQuotation: quotationSummary(),
        ),
        '/api/booking-requests/r-1/status': workflowJson(),
      }),
    );
    await tester.pumpAndSettle();

    expect(
      find.text('You will get an email as soon as they decide.'),
      findsOneWidget,
    );
    expect(find.textContaining('notification'), findsNothing);
  });

  testWidgets('approval status shows the librarian decision and comment', (
    tester,
  ) async {
    await tester.pumpWidget(
      host(const ApprovalStatusScreen(requestId: 'r-1'), {
        '/api/booking-requests/r-1': requestJson(
          status: 'RevisionRequested',
          latestQuotation: quotationSummary(status: 'Rejected'),
          latestDecision: {
            'decision': 'RevisionRequested',
            'comments': 'Pick an afternoon slot',
            'decidedAt': '2026-09-30T05:00:00Z',
          },
        ),
        '/api/booking-requests/r-1/status': workflowJson(status: 'Rejected'),
      }),
    );
    await tester.pumpAndSettle();

    expect(find.text('Changes requested'), findsWidgets);
    expect(find.text("LIBRARIAN'S COMMENT"), findsOneWidget);
    expect(find.text('Pick an afternoon slot'), findsOneWidget);
    expect(find.text('View cost breakdown'), findsOneWidget);
  });

  testWidgets(
    'approval status shows the VALIDATION_FAILED revision note when no quotation was made',
    (tester) async {
      await tester.pumpWidget(
        host(const ApprovalStatusScreen(requestId: 'r-1'), {
          '/api/booking-requests/r-1': requestJson(status: 'Failed'),
          '/api/booking-requests/r-1/status': workflowJson(
            status: 'Failed',
            errorCode: 'VALIDATION_FAILED',
            errorMessage: 'Lower the budget or remove 2 markers.',
          ),
        }),
      );
      await tester.pumpAndSettle();

      expect(find.text('Your request needs changes'), findsOneWidget);
      expect(find.text('WHAT TO CHANGE'), findsOneWidget);
      expect(
        find.text('Lower the budget or remove 2 markers.'),
        findsOneWidget,
      );
      expect(find.text('View cost breakdown'), findsNothing);
    },
  );

  testWidgets(
    'booking history lists costed requests with approved spend and opens their quotation',
    (tester) async {
      await tester.pumpWidget(
        host(const BookingHistoryScreen(live: true), {
          '/api/booking-requests': {
            'items': [
              requestJson(
                objective: 'Networks exam revision',
                status: 'Approved',
                latestQuotation: quotationSummary(
                  status: 'Approved',
                  total: 120,
                ),
              ),
              requestJson(
                id: 'r-2',
                objective: 'Still being priced',
                status: 'Processing',
              ),
            ],
            'page': 1,
            'pageSize': 100,
            'totalItems': 2,
            'totalPages': 1,
          },
          '/api/booking-requests/r-1': requestJson(
            status: 'Approved',
            latestQuotation: quotationSummary(status: 'Approved'),
          ),
        }),
      );
      await tester.pumpAndSettle();

      expect(find.text('Approved spend'), findsOneWidget);
      expect(find.text('Networks exam revision'), findsOneWidget);
      expect(find.text('Still being priced'), findsNothing);

      await tester.tap(find.text('Networks exam revision'));
      await tester.pumpAndSettle();

      expect(find.byType(QuotationViewScreen), findsOneWidget);
    },
  );
}
