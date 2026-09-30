import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../data/demo_seed.dart';
import '../../state/booking_requests_provider.dart';
import '../../widgets/studyhive_ui.dart';
import 'quotation_view_screen.dart';

/// Reached from the "See past bookings and costs" link on M-16. Drawn from the same tiles as the
/// M-12 booking list.
///
/// With [live] it lists the student's own requests (GET /api/booking-requests, already scoped to
/// them by the API) that have a quotation, each with its latest quote's status and total; tapping
/// one opens M-08 for that request. Without it, it shows the seeded reference preview.
class BookingHistoryScreen extends StatefulWidget {
  final bool previewEnabled;
  final bool live;

  const BookingHistoryScreen({
    super.key,
    this.previewEnabled = demoPreviewEnabled,
    this.live = false,
  });

  @override
  State<BookingHistoryScreen> createState() => _BookingHistoryScreenState();
}

class _BookingHistoryScreenState extends State<BookingHistoryScreen> {
  @override
  void initState() {
    super.initState();
    if (widget.live) {
      WidgetsBinding.instance.addPostFrameCallback(
        (_) => context.read<BookingRequestsProvider>().refresh(),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final appBar = AppBar(title: const Text('Past bookings & costs'));
    if (widget.live) {
      return Scaffold(appBar: appBar, body: const _LiveHistory());
    }

    if (!widget.previewEnabled) {
      return Scaffold(
        appBar: appBar,
        body: const PreviewUnavailable(
          message:
              'Booking costs will appear when booking history is connected.',
        ),
      );
    }
    return Scaffold(
      appBar: appBar,
      body: ScreenBody(
        gap: 12,
        children: [
          const DemoPreviewBanner(),
          for (final booking in demoHistory)
            Tile(
              opacity: 0.75,
              onTap:
                  () => Navigator.of(context).push(
                    MaterialPageRoute(
                      builder: (_) => const QuotationViewScreen(),
                    ),
                  ),
              children: [
                Kv.both(
                  leading: Text(
                    booking.objective,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 17,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  trailing: ShTag.forStatus(booking.status),
                ),
                FNote(
                  '${booking.completedAt} · Rs. ${booking.totalCost.toStringAsFixed(0)} spent',
                ),
              ],
            ),
        ],
      ),
    );
  }
}

class _LiveHistory extends StatelessWidget {
  const _LiveHistory();

  @override
  Widget build(BuildContext context) {
    final provider = context.watch<BookingRequestsProvider>();
    final costed =
        provider.requests.where((r) => r.latestQuotation != null).toList()
          ..sort((a, b) => b.createdAt.compareTo(a.createdAt));

    if (provider.loading && costed.isEmpty) {
      return const Center(child: CircularProgressIndicator());
    }
    if (provider.error != null && costed.isEmpty) {
      return ScreenBody(
        children: [
          InlineError(provider.error!),
          SecondaryButton('Try again', onPressed: provider.refresh),
        ],
      );
    }
    if (costed.isEmpty) {
      return const ScreenBody(
        children: [
          Heading('No costed bookings yet', fontSize: 20),
          FNote('Requests appear here once they have a quotation.'),
        ],
      );
    }

    final approvedSpend = costed
        .where((r) => r.latestQuotation!.status == 'Approved')
        .fold<double>(0, (sum, r) => sum + r.latestQuotation!.totalAmount);

    return ScreenBody(
      gap: 12,
      children: [
        Tile(
          tinted: true,
          children: [
            Kv('Approved spend', formatRs(approvedSpend)),
            Kv('Bookings with a quotation', '${costed.length}'),
          ],
        ),
        for (final request in costed)
          Tile(
            onTap:
                () => Navigator.of(context).push(
                  MaterialPageRoute(
                    builder: (_) => QuotationViewScreen(requestId: request.id),
                  ),
                ),
            children: [
              Kv.both(
                leading: Text(
                  request.objective,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontSize: 17,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                trailing: ShTag(
                  quotationStatusLabel(request.latestQuotation!.status),
                  tone:
                      request.latestQuotation!.status == 'Approved'
                          ? TagTone.accent
                          : TagTone.outline,
                ),
              ),
              FNote(
                '${request.preferredDateFrom} · ${formatRs(request.latestQuotation!.totalAmount)}'
                '${request.latestDecision?.comments != null ? ' · has a comment' : ''}',
              ),
            ],
          ),
      ],
    );
  }
}
