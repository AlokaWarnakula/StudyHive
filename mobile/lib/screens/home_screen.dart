import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../models/booking_request.dart';
import '../state/auth_provider.dart';
import '../state/booking_requests_provider.dart';
import '../state/profile_provider.dart';
import '../theme/app_theme.dart';
import '../widgets/studyhive_ui.dart';
import 'booking_detail_screen.dart';
import 'create_request_screen.dart';
import 'profile_screen.dart';
import 'rooms/browse_rooms_screen.dart';
import 'rooms/qr_check_in_screen.dart';
import 'track_screen.dart';

/// The four-tab shell. Each tab draws its own .mtop bar; the .mnav strip is flat
/// with a hairline top border, not Material's pill-indicator NavigationBar.
class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  int _index = 0;

  void _openBookingFlow() {
    Navigator.of(context)
        .push(MaterialPageRoute(builder: (_) => const CreateRequestScreen()));
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
          IconButton(
            tooltip: 'Notifications',
            onPressed: () {},
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
      actions: [
        if (_index == 1)
          IconButton(
            tooltip: 'Search rooms',
            onPressed: () {},
            icon: const Icon(Icons.search, size: 22),
          ),
        const SizedBox(width: 6),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final screens = [
      _HomeDashboard(
          onBookRoom: _openBookingFlow,
          onOpenBookings: () => setState(() => _index = 2)),
      const BrowseRoomsScreen(embedded: true),
      const TrackScreen(),
      const ProfileScreen(),
    ];

    return Scaffold(
      appBar: _appBar(),
      body: IndexedStack(index: _index, children: screens),
      bottomNavigationBar: BottomNav(
        index: _index,
        onChanged: (i) => setState(() => _index = i),
      ),
    );
  }
}

/// M-03 "Home" — GET /api/booking-requests?mine=true and the weekly eligibility
/// allowance.
class _HomeDashboard extends StatefulWidget {
  final VoidCallback onBookRoom;
  final VoidCallback onOpenBookings;

  const _HomeDashboard(
      {required this.onBookRoom, required this.onOpenBookings});

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
    final profile = context.watch<ProfileProvider>().profile;
    final requests = bookings.requests;
    final next = _firstMatching(requests, const {'Approved'});
    final waiting = _firstMatching(requests, const {
      'Submitted',
      'Processing',
      'PendingApproval',
      'RevisionRequested'
    });
    final used = requests
        .where((r) => !{'Draft', 'Rejected', 'Completed', 'Cancelled', 'Failed'}
            .contains(r.status))
        .length;
    final limit = profile?.maxBookingsPerWeek ?? 3;

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
                Kv(_dayLabel(next.preferredDateFrom),
                    _timeRange(next.preferredTimeFrom, next.preferredTimeTo)),
                Row(
                  children: [
                    ShTag.forStatus(next.status),
                    const SizedBox(width: 8),
                    FNote('Group of ${next.groupSize}'),
                  ],
                ),
                SecondaryButton(
                  'Check in with QR',
                  icon: Icons.qr_code_2,
                  onPressed: () => Navigator.of(context).push(
                    MaterialPageRoute(
                        builder: (_) => QrCheckInScreen(bookingId: next.id)),
                  ),
                ),
              ],
            ),
          Tile(
            children: [
              Kv('Bookings this week', '$used of $limit used'),
              Meter(percent: limit == 0 ? 0 : used / limit),
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
              onTap: () => Navigator.of(context).push(
                MaterialPageRoute(
                    builder: (_) => BookingDetailScreen(requestId: waiting.id)),
              ),
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
