class BookingRequestItem {
  final String consumableId;
  final int quantity;

  const BookingRequestItem(
      {required this.consumableId, required this.quantity});

  factory BookingRequestItem.fromJson(Map<String, dynamic> json) =>
      BookingRequestItem(
        consumableId: json['consumableId'] as String,
        quantity: json['quantity'] as int,
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
        items: (json['items'] as List<dynamic>? ?? [])
            .map((e) => BookingRequestItem.fromJson(e as Map<String, dynamic>))
            .toList(),
        latestWorkflowId: json['latestWorkflowId'] as String?,
        createdAt: json['createdAt'] as String,
        updatedAt: json['updatedAt'] as String,
      );
}
