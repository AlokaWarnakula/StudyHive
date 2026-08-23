import 'package:flutter/material.dart';

import '../../widgets/shells/list_shell.dart';
import 'consumable_detail_screen.dart';

/// Mockup source: "Browse consumables (see what's available)". View model: ConsumableListItem.
class BrowseConsumablesScreen extends StatelessWidget {
  const BrowseConsumablesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return ListShell(
      owner: 'S3',
      title: 'Browse consumables',
      description: 'See what supplies are currently available.',
      columns: const ['Name', 'Unit', 'Price', 'Available'],
      unavailableMessage:
          "Consumables & Stock (S3) hasn't built the consumables API yet.",
      footer: OutlinedButton(
        onPressed: () => Navigator.of(context).push(
            MaterialPageRoute(builder: (_) => const ConsumableDetailScreen())),
        child: const Text('Preview: consumable detail →'),
      ),
    );
  }
}
