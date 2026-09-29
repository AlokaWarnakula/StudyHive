import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/consumable.dart';
import '../../state/consumables_provider.dart';
import '../../theme/app_theme.dart';
import '../../widgets/studyhive_ui.dart';

String rupees(double value) => 'Rs. ${value.toStringAsFixed(0)}';

/// "Select consumables for booking (quantity picker)" — the full-screen picker that
/// CreateRequestScreen's step 2 links out to ("Search all items"). Picks are kept in
/// [ConsumablesProvider.selection], which the create flow sends as `booking_request_items`.
class SelectConsumablesScreen extends StatefulWidget {
  const SelectConsumablesScreen({super.key});

  @override
  State<SelectConsumablesScreen> createState() =>
      _SelectConsumablesScreenState();
}

class _SelectConsumablesScreenState extends State<SelectConsumablesScreen> {
  final _search = TextEditingController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<ConsumablesProvider>().refresh();
    });
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final consumables = context.watch<ConsumablesProvider>();

    return Scaffold(
      appBar: AppBar(title: const Text('Add consumables')),
      body: ScreenBody(
        children: [
          const FNote(
              'Pick items and quantities to include in this booking request.'),
          ShTextField(
            label: 'Search items',
            controller: _search,
            hintText: 'Markers, printouts…',
            onFieldSubmitted: (value) => consumables.refresh(search: value),
          ),
          ...ConsumablePickerList.children(consumables),
          Container(
            padding: const EdgeInsets.only(top: 12),
            decoration: const BoxDecoration(
                border: Border(top: BorderSide(color: AppColors.divider))),
            child: Kv('Items subtotal', rupees(consumables.selectionTotal)),
          ),
          PrimaryButton('Done', onPressed: () => Navigator.of(context).pop()),
        ],
      ),
    );
  }
}

/// The live catalogue as quantity tiles, with loading, error and empty states. Shared by this
/// screen and CreateRequestScreen's step 2 so both read and write the same selection.
class ConsumablePickerList {
  static List<Widget> children(ConsumablesProvider consumables) {
    if (consumables.loading && consumables.items.isEmpty) {
      return const [
        Padding(
          padding: EdgeInsets.all(24),
          child: Center(child: CircularProgressIndicator()),
        ),
      ];
    }
    if (consumables.error != null) {
      return [
        InlineError(consumables.error!),
        SecondaryButton('Try again', onPressed: consumables.refresh),
      ];
    }
    if (consumables.items.isEmpty) {
      return const [FNote('No items match. Try another search.')];
    }
    return [
      for (final item in consumables.items)
        ConsumableQuantityTile(
          key: ValueKey('pick:${item.id}'),
          item: item,
          quantity: consumables.selection[item.id] ?? 0,
          onChanged: (value) => consumables.setQuantity(item.id, value),
        ),
    ];
  }
}

/// One catalogue line with a quantity counter capped at what is free to reserve. An item that is
/// out of stock is shown dimmed with no counter, as the reference draws it.
class ConsumableQuantityTile extends StatelessWidget {
  final ConsumableListItem item;
  final int quantity;
  final ValueChanged<int> onChanged;

  const ConsumableQuantityTile({
    super.key,
    required this.item,
    required this.quantity,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    if (item.stockStatus == StockStatus.outOfStock) {
      return Tile(
        gap: 10,
        opacity: 0.55,
        children: [
          Kv.widget(
            label: item.name,
            trailing: const ShTag('Out of stock', tone: TagTone.neutral),
          ),
          const FNote('Staff will restock this item soon.'),
        ],
      );
    }

    return Tile(
      gap: 10,
      children: [
        Kv(item.name, '${rupees(item.unitPrice)} / ${item.unit}'),
        Kv.both(
          leading: FNote('${item.availableQuantity} in stock'),
          trailing: CounterControl(
            value: quantity,
            max: item.availableQuantity,
            size: 44,
            valueFontSize: 17,
            onChanged: onChanged,
          ),
        ),
      ],
    );
  }
}
