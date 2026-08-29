/// S4 (Costing, Validation, Approval & Audit) view models — the mobile half of the contract in
/// web/src/api/approvals.ts, over the locked schema
/// (api/src/StudyHive.Api/Data/Entities/S4/*.cs).
///
/// Paired with lib/api/quotations_api.dart and lib/state/quotations_provider.dart, which exist as
/// scaffolds: the calls are written but the endpoints behind them return 501 until S4 implements
/// api/src/StudyHive.Api/Controllers/Approvals/.
class QuotationLineItemView {
  final String itemName;
  final double quantity;
  final double unitPrice;
  final double lineTotal;

  const QuotationLineItemView({
    required this.itemName,
    required this.quantity,
    required this.unitPrice,
    required this.lineTotal,
  });
}

class QuotationView {
  final String bookingRequestId;
  final double roomFee;
  final double consumableCost;
  final double totalAmount;
  final double budgetSnapshot;
  final bool withinBudget;
  final String status;
  final List<QuotationLineItemView> lineItems;

  const QuotationView({
    required this.bookingRequestId,
    required this.roomFee,
    required this.consumableCost,
    required this.totalAmount,
    required this.budgetSnapshot,
    required this.withinBudget,
    required this.status,
    required this.lineItems,
  });
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
