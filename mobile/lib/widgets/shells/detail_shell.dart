import 'package:flutter/material.dart';

import '../studyhive_ui.dart';
import 'owner_badge.dart';
import 'state_view.dart';

/// Presentation shell for every "detail" screen. Field labels come from the caller's typed view
/// model; values are always an em-dash placeholder — never a fabricated example value — until the
/// owning component wires up its real API. Mirrors web/src/components/shells/DetailShell.tsx.
class DetailShell extends StatelessWidget {
  final String owner;
  final String title;
  final String? description;
  final List<String> fields;
  final String? unavailableMessage;
  final Widget? footer;

  const DetailShell({
    super.key,
    required this.owner,
    required this.title,
    this.description,
    required this.fields,
    this.unavailableMessage,
    this.footer,
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: ScreenBody(
        children: [
          Row(
            children: [
              Expanded(child: Heading(title, fontSize: 20)),
              OwnerBadge(owner: owner),
            ],
          ),
          if (description != null) FNote(description!),
          StateView(
              status: ScreenStatus.unavailable,
              unavailableMessage: unavailableMessage),
          Tile(
            children: [
              for (final field in fields) Kv(field, '—'),
            ],
          ),
          if (footer != null) footer!,
        ],
      ),
    );
  }
}
