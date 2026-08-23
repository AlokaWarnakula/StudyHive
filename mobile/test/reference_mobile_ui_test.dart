import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:mobile/screens/create_request_screen.dart';
import 'package:mobile/screens/login_screen.dart';
import 'package:mobile/screens/quotation/quotation_view_screen.dart';
import 'package:mobile/screens/register_screen.dart';
import 'package:mobile/screens/rooms/browse_rooms_screen.dart';
import 'package:mobile/screens/rooms/checked_in_screen.dart';
import 'package:mobile/screens/rooms/qr_check_in_screen.dart';
import 'package:mobile/screens/rooms/room_detail_screen.dart';
import 'package:mobile/screens/rooms/room_schedule_screen.dart';
import 'package:mobile/state/auth_provider.dart';
import 'package:mobile/theme/app_theme.dart';
import 'package:mobile/widgets/studyhive_ui.dart';

import 'support/finders.dart';

/// Each frame of UI/StudyHive Mobile UI (offline).html, checked for the parts
/// the reference actually draws — its controls, its wording and the reference
/// component it is built from.
void main() {
  Widget host(Widget screen) =>
      MaterialApp(theme: buildAppTheme(), home: screen);

  testWidgets('M-01 sign in reaches the M-02 create-account form',
      (tester) async {
    useReferenceFrame(tester);
    await tester.pumpWidget(
      ChangeNotifierProvider(
        create: (_) => AuthProvider(),
        child: host(const LoginScreen()),
      ),
    );

    // M-01: one primary action, the rest secondary.
    expect(find.text('Book a study room in a few taps.'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Sign in'), findsOneWidget);
    expect(find.text('Forgot password'), findsOneWidget);

    await tapAndSettle(tester, find.text('Create an account'));

    expect(find.byType(RegisterScreen), findsOneWidget);
    expect(field('Full name'), findsOneWidget);
    expect(field('University email'), findsOneWidget);
    expect(field('Password'), findsOneWidget);
    expect(find.text('Faculty of Computing'), findsOneWidget);
    // Year of study is a segmented control in the reference, not a dropdown.
    expect(find.byType(Segmented<int>), findsOneWidget);
    expect(find.text('At least 8 characters, one number.'), findsOneWidget);
  });

  testWidgets('M-04 through M-06 form a three-step booking flow',
      (tester) async {
    useReferenceFrame(tester);
    await tester.pumpWidget(host(const CreateRequestScreen()));

    expect(find.text('STEP 1 OF 3 · WHAT AND WHEN'), findsOneWidget);
    expect(find.byType(StepperBar), findsOneWidget);
    await tester.enterText(
        field('What do you need the room for?'), 'Presentation practice');
    await tapAndSettle(tester, find.text('Next: add items'));

    expect(find.text('STEP 2 OF 3 · OPTIONAL'), findsOneWidget);
    expect(find.text('Whiteboard markers'), findsOneWidget);
    expect(find.text('Out of stock'), findsOneWidget);
    expect(find.text('Items subtotal'), findsOneWidget);
    expect(find.text('Skip, I need no items'), findsOneWidget);
    await tapAndSettle(tester, find.text('Next: review'));

    expect(find.text('STEP 3 OF 3 · CHECK AND SEND'), findsOneWidget);
    expect(find.text('Presentation practice'), findsOneWidget);
    expect(find.text('Send request'), findsOneWidget);
  });

  testWidgets('M-08 quotation shows the cost breakdown and budget tile',
      (tester) async {
    useReferenceFrame(tester);
    await tester.pumpWidget(host(const QuotationViewScreen()));

    expect(find.text('Cost breakdown'), findsOneWidget);
    expect(find.text('Waiting for librarian'), findsOneWidget);
    expect(find.text('Total'), findsOneWidget);
    expect(find.text('Rs. 720'), findsWidgets);
    expect(find.text('Within budget by'), findsOneWidget);
    expect(find.text('Rs. 280'), findsOneWidget);
    expect(find.text('Nothing is charged until the librarian approves.'),
        findsOneWidget);
  });

  testWidgets('M-09 browse rooms lists the reference rooms with their tags',
      (tester) async {
    useReferenceFrame(tester);
    await tester.pumpWidget(host(const BrowseRoomsScreen()));

    expect(find.text('B-204'), findsOneWidget);
    expect(find.text('B-118'), findsOneWidget);
    expect(find.text('C-301'), findsOneWidget);
    expect(find.text('Free now'), findsOneWidget);
    expect(find.text('Busy until 3 PM'), findsOneWidget);
    expect(find.text('Maintenance today'), findsOneWidget);
    // Filter chips only — the reference has no advanced search form.
    expect(find.byType(FilterTags), findsOneWidget);
  });

  testWidgets('M-10 room detail and M-11 free times carry the reference facts',
      (tester) async {
    useReferenceFrame(tester);
    await tester.pumpWidget(host(const RoomDetailScreen()));

    expect(find.text('Room B-204'), findsOneWidget);
    expect(find.text('6 people'), findsOneWidget);
    expect(find.text('Rs. 150 per hour'), findsOneWidget);
    expect(find.text('8 AM – 8 PM'), findsOneWidget);
    expect(find.text('Under repair'), findsOneWidget);

    await tapAndSettle(tester, find.text('See free times'));

    expect(find.byType(RoomScheduleScreen), findsOneWidget);
    expect(find.text('Your pick'), findsOneWidget);
    expect(find.text('Booked'), findsOneWidget);
    expect(find.text('Maintenance'), findsOneWidget);
    expect(find.text('Use 2:00 – 4:00 PM'), findsOneWidget);
  });

  testWidgets('M-14 check-in leads to the M-15 success state', (tester) async {
    useReferenceFrame(tester);
    await tester.pumpWidget(host(const QrCheckInScreen()));
    await tester.pumpAndSettle();

    expect(find.text('Point at the sticker on the door'), findsOneWidget);
    expect(find.text('Enter room code instead'), findsOneWidget);

    await tapAndSettle(tester, find.textContaining('CAMERA VIEW'));

    expect(find.byType(CheckedInScreen), findsOneWidget);
    expect(find.text('You are checked in'), findsOneWidget);
    expect(find.text('Store counter, ground floor'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Done'), findsOneWidget);
  });

  test('tags carry only the three reference tones', () {
    // The reference palette has no red/green/amber; status reads as
    // accent (affirmative), outline (in flight) or neutral (settled).
    expect(ShTag.toneFor('Approved'), TagTone.accent);
    expect(ShTag.toneFor('Free now'), TagTone.accent);
    expect(ShTag.toneFor('Active'), TagTone.accent);
    expect(ShTag.toneFor('PendingApproval'), TagTone.outline);
    expect(ShTag.toneFor('Your pick'), TagTone.outline);
    expect(ShTag.toneFor('Completed'), TagTone.neutral);
    expect(ShTag.toneFor('Rejected'), TagTone.neutral);
    expect(ShTag.toneFor('Booked'), TagTone.neutral);
  });
}
