import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../state/consumables_provider.dart';
import '../../widgets/studyhive_ui.dart';
import 'browse_consumables_screen.dart' show StockTag;
import 'select_consumables_screen.dart' show rupees;

/// "Consumable detail (price, stock status)" — GET /api/consumables/{id}.
class ConsumableDetailScreen extends StatefulWidget {
  final String consumableId;

  const ConsumableDetailScreen({super.key, required this.consumableId});

  @override
  State<ConsumableDetailScreen> createState() => _ConsumableDetailScreenState();
}

class _ConsumableDetailScreenState extends State<ConsumableDetailScreen> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) context.read<ConsumablesProvider>().select(widget.consumableId);
    });
  }

  @override
  Widget build(BuildContext context) {
    final consumables = context.watch<ConsumablesProvider>();
    final item = consumables.selected;
    final ready = item != null && item.id == widget.consumableId;

    return Scaffold(
      appBar: AppBar(title: Text(ready ? item.name : 'Consumable')),
      body: ScreenBody(
        children: [
          if (consumables.error != null) ...[
            InlineError(consumables.error!),
            SecondaryButton('Try again',
                onPressed: () => consumables.select(widget.consumableId)),
          ] else if (!ready)
            const Padding(
              padding: EdgeInsets.all(24),
              child: Center(child: CircularProgressIndicator()),
            )
          else ...[
            Heading(item.name, fontSize: 22),
            Tile(
              children: [
                Kv('Unit price', '${rupees(item.unitPrice)} per ${item.unit}'),
                Kv('Available', '${item.availableQuantity}'),
                Kv.widget(label: 'Stock', trailing: StockTag(item)),
              ],
            ),
            if (item.description != null && item.description!.isNotEmpty)
              Tile(children: [const Lbl('About'), Text(item.description!)]),
            const FNote(
                'To ask for this item, add it in step 2 when you book a room.'),
          ],
        ],
      ),
    );
  }
}
