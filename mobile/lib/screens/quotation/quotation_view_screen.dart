import 'package:flutter/material.dart';

import '../../data/demo_seed.dart';
import '../../models/quotation.dart';
import '../../widgets/studyhive_ui.dart';
import 'approval_status_screen.dart';

/// M-08 "Your quotation" — GET /api/quotations/{id}. Read-only until staff
/// decide; nothing here is actionable except cancelling the request.
class QuotationViewScreen extends StatelessWidget {
  final QuotationView? quotation;
  final bool previewEnabled;

  const QuotationViewScreen({
    super.key,
    this.quotation,
    this.previewEnabled = demoPreviewEnabled,
  });

  @override
  Widget build(BuildContext context) {
    if (quotation == null && !previewEnabled) {
      return Scaffold(
        appBar: AppBar(title: const Text('Cost breakdown')),
        body: const PreviewUnavailable(
          message:
              'A cost breakdown will appear after a quotation is available.',
        ),
      );
    }
    final quote = quotation ?? demoQuotation;
    final difference = (quote.budgetSnapshot - quote.totalAmount).abs();

    return Scaffold(
      appBar: AppBar(title: const Text('Cost breakdown')),
      body: ScreenBody(
        children: [
          if (previewEnabled) const DemoPreviewBanner(),
          const Tile(
            accented: true,
            children: [
              Lbl('Proposed'),
              Big('Room B-204 · 2:00 – 4:00 PM'),
              Align(
                alignment: Alignment.centerLeft,
                child: ShTag('Waiting for librarian', tone: TagTone.outline),
              ),
            ],
          ),
          Tile(
            children: [
              for (final item in quote.lineItems)
                Kv(item.itemName, 'Rs. ${item.lineTotal.toStringAsFixed(0)}'),
              KvTotal('Total', 'Rs. ${quote.totalAmount.toStringAsFixed(0)}'),
            ],
          ),
          Tile(
            tinted: true,
            children: [
              Kv('Your budget',
                  'Rs. ${quote.budgetSnapshot.toStringAsFixed(0)}'),
              Kv(quote.withinBudget ? 'Within budget by' : 'Over budget by',
                  'Rs. ${difference.toStringAsFixed(0)}'),
            ],
          ),
          const FNote('Nothing is charged until the librarian approves.'),
          SecondaryButton('Cancel this request', onPressed: () {}),
          GhostButton(
            'See approval status',
            onPressed: () => Navigator.of(context).push(MaterialPageRoute(
                builder: (_) => const ApprovalStatusScreen())),
          ),
        ],
      ),
    );
  }
}
