import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/booking_request.dart';
import '../state/auth_provider.dart';
import '../state/booking_requests_provider.dart';
import '../state/profile_provider.dart';
import '../theme/app_theme.dart';
import '../utils/colombo_time.dart';
import '../widgets/studyhive_ui.dart';
import 'booking_detail_screen.dart';
import 'create_request_screen.dart';
import 'profile_screen.dart';
import 'rooms/browse_rooms_screen.dart';
import 'rooms/qr_check_in_screen.dart';
import 'track_screen.dart';

/// The four-tab shell. Each tab draws its own .mtop bar; the .mnav strip is flat
/// with a hairline top border, not Material's pill-indicator NavigationBar.
///
/// AUDIT C-08: the tabs live in an IndexedStack, so they are built once; the shell reloads the
/// bookings and the profile when a tab is opened, when the app comes back to the foreground, and
/// when a screen pushed from Home returns.
class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> with WidgetsBindingObserver {
  int _index = 0;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) _refresh();
  }

  Future<void> _refresh() async {
    if (!mounted) return;
    await Future.wait([
      context.read<BookingRequestsProvider>().refresh(),
      context.read<ProfileProvider>().refresh(),
    ]);
  }

  void _selectTab(int index) {
    setState(() => _index = index);
    if (index != 1) _refresh(); // Home, Bookings and Profile all show booking state
  }

  Future<void> _openBookingFlow() async {
    await Navigator.of(context)
        .push(MaterialPageRoute(builder: (_) => const CreateRequestScreen()));
    await _refresh();
  }

  PreferredSizeWidget _appBar() {
    if (_index == 0) {
      final firstName =
          (context.watch<AuthProvider>().studentName ?? 'Student').split(' ').first;
      return AppBar(
        titleSpacing: 16,
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            const Lbl('Good morning'),
            Text(firstName, style: headingStyle(fontSize: 21)),
          ],
        ),
        actions: [
          // D4: email is the notification channel; the bell opens My bookings.
          IconButton(
            tooltip: 'My bookings',
            onPressed: () => _selectTab(2),
            icon: const Icon(Icons.notifications_none, size: 22),
          ),
          const SizedBox(width: 6),
        ],
      );
    }
    return AppBar(
      title: Text(switch (_index) {
        1 => 'Rooms',
        2 => 'My bookings',
        _ => 'Profile',
      }),
      actions: const [SizedBox(width: 6)],
    );
  }

  @override
  Widget build(BuildContext context) {
    final screens = [
      _HomeDashboard(
          onBookRoom: _openBookingFlow,
          onOpenBookings: () => _selectTab(2),
          onReturn: _refresh),
      const BrowseRoomsScreen(embedded: true),
      const TrackScreen(),
      const ProfileScreen(),
    ];

    return Scaffold(
      appBar: _appBar(),
      body: IndexedStack(index: _index, children: screens),
      bottomNavigationBar: BottomNav(
        index: _index,
        onChanged: _selectTab,
      ),
    );
  }
}

/// M-03 "Home" — GET /api/booking-requests?mine=true and the weekly eligibility
/// allowance.
class _HomeDashboard extends StatefulWidget {
  final VoidCallback onBookRoom;
  final VoidCallback onOpenBookings;
  final Future<void> Function() onReturn;

  const _HomeDashboard(
      {required this.onBookRoom,
      required this.onOpenBookings,
      required this.onReturn});

  @override
  State<_HomeDashboard> createState() => _HomeDashboardState();
}

