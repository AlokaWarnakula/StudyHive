import '../models/consumable.dart';
import 'api_client.dart';

/// S3 (Consumables & Stock) — student-facing consumables API, typed against
/// `api/src/StudyHive.Api/Controllers/Store/ConsumablesController.cs` (any signed-in user may read
/// the catalogue).
///
/// Screens this backs: browse consumables, consumable detail, and the quantity picker that the
/// create-request flow uses. What the picker produces is sent as the request's `items`, which S1's
/// `ValidateItemsOrProblemAsync` checks (unknown or duplicated consumable ids are a 422).
class ConsumablesApi {
  final ApiClient _client;
  const ConsumablesApi(this._client);

  /// GET /api/consumables — the active catalogue, by name. 100 is the API's page-size cap, well
  /// above the size of a study-room store.
  Future<List<ConsumableListItem>> list({String? search}) async {
    final uri = Uri(path: '/api/consumables', queryParameters: {
      'pageSize': '100',
      'sortBy': 'name',
      'sortDir': 'asc',
      if (search != null && search.trim().isNotEmpty) 'search': search.trim(),
    });
    final response = await _client.get(uri.toString()) as Map<String, dynamic>;
    final items = response['items'] as List<dynamic>;
    return items
        .map((e) => _detailFromJson(e as Map<String, dynamic>))
        .toList();
  }

  /// GET /api/consumables/{id} — `ConsumableDetailResponse`: `{ consumable, recentTransactions }`.
  /// The ledger is staff business, so only the consumable is read here.
  Future<ConsumableDetail> getById(String id) async {
    final json = await _client.get('/api/consumables/${Uri.encodeComponent(id)}')
        as Map<String, dynamic>;
    return _detailFromJson(json['consumable'] as Map<String, dynamic>);
  }

  static ConsumableDetail _detailFromJson(Map<String, dynamic> json) =>
      ConsumableDetail(
        id: json['id'] as String,
        name: json['name'] as String,
        unit: json['unit'] as String,
        unitPrice: (json['unitPrice'] as num).toDouble(),
        availableQuantity: json['availableQuantity'] as int,
        minStockLevel: json['minStockLevel'] as int,
        isActive: json['isActive'] as bool,
        description: json['description'] as String?,
      );
}
