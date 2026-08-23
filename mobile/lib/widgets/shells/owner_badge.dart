import 'package:flutter/material.dart';

import '../studyhive_ui.dart';

/// Which relay-stage owner a screen belongs to (S1-S4) — a visual reminder
/// matching the badge used on the equivalent web shell
/// (web/src/components/shells/PageHeader.tsx). Drawn as a neutral reference tag.
class OwnerBadge extends StatelessWidget {
  final String owner;
  const OwnerBadge({super.key, required this.owner});

  @override
  Widget build(BuildContext context) =>
      ShTag(owner, tone: TagTone.neutral);
}
