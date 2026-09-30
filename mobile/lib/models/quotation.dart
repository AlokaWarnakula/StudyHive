/// S4 (Costing, Validation, Approval & Audit) view models — the mobile half of the contract in
/// web/src/api/approvals.ts, over the locked schema
/// (api/src/StudyHive.Api/Data/Entities/S4/*.cs).
///
/// Live data comes from GET /api/quotations/{id} (a student may read their own), reached through
/// BookingRequest.latestQuotation.id — see [QuotationView.fromJson].
class QuotationLineItemView {
  /// Room or Consumable. A Room line's quantity is hours.
  final String itemType;
  final String itemName;
  final double quantity;
  final double unitPrice;
  final double lineTotal;

  const QuotationLineItemView({
    this.itemType = 'Consumable',
    required this.itemName,
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
  });
}

class QuotationView {
  final String? id;
  final int version;
  final String currency;
  final String bookingRequestId;
  final double roomFee;
  final double consumableCost;
  final double totalAmount;
  final double budgetSnapshot;
  final bool withinBudget;
  final String status;
  final List<QuotationLineItemView> lineItems;

  const QuotationView({
    this.id,
    this.version = 1,
    this.currency = 'LKR',
    required this.bookingRequestId,
    required this.roomFee,
    required this.consumableCost,
    required this.totalAmount,
    required this.budgetSnapshot,
    required this.withinBudget,
    required this.status,
    required this.lineItems,
  });

  /// QuotationResponse from GET /api/quotations/{id}.
  factory QuotationView.fromJson(Map<String, dynamic> json) {
    final total = (json['totalAmount'] as num).toDouble();
    final budget = (json['budgetSnapshot'] as num).toDouble();
    return QuotationView(
      id: json['id'] as String?,
      version: json['version'] as int? ?? 1,
      currency: json['currency'] as String? ?? 'LKR',
      bookingRequestId: json['bookingRequestId'] as String,
      roomFee: (json['roomFee'] as num).toDouble(),
      consumableCost: (json['consumableCost'] as num).toDouble(),
      totalAmount: total,
      budgetSnapshot: budget,
      // Derived rather than trusted from the wire: the screen must never disagree with its own
      // numbers.
      withinBudget: total <= budget,
      status: json['status'] as String,
      lineItems:
          (json['lineItems'] as List<dynamic>? ?? const []).map((e) {
            final line = e as Map<String, dynamic>;
            return QuotationLineItemView(
              itemType: line['itemType'] as String? ?? 'Consumable',
              itemName: line['itemName'] as String,
              quantity: (line['quantity'] as num).toDouble(),
              unitPrice: (line['unitPrice'] as num).toDouble(),
              lineTotal: (line['lineTotal'] as num).toDouble(),
            );
          }).toList(),
    );
  }
}

class BookingHistoryItem {
  final String bookingRequestId;
  final String objective;
  final double totalCost;
  final String status;
  final String completedAt;

  const BookingHistoryItem({
    required this.bookingRequestId,
    required this.objective,
    required this.totalCost,
    required this.status,
    required this.completedAt,
  });
}
