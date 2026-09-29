import 'package:flutter/foundation.dart';

import '../api/consumables_api.dart';
import '../models/booking_request.dart';
import '../models/consumable.dart';

/// S3 (Consumables & Stock) state for the student app. Mirrors `BookingRequestsProvider`, and is
/// registered in `main.dart`'s MultiProvider on `authProvider.apiClient`.
///
/// [selection] is the quantity picker's state. It lives here rather than in a screen so the
/// picks survive moving between the create-request flow and the full-screen picker
/// (`select_consumables_screen.dart`), and `CreateRequestScreen` sends it as the request's items.
class ConsumablesProvider extends ChangeNotifier {
  final ConsumablesApi _api;
  ConsumablesProvider(this._api);

  ConsumablesApi get api => _api;

  List<ConsumableListItem> _items = [];
  ConsumableDetail? _selected;
  final Map<String, int> _selection = {};

  /// Every item seen by any list call, so a pick made under one search can still be named and
  /// priced after the list is filtered differently.
  final Map<String, ConsumableListItem> _known = {};
  bool _loading = false;
  String? _error;

  List<ConsumableListItem> get items => _items;
  ConsumableDetail? get selected => _selected;
  bool get loading => _loading;
  String? get error => _error;

  /// consumableId -> quantity, for the booking request's items.
  Map<String, int> get selection => Map.unmodifiable(_selection);

  ConsumableListItem? itemFor(String id) => _known[id];

  /// The picks as the request payload: one line per consumable, quantity > 0.
  List<BookingRequestItem> get selectedItems => [
        for (final entry in _selection.entries)
          BookingRequestItem(consumableId: entry.key, quantity: entry.value),
      ];

  double get selectionTotal => _selection.entries.fold(
        0,
        (total, entry) => total + (_known[entry.key]?.unitPrice ?? 0) * entry.value,
      );

  Future<void> refresh({String? search}) async {
    await _run(() async {
      _items = await _api.list(search: search);
      for (final item in _items) {
        _known[item.id] = item;
      }
    });
  }

  Future<void> select(String id) async {
    _selected = null;
    await _run(() async {
      _selected = await _api.getById(id);
      _known[id] = _selected!;
    });
  }

  /// A quantity of zero or less removes the line rather than storing it. S1's API rejects a
  /// quantity that is not greater than zero, and a duplicate consumable id, as a 422 — keeping
  /// the map clean here means that never has to round-trip.
  void setQuantity(String consumableId, int quantity) {
    if (quantity <= 0) {
      _selection.remove(consumableId);
    } else {
      _selection[consumableId] = quantity;
    }
    notifyListeners();
  }

  void clearSelection() {
    if (_selection.isEmpty) return;
    _selection.clear();
    notifyListeners();
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
