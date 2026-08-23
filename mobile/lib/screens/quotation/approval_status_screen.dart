import 'package:flutter/material.dart';

import '../../data/demo_seed.dart';
import '../../widgets/studyhive_ui.dart';

/// Not one of the 16 reference frames — it expands the "Waiting for librarian"
/// tag on M-08, so it is drawn from the same tiles and tags.
class ApprovalStatusScreen extends StatelessWidget {
  const ApprovalStatusScreen({super.key});

  @override
  Widget build(BuildContext context) {
    if (!demoPreviewEnabled) {
      return Scaffold(
        appBar: AppBar(title: const Text('Approval status')),
        body: const PreviewUnavailable(
          message: 'Approval details will appear when a quotation is ready.',
        ),
      );
    }
    return Scaffold(
      appBar: AppBar(title: const Text('Approval status')),
      body: const ScreenBody(
        children: [
          DemoPreviewBanner(),
          Tile(
            accented: true,
            children: [
              Align(
                alignment: Alignment.centerLeft,
                child: ShTag('Waiting for librarian', tone: TagTone.outline),
              ),
              Heading('Waiting for a librarian', fontSize: 20),
              Text(
                'Your room and items are proposed. A librarian will review the cost and availability.',
                style: TextStyle(fontSize: 14),
              ),
            ],
          ),
          FNote('You will get a notification as soon as they decide.'),
        ],
      ),
    );
  }
}
