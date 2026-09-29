import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../models/consumable.dart';
import '../../state/consumables_provider.dart';
import '../../widgets/studyhive_ui.dart';
import 'consumable_detail_screen.dart';
import 'select_consumables_screen.dart' show rupees;

/// "Browse consumables (see what's available)" — the live catalogue from GET /api/consumables,
/// with a search, each row showing price and stock status and opening its detail.
class BrowseConsumablesScreen extends StatefulWidget {
  const BrowseConsumablesScreen({super.key});

  @override
  State<BrowseConsumablesScreen> createState() =>
      _BrowseConsumablesScreenState();
}

class _BrowseConsumablesScreenState extends State<BrowseConsumablesScreen> {
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
      appBar: AppBar(title: const Text('Browse consumables')),
      body: ScreenBody(
        children: [
          const FNote('See what supplies are currently available.'),
          ShTextField(
            label: 'Search items',
            controller: _search,
            hintText: 'Markers, printouts…',
            onFieldSubmitted: (value) => consumables.refresh(search: value),
          ),
          if (consumables.loading && consumables.items.isEmpty)
            const Padding(
              padding: EdgeInsets.all(24),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (consumables.error != null) ...[
            InlineError(consumables.error!),
            SecondaryButton('Try again',
                onPressed: () => consumables.refresh(search: _search.text)),
          ] else if (consumables.items.isEmpty)
            const FNote('No items match. Try another search.')
          else
            for (final item in consumables.items)
              Tile(
                key: ValueKey('consumable:${item.id}'),
                onTap: () => Navigator.of(context).push(MaterialPageRoute(
                    builder: (_) =>
                        ConsumableDetailScreen(consumableId: item.id))),
                children: [
                  Kv.widget(label: item.name, trailing: StockTag(item)),
                  FNote(
                      '${rupees(item.unitPrice)} per ${item.unit} · ${item.availableQuantity} available'),
                ],
              ),
        ],
      ),
    );
  }
}

/// In stock is affirmative, low is in-flight, out of stock is inert — the reference's tag tones.
class StockTag extends StatelessWidget {
  final ConsumableListItem item;
  const StockTag(this.item, {super.key});

  @override
  Widget build(BuildContext context) => ShTag(
        item.stockLabel,
        tone: switch (item.stockStatus) {
          StockStatus.inStock => TagTone.accent,
          StockStatus.low => TagTone.outline,
          StockStatus.outOfStock => TagTone.neutral,
        },
      );
}
