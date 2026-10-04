import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../api/api_client.dart';
import '../../data/demo_seed.dart';
import '../../models/booking_request.dart';
import '../../models/quotation.dart';
import '../../state/booking_requests_provider.dart';
import '../../widgets/studyhive_ui.dart';
import 'approval_status_screen.dart';

/// Rupees as the reference prints them: whole amounts without decimals ("Rs. 720"), anything
/// else to the cent ("Rs. 27.66"). Shared by the three S4 student screens.
String formatRs(double amount) =>
    amount == amount.roundToDouble()
        ? 'Rs. ${amount.toStringAsFixed(0)}'
        : 'Rs. ${amount.toStringAsFixed(2)}';

/// A quotation status in the student's words.
String quotationStatusLabel(String status) => switch (status) {
  'Proposed' => 'Waiting for librarian',
  'Approved' => 'Approved',
  'Rejected' => 'Not approved',
  'Superseded' => 'Replaced by a newer quote',
  _ => status,
};

/// A librarian decision in the student's words.
String decisionLabel(String decision) => switch (decision) {
  'Approved' => 'Approved',
  'Rejected' => 'Rejected',
  'RevisionRequested' => 'Changes requested',
  _ => decision,
};

/// M-08 "Your quotation". With [requestId] it is live: GET /api/booking-requests/{id} gives the
/// student's own latestQuotation, whose id then loads GET /api/quotations/{id} with its lines.
/// Without one it shows the seeded reference preview (or fails closed when preview is off).
/// Read-only: the approve/reject decision belongs to a librarian on W-04.
class QuotationViewScreen extends StatefulWidget {
  final String? requestId;
  final QuotationView? quotation;
  final bool previewEnabled;

  const QuotationViewScreen({
    super.key,
    this.requestId,
    this.quotation,
    this.previewEnabled = demoPreviewEnabled,
  });

  @override
  State<QuotationViewScreen> createState() => _QuotationViewScreenState();
}

class _QuotationViewScreenState extends State<QuotationViewScreen> {
  BookingRequest? _request;
  QuotationView? _quotation;
  bool _loading = false;
  String? _error;

  bool get _live => widget.requestId != null;

  @override
  void initState() {
    super.initState();
    if (_live) _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    final api = context.read<BookingRequestsProvider>().api;
    try {
      final request = await api.getById(widget.requestId!);
      final summary = request.latestQuotation;
      final quotation =
          summary == null ? null : await api.getQuotation(summary.id);
      if (!mounted) return;
      setState(() {
        _request = request;
        _quotation = quotation;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _error =
            e is ApiException ? e.toString() : 'Could not load your quotation.';
        _loading = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final appBar = AppBar(title: const Text('Cost breakdown'));

    if (_live) {
      if (_loading) {
        return Scaffold(
          appBar: appBar,
          body: const Center(child: CircularProgressIndicator()),
        );
      }
      if (_error != null) {
        return Scaffold(
          appBar: appBar,
          body: ScreenBody(
            children: [
              InlineError(_error!),
              SecondaryButton('Try again', onPressed: _load),
            ],
          ),
        );
      }
      final quote = _quotation;
      if (quote == null) {
        return Scaffold(
          appBar: appBar,
          body: const ScreenBody(
            children: [
              Heading('No quotation yet', fontSize: 20),
              FNote(
                'The cost appears here once the assistant has priced your request.',
              ),
            ],
          ),
        );
      }
      return Scaffold(
        appBar: appBar,
        body: _breakdown(quote, _request?.latestDecision),
      );
    }

    if (widget.quotation == null && !widget.previewEnabled) {
      return Scaffold(
        appBar: appBar,
        body: const PreviewUnavailable(
          message:
              'A cost breakdown will appear after a quotation is available.',
        ),
      );
    }
    return Scaffold(
      appBar: appBar,
      body: _breakdown(
        widget.quotation ?? demoQuotation,
        null,
        preview: widget.previewEnabled,
      ),
    );
  }

  Widget _breakdown(
    QuotationView quote,
    BookingDecisionSummary? decision, {
    bool preview = false,
  }) {
    final difference = (quote.budgetSnapshot - quote.totalAmount).abs();
    final status = quotationStatusLabel(quote.status);

    return ScreenBody(
      children: [
        if (preview) const DemoPreviewBanner(),
        Tile(
          accented: true,
          children: [
            const Lbl('Proposed'),
            Big(
              _live
                  ? 'Quotation · version ${quote.version}'
                  : 'Room B-204 · 2:00 – 4:00 PM',
            ),
            Align(
              alignment: Alignment.centerLeft,
              child: ShTag(
                status,
                tone:
                    quote.status == 'Approved'
                        ? TagTone.accent
                        : TagTone.outline,
              ),
            ),
          ],
        ),
        Tile(
          children: [
            for (final item in quote.lineItems)
              Kv(
                _live
                    ? '${item.itemName} · ${item.itemType == 'Room' ? '${_qty(item.quantity)} h' : '× ${_qty(item.quantity)}'}'
                    : item.itemName,
                formatRs(item.lineTotal),
              ),
            if (_live) ...[
              Kv('Room fee', formatRs(quote.roomFee)),
              Kv('Items', formatRs(quote.consumableCost)),
            ],
            KvTotal('Total', formatRs(quote.totalAmount)),
          ],
        ),
        Tile(
          tinted: true,
          children: [
            Kv('Your budget', formatRs(quote.budgetSnapshot)),
            Kv(
              quote.withinBudget ? 'Within budget by' : 'Over budget by',
              formatRs(difference),
            ),
          ],
        ),
        if (decision?.comments != null)
          Tile(
            children: [
              Lbl("Librarian's comment · ${decisionLabel(decision!.decision)}"),
              Text(decision.comments!),
            ],
          ),
        FNote(
          quote.status == 'Approved'
              ? 'Approved: your room is booked and your items are reserved.'
              : 'Nothing is charged until the librarian approves.',
        ),
        if (!_live) SecondaryButton('Cancel this request', onPressed: () {}),
        GhostButton(
          'See approval status',
          // C-20: swap with Approval status instead of stacking the two screens on each other.
          onPressed:
              () => Navigator.of(context).pushReplacement(
                MaterialPageRoute(
                  builder:
                      (_) => ApprovalStatusScreen(requestId: widget.requestId),
                ),
              ),
        ),
      ],
    );
  }
}

String _qty(double quantity) =>
    quantity == quantity.roundToDouble()
        ? quantity.toStringAsFixed(0)
        : quantity.toStringAsFixed(2);
