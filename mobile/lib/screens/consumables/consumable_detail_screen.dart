import 'package:flutter/material.dart';

import '../../widgets/shells/detail_shell.dart';
import 'select_consumables_screen.dart';

/// Mockup source: "Consumable detail (price, stock status)". View model: ConsumableDetail.
class ConsumableDetailScreen extends StatelessWidget {
  const ConsumableDetailScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return DetailShell(
      owner: 'S3',
      title: 'Consumable detail',
      fields: const ['Name', 'Unit price', 'Available quantity', 'Description'],
      unavailableMessage:
          "Consumables & Stock (S3) hasn't built the consumable detail API yet.",
      footer: OutlinedButton(
        onPressed: () => Navigator.of(context).push(
            MaterialPageRoute(builder: (_) => const SelectConsumablesScreen())),
        child: const Text('Preview: add to a request →'),
      ),
    );
  }
}
