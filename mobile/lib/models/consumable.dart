/// S3 (Consumables & Stock) view models — mirrors web/src/types/consumables.ts and the locked
/// schema (api/src/StudyHive.Api/Data/Entities/S3/*.cs). No API client exists for these yet.
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
