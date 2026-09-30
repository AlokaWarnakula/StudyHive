class BookingRequestItem {
  final String consumableId;
  final int quantity;

  const BookingRequestItem({
    required this.consumableId,
    required this.quantity,
  });

  factory BookingRequestItem.fromJson(Map<String, dynamic> json) =>
      BookingRequestItem(
        consumableId: json['consumableId'] as String,
        quantity: json['quantity'] as int,
      );
}

/// The request's newest non-Draft quotation (S4), as BookingRequestResponse.latestQuotation carries
/// it. Its [id] is what GET /api/quotations/{id} takes; nothing else hands a student that id.
class BookingQuotationSummary {
  final String id;
  final String status;
  final int version;
  final double totalAmount;
  final String currency;
  final double budgetSnapshot;
  final bool withinBudget;

  const BookingQuotationSummary({
    required this.id,
    required this.status,
    required this.version,
    required this.totalAmount,
    required this.currency,
    required this.budgetSnapshot,
    required this.withinBudget,
  });

  factory BookingQuotationSummary.fromJson(Map<String, dynamic> json) =>
      BookingQuotationSummary(
        id: json['id'] as String,
        status: json['status'] as String,
        version: json['version'] as int,
        totalAmount: (json['totalAmount'] as num).toDouble(),
        currency: json['currency'] as String? ?? 'LKR',
        budgetSnapshot: (json['budgetSnapshot'] as num).toDouble(),
        withinBudget: json['withinBudget'] as bool,
      );
}

/// The librarian's decision on [BookingRequest.latestQuotation] (S4): outcome, comments and when.
/// Null while that quotation is undecided; an older version's decision is never carried over.
class BookingDecisionSummary {
  final String decision;
  final String? comments;
  final String decidedAt;

  const BookingDecisionSummary({
    required this.decision,
    required this.comments,
    required this.decidedAt,
  });

  factory BookingDecisionSummary.fromJson(Map<String, dynamic> json) =>
      BookingDecisionSummary(
        decision: json['decision'] as String,
        comments: json['comments'] as String?,
        decidedAt: json['decidedAt'] as String,
      );
}

/// Mirrors StudyHive.Api's BookingRequestResponse (see
/// api/src/StudyHive.Api/Controllers/BookingRequests/BookingRequestContracts.cs).
class BookingRequest {
  final String id;
  final String studentId;
  final String objective;
  final int groupSize;
  final String preferredDateFrom;
  final String preferredDateTo;
  final String preferredTimeFrom;
  final String preferredTimeTo;
  final int sessionsRequired;
  final int sessionDurationMinutes;
  final double budget;
  final String? notes;
  final String status;
  final List<BookingRequestItem> items;
  final String? latestWorkflowId;
  final BookingQuotationSummary? latestQuotation;
  final BookingDecisionSummary? latestDecision;
  final String createdAt;
  final String updatedAt;

  const BookingRequest({
    required this.id,
    required this.studentId,
    required this.objective,
    required this.groupSize,
    required this.preferredDateFrom,
    required this.preferredDateTo,
    required this.preferredTimeFrom,
    required this.preferredTimeTo,
    required this.sessionsRequired,
    required this.sessionDurationMinutes,
    required this.budget,
    required this.notes,
    required this.status,
    required this.items,
    required this.latestWorkflowId,
    this.latestQuotation,
    this.latestDecision,
    required this.createdAt,
    required this.updatedAt,
  });

  factory BookingRequest.fromJson(Map<String, dynamic> json) => BookingRequest(
    id: json['id'] as String,
    studentId: json['studentId'] as String,
    objective: json['objective'] as String,
    groupSize: json['groupSize'] as int,
    preferredDateFrom: json['preferredDateFrom'] as String,
    preferredDateTo: json['preferredDateTo'] as String,
    preferredTimeFrom: json['preferredTimeFrom'] as String,
    preferredTimeTo: json['preferredTimeTo'] as String,
    sessionsRequired: json['sessionsRequired'] as int,
    sessionDurationMinutes: json['sessionDurationMinutes'] as int,
    budget: (json['budget'] as num).toDouble(),
    notes: json['notes'] as String?,
    status: json['status'] as String,
    items:
        (json['items'] as List<dynamic>? ?? [])
            .map((e) => BookingRequestItem.fromJson(e as Map<String, dynamic>))
            .toList(),
    latestWorkflowId: json['latestWorkflowId'] as String?,
    latestQuotation:
        json['latestQuotation'] == null
            ? null
            : BookingQuotationSummary.fromJson(
              json['latestQuotation'] as Map<String, dynamic>,
            ),
    latestDecision:
        json['latestDecision'] == null
            ? null
            : BookingDecisionSummary.fromJson(
              json['latestDecision'] as Map<String, dynamic>,
            ),
    createdAt: json['createdAt'] as String,
    updatedAt: json['updatedAt'] as String,
  );
}
