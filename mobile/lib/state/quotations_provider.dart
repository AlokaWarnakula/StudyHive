import 'package:flutter/foundation.dart';

import '../api/quotations_api.dart';
import '../models/quotation.dart';

/// S4 (Costing, Validation, Approval & Audit) state for the student app, over [QuotationsApi].
/// Mirrors `BookingRequestsProvider`: a failed call sets [error] and the screens show it.
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
