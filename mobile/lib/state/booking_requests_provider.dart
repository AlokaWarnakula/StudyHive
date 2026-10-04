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

  /// Creates a Draft (no submit yet). AUDIT C-11: the create screen keeps the returned id, so a
  /// submit that fails can be retried on the same draft instead of leaving an orphan behind.
  Future<BookingRequest> createDraft(BookingRequestFields fields) => _api.createDraft(fields);

  /// Saves edits to a Draft, or to a RevisionRequested request (which becomes a Draft again, C-02).
  Future<BookingRequest> updateDraft(String requestId, BookingRequestFields fields) =>
      _api.update(requestId, fields);

  /// Sends a Draft to the agents, then reloads the list.
  Future<String> submit(String requestId) async {
    final workflowId = await _api.submit(requestId);
    await refresh();
    return workflowId;
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
