import 'package:flutter/material.dart';

import '../studyhive_ui.dart';
import 'owner_badge.dart';
import 'state_view.dart';

/// Presentation shell for every "browse"/list screen (Rooms, Consumables, Booking history, ...).
/// Column labels come from the caller's typed view model — only the rows are withheld until the
/// owning component wires up its real API. Mirrors web/src/components/shells/ListShell.tsx, and is
/// built from the same reference tiles and tags as the M-xx frames.
class ListShell extends StatelessWidget {
  final String owner;
  final String title;
  final String? description;
  final List<String> columns;
  final ScreenStatus status;
  final String? unavailableMessage;
  final Widget? footer;

  const ListShell({
    super.key,
    required this.owner,
    required this.title,
    this.description,
    required this.columns,
    this.status = ScreenStatus.unavailable,
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
          const Lbl('Columns'),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              for (final column in columns)
                ShTag(column, tone: TagTone.neutral),
            ],
          ),
          StateView(status: status, unavailableMessage: unavailableMessage),
          if (footer != null) footer!,
        ],
      ),
    );
  }
}
