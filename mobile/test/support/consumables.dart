import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/consumables_api.dart';
import 'package:mobile/state/consumables_provider.dart';

/// `ConsumableResponse` rows as GET /api/consumables returns them (StoreContracts.cs).
Map<String, dynamic> consumableJson({
  required String id,
  required String name,
  String unit = 'pcs',
  num unitPrice = 60,
  int stockQuantity = 42,
  int reservedQuantity = 0,
  int minStockLevel = 10,
  String? description,
}) =>
    {
      'id': id,
      'name': name,
      'description': description,
      'unit': unit,
      'unitPrice': unitPrice,
      'stockQuantity': stockQuantity,
      'reservedQuantity': reservedQuantity,
      'availableQuantity': stockQuantity - reservedQuantity,
      'minStockLevel': minStockLevel,
      'isLowStock': stockQuantity <= minStockLevel,
      'isActive': true,
      'createdAt': '2026-09-01T00:00:00Z',
      'updatedAt': '2026-09-01T00:00:00Z',
    };

final markersJson = consumableJson(
    id: 'c-markers',
    name: 'Whiteboard markers',
    unit: 'marker',
    unitPrice: 60,
    stockQuantity: 5,
    reservedQuantity: 2,
    minStockLevel: 1,
    description: 'Black and blue dry-erase markers.');
final printoutsJson = consumableJson(
    id: 'c-a4',
    name: 'A4 printouts',
    unit: 'page',
    unitPrice: 5,
    stockQuantity: 1200,
    minStockLevel: 200);
final hdmiJson = consumableJson(
    id: 'c-hdmi',
    name: 'HDMI cable',
    unit: 'cable',
    unitPrice: 0,
    stockQuantity: 0,
    minStockLevel: 2);

final catalogueJson = [printoutsJson, hdmiJson, markersJson];

http.Response jsonReply(Object body, [int status = 200]) => http.Response(
    jsonEncode(body), status,
    headers: {'content-type': 'application/json'});

Map<String, dynamic> pageOf(List<Map<String, dynamic>> items) => {
      'items': items,
      'page': 1,
      'pageSize': 100,
      'totalItems': items.length,
      'totalPages': items.isEmpty ? 0 : 1,
    };

/// Answers the consumables endpoints from [catalogueJson]; null for anything else so a caller
/// can chain its own routes.
http.Response? consumablesRoute(http.Request request) {
  if (request.method != 'GET') return null;
  if (request.url.path == '/api/consumables') {
    final search = request.url.queryParameters['search']?.toLowerCase();
    return jsonReply(pageOf(catalogueJson
        .where((c) => search == null || (c['name'] as String).toLowerCase().contains(search))
        .toList()));
  }
  for (final c in catalogueJson) {
    if (request.url.path == '/api/consumables/${c['id']}') {
      return jsonReply({'consumable': c, 'recentTransactions': []});
    }
  }
  return null;
}

ConsumablesProvider consumablesProviderFor([MockClientHandler? handler]) =>
    ConsumablesProvider(ConsumablesApi(ApiClient(
        client: MockClient(handler ??
            (request) async =>
                consumablesRoute(request) ?? http.Response('not found', 404)))));
