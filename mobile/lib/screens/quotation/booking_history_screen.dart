import 'package:flutter/material.dart';

import '../../data/demo_seed.dart';
import '../../widgets/studyhive_ui.dart';
import 'quotation_view_screen.dart';

/// Reached from the "See past bookings and costs" link on M-16. Drawn from the
/// same tiles as the M-12 booking list.
class BookingHistoryScreen extends StatelessWidget {
  final bool previewEnabled;

  const BookingHistoryScreen({
    super.key,
    this.previewEnabled = demoPreviewEnabled,
  });

  @override
  Widget build(BuildContext context) {
    if (!previewEnabled) {
      return Scaffold(
        appBar: AppBar(title: const Text('Past bookings & costs')),
        body: const PreviewUnavailable(
          message:
              'Booking costs will appear when booking history is connected.',
        ),
      );
    }
    return Scaffold(
      appBar: AppBar(title: const Text('Past bookings & costs')),
      body: ScreenBody(
        gap: 12,
        children: [
          const DemoPreviewBanner(),
          for (final booking in demoHistory)
            Tile(
              opacity: 0.75,
              onTap: () => Navigator.of(context).push(MaterialPageRoute(
                  builder: (_) => const QuotationViewScreen())),
              children: [
                Kv.both(
                  leading: Text(booking.objective,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                          fontSize: 17, fontWeight: FontWeight.w600)),
                  trailing: ShTag.forStatus(booking.status),
                ),
                FNote(
                    '${booking.completedAt} · Rs. ${booking.totalCost.toStringAsFixed(0)} spent'),
              ],
            ),
        ],
      ),
    );
  }
}
