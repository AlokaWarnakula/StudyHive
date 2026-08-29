import 'package:flutter/foundation.dart';

import '../api/consumables_api.dart';
import '../models/consumable.dart';

/// S3 (Consumables & Stock) state for the student app. SCAFFOLD.
///
/// Mirrors `BookingRequestsProvider`, the working S1 reference. Every call currently lands on a
/// 501 until S3 builds the endpoints, so [error] is set and [items] stays empty — the screens
/// show their real error state rather than pretending.
///
/// S3, to bring this to life:
///   1. Implement `api/src/StudyHive.Api/Controllers/Store/ConsumablesController.cs`.
///   2. Register this provider in `main.dart`'s MultiProvider, sharing `authProvider.apiClient`.
///   3. Point the browse / detail / select screens at it.
///
/// [selection] is the quantity picker's state. It is kept here rather than in the screen so that
/// the picker survives navigation between the catalogue and the booking form — which is what
/// happens once `select_consumables_screen.dart` is wired into the create flow.
class ConsumablesProvider extends ChangeNotifier {
  final ConsumablesApi _api;
  ConsumablesProvider(this._api);

  ConsumablesApi get api => _api;

  List<ConsumableListItem> _items = [];
  ConsumableDetail? _selected;
  final Map<String, int> _selection = {};
  bool _loading = false;
  String? _error;

  List<ConsumableListItem> get items => _items;
  ConsumableDetail? get selected => _selected;
  bool get loading => _loading;
  String? get error => _error;

  /// consumableId -> quantity, for the booking request's items.
  Map<String, int> get selection => Map.unmodifiable(_selection);

  Future<void> refresh({String? search}) async {
    await _run(() async {
      _items = await _api.list(search: search);
    });
  }

  Future<void> select(String id) async {
    await _run(() async {
      _selected = await _api.getById(id);
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
