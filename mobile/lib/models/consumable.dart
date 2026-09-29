/// S3 (Consumables & Stock) view models — the mobile half of the contract in
/// web/src/api/consumables.ts, over `ConsumableResponse` in
/// api/src/StudyHive.Api/Controllers/Store/StoreContracts.cs.
///
/// Paired with lib/api/consumables_api.dart and lib/state/consumables_provider.dart.
enum StockStatus { inStock, low, outOfStock }

class ConsumableListItem {
  final String id;
  final String name;
  final String unit;
  final double unitPrice;

  /// On-hand stock less what is already reserved — what a student can still ask for.
  final int availableQuantity;
  final int minStockLevel;
  final bool isActive;

  const ConsumableListItem({
    required this.id,
    required this.name,
    required this.unit,
    required this.unitPrice,
    required this.availableQuantity,
    required this.minStockLevel,
    required this.isActive,
  });

  StockStatus get stockStatus {
    if (availableQuantity <= 0) return StockStatus.outOfStock;
    if (availableQuantity <= minStockLevel) return StockStatus.low;
    return StockStatus.inStock;
  }

  String get stockLabel => switch (stockStatus) {
        StockStatus.inStock => 'In stock',
        StockStatus.low => 'Low stock',
        StockStatus.outOfStock => 'Out of stock',
      };
}

class ConsumableDetail extends ConsumableListItem {
  final String? description;

  const ConsumableDetail({
    required super.id,
    required super.name,
    required super.unit,
    required super.unitPrice,
    required super.availableQuantity,
    required super.minStockLevel,
    required super.isActive,
    required this.description,
  });
}
