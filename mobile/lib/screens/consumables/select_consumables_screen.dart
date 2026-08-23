import 'package:flutter/material.dart';

import '../../widgets/shells/list_shell.dart';

/// Mockup source: "Select consumables for booking (quantity picker)". Deliberately not wired into
/// CreateRequestScreen yet — S1's create form intentionally ships with no consumable selector
/// (DOCS: "no fake selectable catalog data"). Once S3 provides a real consumables API, this screen
/// becomes the quantity-picker step CreateRequestScreen links out to.
class SelectConsumablesScreen extends StatelessWidget {
  const SelectConsumablesScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const ListShell(
      owner: 'S3',
      title: 'Add consumables',
      description:
          'Pick items and quantities to include in this booking request.',
      columns: ['Name', 'Available', 'Quantity'],
      unavailableMessage:
          "Consumables & Stock (S3) hasn't built the consumables API yet.",
    );
  }
}
