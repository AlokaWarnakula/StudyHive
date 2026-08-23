import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/booking_request.dart';
import '../state/booking_requests_provider.dart';
import '../widgets/studyhive_ui.dart';
import 'booking_detail_screen.dart';
import 'rooms/qr_check_in_screen.dart';

/// M-12 "My bookings" — GET /api/booking-requests?status=. The three tabs are a
/// segmented control, not Material chips.
class TrackScreen extends StatefulWidget {
  const TrackScreen({super.key});

  @override
  State<TrackScreen> createState() => _TrackScreenState();
}

class _TrackScreenState extends State<TrackScreen> {
  static const _activeStatuses = {'Approved'};
  static const _waitingStatuses = {
    'Draft',
    'Submitted',
    'Processing',
    'PendingApproval',
    'RevisionRequested'
  };
  static const _pastStatuses = {'Completed', 'Rejected', 'Cancelled', 'Failed'};

  String _tab = 'Active';

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback(
        (_) => context.read<BookingRequestsProvider>().refresh());
  }

  @override
  Widget build(BuildContext context) {
    return Consumer<BookingRequestsProvider>(
      builder: (context, provider, _) {
        final filtered = provider.requests.where(_matchesTab).toList();
        return RefreshIndicator(
          onRefresh: provider.refresh,
          child: ScreenBody(
            gap: 12,
            children: [
              Segmented<String>(
                options: const [
                  ('Active', 'Active'),
                  ('Waiting', 'Waiting'),
                  ('Past', 'Past'),
                ],
                value: _tab,
                onChanged: (value) => setState(() => _tab = value),
              ),
              if (provider.loading && provider.requests.isEmpty)
                const Padding(
                  padding: EdgeInsets.all(28),
                  child: Center(child: CircularProgressIndicator()),
                )
              else if (provider.error != null && provider.requests.isEmpty)
                InlineError(provider.error!)
              else if (filtered.isEmpty)
                Tile(children: [
                  Text(_tab == 'Past'
                      ? 'No past bookings yet.'
                      : 'No ${_tab.toLowerCase()} bookings.'),
                ])
              else
                for (final request in filtered) _BookingTile(request: request),
            ],
          ),
        );
      },
    );
  }

  bool _matchesTab(BookingRequest request) => switch (_tab) {
        'Active' => _activeStatuses.contains(request.status),
        'Waiting' => _waitingStatuses.contains(request.status),
        'Past' => _pastStatuses.contains(request.status),
        _ => true,
      };
}

class _BookingTile extends StatelessWidget {
  final BookingRequest request;

  const _BookingTile({required this.request});

  /// The reference dims settled bookings and keeps live ones at full strength.
  static const _settled = {'Completed', 'Rejected', 'Cancelled', 'Failed'};

  @override
  Widget build(BuildContext context) {
    void open() => Navigator.of(context).push(
          MaterialPageRoute(
              builder: (_) => BookingDetailScreen(requestId: request.id)),
        );

    return Tile(
      opacity: _settled.contains(request.status) ? 0.75 : null,
      onTap: open,
      children: [
        Kv.both(
          leading: Text(request.objective,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w600)),
          trailing: ShTag.forStatus(request.status),
        ),
        FNote(
            '${request.preferredDateFrom} · ${_hhmm(request.preferredTimeFrom)} – ${_hhmm(request.preferredTimeTo)} · Rs. ${request.budget.toStringAsFixed(0)}'),
        if (request.status == 'Approved')
          SecondaryButton(
            'Check in',
            icon: Icons.qr_code_2,
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(
                  builder: (_) => QrCheckInScreen(bookingId: request.id)),
            ),
          ),
        if (request.status == 'Rejected') ShLink('See reason', onPressed: open),
      ],
    );
  }
}

String _hhmm(String time) => time.length >= 5 ? time.substring(0, 5) : time;
