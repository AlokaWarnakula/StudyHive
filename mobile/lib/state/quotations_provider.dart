import 'package:flutter/foundation.dart';

import '../api/quotations_api.dart';
import '../models/quotation.dart';

/// S4 (Costing, Validation, Approval & Audit) state for the student app. SCAFFOLD.
///
/// Mirrors `BookingRequestsProvider`, the working S1 reference. Calls land on a 501 until S4
/// builds the endpoints, so [error] is set and [quotation] stays null — the screens show their
/// real error state rather than pretending.
///
/// S4, to bring this to life:
///   1. Implement `api/src/StudyHive.Api/Controllers/Approvals/QuotationsController.cs`.
///   2. Register this provider in `main.dart`'s MultiProvider, sharing `authProvider.apiClient`.
///   3. Point M-08 and the booking-history view at it.
///
/// Deliberately read-only. There is no approve/reject here: that decision belongs to a librarian
/// on W-04, and the student side of it is only ever a status to look at.
class QuotationsProvider extends ChangeNotifier {
  final QuotationsApi _api;
  QuotationsProvider(this._api);

  QuotationsApi get api => _api;

  QuotationView? _quotation;
  List<BookingHistoryItem> _history = [];
  bool _loading = false;
  String? _error;

  QuotationView? get quotation => _quotation;
  List<BookingHistoryItem> get history => _history;
  bool get loading => _loading;
  String? get error => _error;

  /// M-08.
  Future<void> load(String quotationId) async {
    await _run(() async {
      _quotation = await _api.getById(quotationId);
    });
  }

  /// Reached from the "See past bookings and costs" link on M-16.
  Future<void> loadHistory() async {
    await _run(() async {
      _history = await _api.history();
    });
  }

  Future<void> _run(Future<void> Function() action) async {
    _loading = true;
    _error = null;
    notifyListeners();
    try {
      await action();
    } catch (e) {
      _error = e.toString();
    } finally {
      _loading = false;
      notifyListeners();
    }
  }
}