class _HomeDashboardState extends State<_HomeDashboard> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      context.read<BookingRequestsProvider>().refresh();
      context.read<ProfileProvider>().refresh();
    });
  }

  @override
  Widget build(BuildContext context) {
    final bookings = context.watch<BookingRequestsProvider>();
    final profileState = context.watch<ProfileProvider>();
    final profile = profileState.profile;
    final eligibility = profileState.eligibility;
    final requests = bookings.requests;
    final next = _nextBooking(requests);
    final waiting = _firstMatching(requests, const {
      'Submitted',
      'Processing',
      'PendingApproval',
      'RevisionRequested'
    });
    // C-16: the server's own count of this Colombo week's submissions, not a local guess.
    final used = eligibility?.usedThisWeek;
    final limit = eligibility?.maxBookingsPerWeek ?? profile?.maxBookingsPerWeek ?? 3;

    return RefreshIndicator(
      onRefresh: () async {
        await Future.wait([
          context.read<BookingRequestsProvider>().refresh(),
          context.read<ProfileProvider>().refresh(),
        ]);
      },
      child: ScreenBody(
        children: [
          PrimaryButton(
            'Book a room',
            icon: Icons.add,
            fontSize: 19,
            padding: const EdgeInsets.symmetric(vertical: 18),
            onPressed: widget.onBookRoom,
          ),
          if (bookings.loading && requests.isEmpty)
            const Padding(
              padding: EdgeInsets.all(24),
              child: Center(child: CircularProgressIndicator()),
            )
          else if (next == null)
            Tile(
              onTap: widget.onBookRoom,
              children: const [
                Lbl('Next booking'),
                Text('No approved booking yet. Start a request when you are ready.'),
              ],
            )
          else
            Tile(
              children: [
                const Lbl('Next booking'),
                Big(next.objective),
                if (next.slot case final slot?)
                  Kv('${colomboDay(slot.startsAt)} · ${slot.roomName}',
                      '${colomboHhmm(slot.startsAt)} – ${colomboHhmm(slot.endsAt)}')
                else
                  Kv(_dayLabel(next.preferredDateFrom),
                      _timeRange(next.preferredTimeFrom, next.preferredTimeTo)),
                Row(
                  children: [
                    ShTag.forStatus(
                        next.checkedInAt != null ? 'Checked in' : next.status),
                    const SizedBox(width: 8),
                    FNote('Group of ${next.groupSize}'),
                  ],
                ),
                if (next.canCheckIn)
                  SecondaryButton(
                    'Check in with QR',
                    icon: Icons.qr_code_2,
                    onPressed: () => _push(QrCheckInScreen(bookingId: next.id)),
                  ),
              ],
            ),
          Tile(
            children: [
              Kv('Bookings this week',
                  used == null ? 'Limit $limit' : '$used of $limit used'),
              Meter(
                  percent: limit == 0 || used == null
                      ? 0
                      : (used / limit).clamp(0, 1).toDouble()),
              const FNote('Limit resets every Monday.'),
            ],
          ),
          const Lbl('Waiting on staff'),
          if (waiting == null)
            const Tile(
              children: [Text('Nothing is waiting for staff right now.')],
            )
          else
            Tile(
              gap: 6,
              onTap: () => _push(BookingDetailScreen(requestId: waiting.id)),
              children: [
                Kv.both(
                  leading: Text(waiting.objective,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                          fontSize: 14, fontWeight: FontWeight.w500)),
                  trailing: ShTag.forStatus(waiting.status),
                ),
                FNote(
                    '${_dayLabel(waiting.preferredDateFrom)} · Rs. ${waiting.budget.toStringAsFixed(0)} budget'),
              ],
            ),
          if (bookings.error != null)
            InlineError(bookings.error!),
        ],
      ),
    );
  }

  Future<void> _push(Widget screen) async {
    await Navigator.of(context).push(MaterialPageRoute(builder: (_) => screen));
    await widget.onReturn(); // C-08
  }

  /// The soonest Approved booking that has not ended (C-06).
  BookingRequest? _nextBooking(List<BookingRequest> requests) {
    final upcoming = requests
        .where((r) => r.status == 'Approved' && !r.hasEnded)
        .toList()
      ..sort((a, b) {
        final aStart = a.slot?.startsAt;
        final bStart = b.slot?.startsAt;
        if (aStart == null || bStart == null) return aStart == null ? 1 : -1;
        return aStart.compareTo(bStart);
      });
    return upcoming.isEmpty ? null : upcoming.first;
  }

  BookingRequest? _firstMatching(
      List<BookingRequest> requests, Set<String> statuses) {
    for (final request in requests) {
      if (statuses.contains(request.status)) return request;
    }
    return null;
  }
}

/// "2026-08-24" reads as "Today" when it is, otherwise as the plain date the
/// reference shows in a .kv.
String _dayLabel(String isoDate) {
  final parsed = DateTime.tryParse(isoDate);
  if (parsed == null) return isoDate;
  final now = DateTime.now();
  if (parsed.year == now.year &&
      parsed.month == now.month &&
      parsed.day == now.day) {
    return 'Today';
  }
  const months = [
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
    'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'
  ];
  return '${parsed.day} ${months[parsed.month - 1]}';
}

String _timeRange(String from, String to) =>
    '${_hhmm(from)} – ${_hhmm(to)}';

String _hhmm(String time) =>
    time.length >= 5 ? time.substring(0, 5) : time;
