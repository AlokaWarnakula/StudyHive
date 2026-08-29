/// S3 (Consumables & Stock) view models — the mobile half of the contract in
/// web/src/api/consumables.ts, over the locked schema
/// (api/src/StudyHive.Api/Data/Entities/S3/*.cs).
///
/// Paired with lib/api/consumables_api.dart and lib/state/consumables_provider.dart, which exist
/// as scaffolds: the calls are written but the endpoints behind them return 501 until S3
/// implements api/src/StudyHive.Api/Controllers/Store/.
class ConsumableListItem {
  final String id;
  final String name;
  final String unit;
  final double unitPrice;
  final int availableQuantity;
  final bool isActive;

  const ConsumableListItem({
    required this.id,
    required this.name,
    required this.unit,
    required this.unitPrice,
    required this.availableQuantity,
    required this.isActive,
  });
}

class ConsumableDetail extends ConsumableListItem {
  final String? description;
  final int minStockLevel;

  const ConsumableDetail({
    required super.id,
    required super.name,
    required super.unit,
    required super.unitPrice,
    required super.availableQuantity,
    required super.isActive,
    required this.description,
    required this.minStockLevel,
  });
}
