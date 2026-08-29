import '../models/quotation.dart';
import 'api_client.dart';

/// S4 (Costing, Validation, Approval & Audit) — student-facing quotation API. SCAFFOLD.
///
/// Written and typed against the locked schema (`api/src/StudyHive.Api/Data/Entities/S4/`), but
/// the endpoints return 501 until S4 implements
/// `api/src/StudyHive.Api/Controllers/Approvals/QuotationsController.cs`. Nothing here invents
/// data.
///
/// Screens this backs: M-08 Your quotation, plus the approval-status and booking-history views
/// reached from it.
///
/// Read-only on purpose. A student can look at their quotation and cancel the request behind it;
/// nothing else on M-08 is actionable, because the approve/reject decision belongs to a librarian
/// on W-04. Do not add a student-facing approval call here.
class QuotationsApi {
  final ApiClient _client;
  const QuotationsApi(this._client);

  /// TODO(S4): GET /api/quotations/{id} — backs M-08.
  ///
  /// A student may read their own; the ownership check belongs on the server, not here.
  Future<QuotationView> getById(String id) async {
    final json = await _client.get('/api/quotations/$id') as Map<String, dynamic>;
    final lineItems = (json['lineItems'] as List<dynamic>? ?? const [])
        .map((e) => QuotationLineItemView(
              itemName: (e as Map<String, dynamic>)['itemName'] as String,
              quantity: (e['quantity'] as num).toDouble(),
              unitPrice: (e['unitPrice'] as num).toDouble(),
              lineTotal: (e['lineTotal'] as num).toDouble(),
            ))
        .toList();

    final total = (json['totalAmount'] as num).toDouble();
    final budget = (json['budgetSnapshot'] as num).toDouble();

    return QuotationView(
      bookingRequestId: json['bookingRequestId'] as String,
      roomFee: (json['roomFee'] as num).toDouble(),
      consumableCost: (json['consumableCost'] as num).toDouble(),
      totalAmount: total,
      budgetSnapshot: budget,
      // Derived rather than trusted from the wire: the screen must never disagree with its own
      // numbers. If the server sends a contradicting flag, the numbers win.
      withinBudget: total <= budget,
      status: json['status'] as String,
      lineItems: lineItems,
    );
  }

  /// TODO(S4): GET /api/booking-requests?status=Completed — backs the booking history view.
  ///
  /// Reads S1's endpoint, which already exists; what is missing is the cost, which only becomes
  /// real once quotations do.
  Future<List<BookingHistoryItem>> history() async {
    final response = await _client
        .get('/api/booking-requests?status=Completed&pageSize=100') as Map<String, dynamic>;
    final items = response['items'] as List<dynamic>;
    return items
        .map((e) => BookingHistoryItem(
              bookingRequestId: (e as Map<String, dynamic>)['id'] as String,
              objective: e['objective'] as String,
              totalCost: (e['totalCost'] as num?)?.toDouble() ?? 0,
              status: e['status'] as String,
              completedAt: e['updatedAt'] as String,
            ))
        .toList();
  }
}
