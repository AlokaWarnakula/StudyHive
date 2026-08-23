import 'package:flutter/foundation.dart';

import '../api/booking_requests_api.dart';
import '../models/booking_request.dart';

/// S1's booking request list + create/submit/cancel actions for the signed-in student.
class BookingRequestsProvider extends ChangeNotifier {
  final BookingRequestsApi _api;
  BookingRequestsProvider(this._api);

  /// Exposed for screens that need a single request or its workflow status directly (e.g.
  /// BookingDetailScreen) without adding single-item state to this list-oriented provider.
  BookingRequestsApi get api => _api;

  List<BookingRequest> _requests = [];
  bool _loading = false;
  String? _error;

  List<BookingRequest> get requests => _requests;
  bool get loading => _loading;
  String? get error => _error;

  Future<void> refresh() async {
    _loading = true;
    _error = null;
    notifyListeners();
    try {
      _requests = await _api.listMine();
    } catch (e) {
      _error = e.toString();
    } finally {
      _loading = false;
      notifyListeners();
    }
  }

  /// Creates a Draft request, then immediately submits it to trigger the AI workflow — the mobile
  /// create form has no separate "save draft" step (DOCS Flutter pages: a single Create form).
  Future<BookingRequest> createAndSubmit({
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
    final created = await _api.create(
      objective: objective,
      groupSize: groupSize,
      preferredDateFrom: preferredDateFrom,
      preferredDateTo: preferredDateTo,
      preferredTimeFrom: preferredTimeFrom,
      preferredTimeTo: preferredTimeTo,
      sessionsRequired: sessionsRequired,
      sessionDurationMinutes: sessionDurationMinutes,
      budget: budget,
      items: items,
      notes: notes,
    );
    await _api.submit(created.id);
    await refresh();
    return created;
  }

  Future<void> cancel(String requestId) async {
    await _api.cancel(requestId);
    await refresh();
  }

  void reset() {
    _requests = [];
    _loading = false;
    _error = null;
    notifyListeners();
  }
}
