import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../models/booking_request.dart';
import '../state/booking_requests_provider.dart';
import '../utils/colombo_time.dart';
import '../widgets/studyhive_ui.dart';
import 'booking_detail_screen.dart';
import 'rooms/qr_check_in_screen.dart';
import 'workflow_progress_screen.dart';

/// M-12 "My bookings" — GET /api/booking-requests?status=. The three tabs are a
/// segmented control, not Material chips.
///
/// AUDIT C-06: an Approved booking whose room times are over (Completed or NoShow) is Past, and
/// tiles show the booked room and slot. C-11: Draft tiles can be sent or deleted.
class TrackScreen extends StatefulWidget {
  const TrackScreen({super.key});

  @override
  State<TrackScreen> createState() => _TrackScreenState();
}

class _TrackScreenState extends State<TrackScreen> {
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
                for (final request in filtered) BookingTile(request: request),
            ],
          ),
        );
      },
    );
  }

  bool _matchesTab(BookingRequest request) => switch (_tab) {
        'Active' => request.status == 'Approved' && !request.hasEnded,
        'Waiting' => _waitingStatuses.contains(request.status),
        'Past' => _pastStatuses.contains(request.status) ||
            (request.status == 'Approved' && request.hasEnded),
        _ => true,
      };
}

/// One request in My bookings. Public so its states can be tested on their own.
class BookingTile extends StatefulWidget {
  final BookingRequest request;

  const BookingTile({super.key, required this.request});

  @override
  State<BookingTile> createState() => _BookingTileState();
}

class _BookingTileState extends State<BookingTile> {
  /// The reference dims settled bookings and keeps live ones at full strength.
  static const _settled = {'Completed', 'Rejected', 'Cancelled', 'Failed'};

  bool _busy = false;

  Future<void> _push(Widget screen) async {
    await Navigator.of(context).push(MaterialPageRoute(builder: (_) => screen));
    if (mounted) await context.read<BookingRequestsProvider>().refresh(); // C-08
  }

  Future<void> _send() async {
    setState(() => _busy = true);
    try {
      await context.read<BookingRequestsProvider>().submit(widget.request.id);
      if (mounted) await _push(WorkflowProgressScreen(requestId: widget.request.id));
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(e.toString())));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _delete() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete this draft?'),
        content: const Text('It was never sent, so nobody has seen it yet.'),
        actions: [
          TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('Keep it')),
          FilledButton(
              onPressed: () => Navigator.of(context).pop(true),
              child: const Text('Delete draft')),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;
    setState(() => _busy = true);
    try {
      await context.read<BookingRequestsProvider>().cancel(widget.request.id);
    } on ApiException catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context)
            .showSnackBar(SnackBar(content: Text(e.toString())));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final request = widget.request;
    final slot = request.slot;
    final checkedInAt = request.checkedInAt;
    final ended = request.status == 'Approved' && request.hasEnded;

    return Tile(
      opacity: _settled.contains(request.status) || ended ? 0.75 : null,
      onTap: () => _push(BookingDetailScreen(requestId: request.id)),
      children: [
        Kv.both(
          leading: Text(request.objective,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(fontSize: 17, fontWeight: FontWeight.w600)),
          trailing: ShTag.forStatus(checkedInAt != null
              ? 'Checked in'
              : ended
                  ? 'Completed'
                  : request.status),
        ),
        FNote(slot != null
            ? '${colomboSlot(slot.startsAt, slot.endsAt)} · ${slot.roomName}'
            : '${request.preferredDateFrom} · ${_hhmm(request.preferredTimeFrom)} – ${_hhmm(request.preferredTimeTo)} · Rs. ${request.budget.toStringAsFixed(0)}'),
        if (checkedInAt != null) FNote('Checked in at ${colomboHhmm(checkedInAt)}'),
        if (request.canCheckIn)
          SecondaryButton(
            'Check in',
            icon: Icons.qr_code_2,
            onPressed: () => _push(QrCheckInScreen(bookingId: request.id)),
          ),
        if (request.status == 'Draft')
          Row(children: [
            Expanded(
                child: PrimaryButton(_busy ? 'Sending…' : 'Send',
                    onPressed: _busy ? null : _send)),
            const SizedBox(width: 10),
            Expanded(
                child: SecondaryButton('Delete',
                    onPressed: _busy ? null : _delete)),
          ]),
        if (request.status == 'RevisionRequested')
          ShLink('See what to change',
              onPressed: () => _push(BookingDetailScreen(requestId: request.id))),
        if (request.status == 'Rejected')
          ShLink('See reason',
              onPressed: () => _push(BookingDetailScreen(requestId: request.id))),
      ],
    );
  }
}

String _hhmm(String time) => time.length >= 5 ? time.substring(0, 5) : time;
