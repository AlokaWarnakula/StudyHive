import 'package:flutter/material.dart';

import '../studyhive_ui.dart';

enum ScreenStatus { loading, empty, unavailable, error, ready }

/// The one place every S2-S4 shell screen decides what to render for a non-"ready" data state —
/// mirrors web/src/components/shells/StateView.tsx so both clients present the same four states
/// the same way. A future owner wires up real data by rendering their own content for `ready`.
class StateView extends StatelessWidget {
  final ScreenStatus status;
  final String? emptyMessage;
  final String? unavailableMessage;
  final String? errorMessage;

  const StateView({
    super.key,
    required this.status,
    this.emptyMessage,
    this.unavailableMessage,
    this.errorMessage,
  });

  @override
  Widget build(BuildContext context) {
    switch (status) {
      case ScreenStatus.loading:
        return const Padding(
          padding: EdgeInsets.all(32),
          child: Center(child: CircularProgressIndicator()),
        );
      case ScreenStatus.empty:
        return Padding(
          padding: const EdgeInsets.all(32),
          child: Center(child: FNote(emptyMessage ?? 'Nothing here yet.')),
        );
      case ScreenStatus.unavailable:
        return PreviewUnavailable(
          message: unavailableMessage ??
              "This screen's backend hasn't been built yet.",
        );
      case ScreenStatus.error:
        return Padding(
          padding: const EdgeInsets.all(16),
          child: InlineError(errorMessage ?? 'Something went wrong.'),
        );
      case ScreenStatus.ready:
        return const SizedBox.shrink();
    }
  }
}
