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

/// One room booking made for an approved request (BookingRequestResponse.roomBookings): where and
/// when to go, and whether the student checked in (AUDIT C-06).
class RoomBookingSummary {
  final String id;
  final String roomId;
  final String roomName;
  final DateTime startsAt;
  final DateTime endsAt;

  /// Confirmed, Cancelled, Completed or NoShow.
  final String status;
  final DateTime? checkedInAt;

  const RoomBookingSummary({
    required this.id,
    required this.roomId,
    required this.roomName,
    required this.startsAt,
    required this.endsAt,
    required this.status,
    this.checkedInAt,
  });

  factory RoomBookingSummary.fromJson(Map<String, dynamic> json) =>
      RoomBookingSummary(
        id: json['id'] as String,
        roomId: json['roomId'] as String,
        roomName: json['roomName'] as String,
        startsAt: DateTime.parse(json['startsAt'] as String),
        endsAt: DateTime.parse(json['endsAt'] as String),
        status: json['status'] as String,
        checkedInAt: json['checkedInAt'] == null
            ? null
            : DateTime.parse(json['checkedInAt'] as String),
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
  final List<RoomBookingSummary> roomBookings;
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
    this.roomBookings = const [],
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
    roomBookings:
        (json['roomBookings'] as List<dynamic>? ?? [])
            .map((e) => RoomBookingSummary.fromJson(e as Map<String, dynamic>))
            .toList()
          ..sort((a, b) => a.startsAt.compareTo(b.startsAt)),
    createdAt: json['createdAt'] as String,
    updatedAt: json['updatedAt'] as String,
  );

  /// The room times that still stand (not cancelled), earliest first.
  List<RoomBookingSummary> get activeBookings =>
      roomBookings.where((b) => b.status != 'Cancelled').toList();

  /// Where and when to go next: the first booking that has not ended, else the last one.
  RoomBookingSummary? get slot {
    final active = activeBookings;
    if (active.isEmpty) return null;
    final now = DateTime.now();
    for (final booking in active) {
      if (booking.endsAt.isAfter(now)) return booking;
    }
    return active.last;
  }

  /// When the student checked in to [slot], if they did.
  DateTime? get checkedInAt => slot?.checkedInAt;

  /// An approved booking whose every room time is over (C-06: it belongs in Past).
  bool get hasEnded {
    final active = activeBookings;
    if (active.isEmpty) return false;
    final now = DateTime.now();
    return active.every(
      (b) => b.status == 'Completed' || b.status == 'NoShow' || !b.endsAt.isAfter(now),
    );
  }

  /// A student may check in to an Approved booking that has not ended and is not already checked in.
  bool get canCheckIn => status == 'Approved' && !hasEnded && checkedInAt == null;

  /// What the API allows to be cancelled (A1): an undecided request, or an Approved one whose first
  /// room time has not started.
  bool get canCancel {
    const undecided = {
      'Draft',
      'Submitted',
      'Processing',
      'PendingApproval',
      'RevisionRequested',
    };
    if (undecided.contains(status)) return true;
    if (status != 'Approved') return false;
    final active = activeBookings;
    if (active.isEmpty) return true;
    return active.first.startsAt.isAfter(DateTime.now()) &&
        active.every((b) => b.checkedInAt == null);
  }
}
