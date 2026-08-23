import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:mobile/screens/consumables/consumable_detail_screen.dart';
import 'package:mobile/screens/rooms/browse_rooms_screen.dart';
import 'package:mobile/screens/rooms/room_detail_screen.dart';
import 'package:mobile/screens/rooms/room_schedule_screen.dart';
import 'package:mobile/widgets/shells/detail_shell.dart';
import 'package:mobile/widgets/shells/list_shell.dart';
import 'package:mobile/widgets/shells/state_view.dart';

Future<void> _pump(WidgetTester tester, Widget child) =>
    tester.pumpWidget(MaterialApp(home: child));

void main() {
  group('StateView', () {
    testWidgets('shows the unavailable banner with the given reason',
        (tester) async {
      await _pump(
          tester,
          const StateView(
              status: ScreenStatus.unavailable,
              unavailableMessage: 'Not built yet.'));
      expect(find.text('Not available yet.'), findsOneWidget);
      expect(find.text('Not built yet.'), findsOneWidget);
    });

    testWidgets('shows a custom empty message', (tester) async {
      await _pump(
          tester,
          const StateView(
              status: ScreenStatus.empty, emptyMessage: 'No rooms yet.'));
      expect(find.text('No rooms yet.'), findsOneWidget);
    });

    testWidgets('shows a loading spinner', (tester) async {
      await _pump(tester, const StateView(status: ScreenStatus.loading));
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
    });

    testWidgets('shows an error message', (tester) async {
      await _pump(
          tester,
          const StateView(
              status: ScreenStatus.error, errorMessage: 'Failed to load.'));
      expect(find.text('Failed to load.'), findsOneWidget);
    });
  });

  group('ListShell / DetailShell', () {
    testWidgets(
        'ListShell renders its title, owner badge and column chips, never fake rows',
        (tester) async {
      await _pump(
        tester,
        const ListShell(
          owner: 'S2',
          title: 'Rooms',
          columns: ['Name', 'Capacity'],
          unavailableMessage: 'Rooms API not built yet.',
        ),
      );

      expect(find.text('Rooms'),
          findsWidgets); // appears in both the AppBar and the body heading
      expect(find.text('S2'), findsOneWidget);
      expect(find.text('Name'), findsOneWidget);
      expect(find.text('Capacity'), findsOneWidget);
      expect(find.text('Not available yet.'), findsOneWidget);
    });

    testWidgets(
        'DetailShell renders field labels with an em-dash placeholder, never a fabricated value',
        (tester) async {
      await _pump(
        tester,
        const DetailShell(
            owner: 'S3', title: 'Consumable', fields: ['Name', 'Unit price']),
      );

      expect(find.text('Name'), findsOneWidget);
      expect(find.text('Unit price'), findsOneWidget);
      expect(find.text('—'), findsNWidgets(2));
    });
  });

  group('S2 screen hierarchy', () {
    testWidgets(
        'Browse rooms -> Room detail -> Room schedule is a real, navigable chain',
        (tester) async {
      await _pump(tester, const BrowseRoomsScreen());
      expect(find.text('Rooms'), findsOneWidget);
      expect(find.text('B-204'), findsOneWidget);

      await tester.tap(find.text('B-204'));
      await tester.pumpAndSettle();
      expect(find.byType(RoomDetailScreen), findsOneWidget);

      await tester.scrollUntilVisible(find.text('See free times'), 240);
      await tester.drag(find.byType(ListView), const Offset(0, -120));
      await tester.pumpAndSettle();
      await tester.tap(find.text('See free times'));
      await tester.pumpAndSettle();
      expect(find.byType(RoomScheduleScreen), findsOneWidget);
      expect(find.text('B-204 · free times'), findsOneWidget);
    });
  });

  group('S3 screen hierarchy', () {
    testWidgets('Consumable detail previews the add-to-request screen',
        (tester) async {
      await _pump(tester, const ConsumableDetailScreen());

      await tester.tap(find.text('Preview: add to a request →'));
      await tester.pumpAndSettle();

      expect(find.text('Add consumables'), findsWidgets);
    });
  });
}
