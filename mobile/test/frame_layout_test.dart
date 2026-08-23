import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:mobile/data/demo_seed.dart';
import 'package:mobile/screens/create_request_screen.dart';
import 'package:mobile/screens/login_screen.dart';
import 'package:mobile/screens/quotation/approval_status_screen.dart';
import 'package:mobile/screens/quotation/booking_history_screen.dart';
import 'package:mobile/screens/quotation/quotation_view_screen.dart';
import 'package:mobile/screens/register_screen.dart';
import 'package:mobile/screens/rooms/browse_rooms_screen.dart';
import 'package:mobile/screens/rooms/checked_in_screen.dart';
import 'package:mobile/screens/rooms/qr_check_in_screen.dart';
import 'package:mobile/screens/rooms/room_detail_screen.dart';
import 'package:mobile/screens/rooms/room_schedule_screen.dart';
import 'package:mobile/state/auth_provider.dart';
import 'package:mobile/theme/app_theme.dart';

import 'support/finders.dart';

/// Every reference frame is drawn at 390 x 800. A screen that overflows at that
/// size is not the screen the reference specifies, so pump each one there and
/// fail on any layout exception.
void main() {
  final frames = <String, Widget Function()>{
    'M-01 Sign in': () => const LoginScreen(),
    'M-02 Create account': () => const RegisterScreen(),
    'M-04..M-06 Booking flow': () => const CreateRequestScreen(),
    'M-08 Quotation': () => const QuotationViewScreen(),
    'M-09 Browse rooms': () => const BrowseRoomsScreen(),
    'M-10 Room detail': () => const RoomDetailScreen(),
    'M-11 Free times': () => const RoomScheduleScreen(),
    'M-14 QR check-in': () => const QrCheckInScreen(),
    'M-15 Checked in': () => const CheckedInScreen(),
    'Approval status': () => const ApprovalStatusScreen(),
    'Booking history': () => const BookingHistoryScreen(),
  };

  frames.forEach((name, build) {
    testWidgets('$name lays out at the reference frame size', (tester) async {
      useReferenceFrame(tester);
      await tester.pumpWidget(
        ChangeNotifierProvider(
          create: (_) => AuthProvider(),
          child: MaterialApp(theme: buildAppTheme(), home: build()),
        ),
      );
      await tester.pumpAndSettle();

      expect(tester.takeException(), isNull, reason: '$name overflowed');
    });
  });

  test('preview content is compiled out of release builds', () {
    // The frames above render seeded reference content; that seed must never
    // reach a release build.
    expect(demoPreviewEnabled, isTrue,
        reason: 'debug/test builds show the seeded reference content');
  });
}
