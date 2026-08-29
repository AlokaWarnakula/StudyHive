import '../models/consumable.dart';
import 'api_client.dart';

/// S3 (Consumables & Stock) — student-facing consumables API. SCAFFOLD.
///
/// Written and typed against the locked schema (`api/src/StudyHive.Api/Data/Entities/S3/`), but
/// the endpoints return 501 until S3 implements `api/src/StudyHive.Api/Controllers/Store/`.
/// Nothing here invents data.
///
/// Screens this backs: browse consumables, consumable detail, and the quantity picker.
///
/// One thing S3 should know before wiring this up: `select_consumables_screen.dart` is
/// deliberately not connected to `create_request_screen.dart` yet. S1's create form ships with no
/// consumable selector on purpose, because there was no real catalogue to select from. Once these
/// endpoints exist, that screen becomes the picker step the create form links out to — and the
/// API already accepts the result: `booking_request_items` is validated by S1's
/// `ValidateItemsOrProblemAsync`, which rejects unknown or duplicated consumable ids as a 422.
class ConsumablesApi {
  final ApiClient _client;
  const ConsumablesApi(this._client);

  /// TODO(S3): GET /api/consumables
  Future<List<ConsumableListItem>> list({String? search}) async {
    final query = <String, String>{
      'pageSize': '100',
      if (search != null && search.isNotEmpty) 'search': search,
    };
    final qs = query.entries.map((e) => '${e.key}=${e.value}').join('&');
    final response = await _client.get('/api/consumables?$qs') as Map<String, dynamic>;
    final items = response['items'] as List<dynamic>;
    return items
        .map((e) => _listItemFromJson(e as Map<String, dynamic>))
        .toList();
  }

  /// TODO(S3): GET /api/consumables/{id}
  Future<ConsumableDetail> getById(String id) async {
    final json = await _client.get('/api/consumables/$id') as Map<String, dynamic>;
    return ConsumableDetail(
      id: json['id'] as String,
      name: json['name'] as String,
      unit: json['unit'] as String,
      unitPrice: (json['unitPrice'] as num).toDouble(),
      availableQuantity: json['availableQuantity'] as int,
      isActive: json['isActive'] as bool,
      description: json['description'] as String?,
      minStockLevel: json['minStockLevel'] as int,
    );
  }

  static ConsumableListItem _listItemFromJson(Map<String, dynamic> json) =>
      ConsumableListItem(
        id: json['id'] as String,
        name: json['name'] as String,
        unit: json['unit'] as String,
        unitPrice: (json['unitPrice'] as num).toDouble(),
        availableQuantity: json['availableQuantity'] as int,
        isActive: json['isActive'] as bool,
      );
}
