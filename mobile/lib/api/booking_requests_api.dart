import '../models/booking_request.dart';
import '../models/workflow_status.dart';
import 'api_client.dart';

/// S1: the student-facing subset of the booking request lifecycle.
class BookingRequestsApi {
  final ApiClient _client;
  const BookingRequestsApi(this._client);

  Future<List<BookingRequest>> listMine() async {
    final response = await _client.get(
            '/api/booking-requests?pageSize=100&sortBy=createdAt&sortDir=desc')
        as Map<String, dynamic>;
    final items = response['items'] as List<dynamic>;
    return items
        .map((e) => BookingRequest.fromJson(e as Map<String, dynamic>))
        .toList();
  }

  Future<BookingRequest> getById(String id) async {
    final response =
        await _client.get('/api/booking-requests/$id') as Map<String, dynamic>;
    return BookingRequest.fromJson(response);
  }

  Future<BookingRequest> create({
    required String objective,
    required int groupSize,
    required String preferredDateFrom,
    required String preferredDateTo,
    required String preferredTimeFrom,
    required String preferredTimeTo,
    required int sessionsRequired,
    required int sessionDurationMinutes,
    required double budget,
    List<BookingRequestItem> items = const [],
    String? notes,
  }) async {
    final response = await _client.post('/api/booking-requests', body: {
      'objective': objective,
      'groupSize': groupSize,
      'preferredDateFrom': preferredDateFrom,
      'preferredDateTo': preferredDateTo,
      'preferredTimeFrom': preferredTimeFrom,
      'preferredTimeTo': preferredTimeTo,
      'sessionsRequired': sessionsRequired,
      'sessionDurationMinutes': sessionDurationMinutes,
      'budget': budget,
      'notes': notes,
      'items': items
          .map((item) =>
              {'consumableId': item.consumableId, 'quantity': item.quantity})
          .toList(),
    }) as Map<String, dynamic>;
    return BookingRequest.fromJson(response);
  }

  Future<String> submit(String requestId) async {
    final response =
        await _client.post('/api/booking-requests/$requestId/submit')
            as Map<String, dynamic>;
    return response['workflowId'] as String;
  }

  Future<void> cancel(String requestId) =>
      _client.delete('/api/booking-requests/$requestId');

  Future<WorkflowStatus> getStatus(String requestId) async {
    final response = await _client
        .get('/api/booking-requests/$requestId/status') as Map<String, dynamic>;
    return WorkflowStatus.fromJson(response);
  }
}
