import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:mobile/screens/quotation/booking_history_screen.dart';
import 'package:mobile/screens/quotation/quotation_view_screen.dart';
import 'package:mobile/screens/rooms/room_detail_screen.dart';
import 'package:mobile/screens/rooms/room_schedule_screen.dart';
import 'package:mobile/theme/app_theme.dart';

void main() {
  final previewScreens = <Widget>[
    const RoomDetailScreen(previewEnabled: false),
    const RoomScheduleScreen(previewEnabled: false),
    const QuotationViewScreen(previewEnabled: false),
    const BookingHistoryScreen(previewEnabled: false),
  ];

  for (final screen in previewScreens) {
    testWidgets('${screen.runtimeType} fails closed when preview data is off',
        (tester) async {
      await tester.pumpWidget(MaterialApp(home: screen));

      expect(find.text('Not available yet.'), findsOneWidget);
      expect(find.text('B-204'), findsNothing);
      expect(find.text('Whiteboard markers'), findsNothing);
      expect(find.text('Group study'), findsNothing);
    });
  }

  test('theme uses the reference type families and square card geometry', () {
    final theme = buildAppTheme();
    final cardShape = theme.cardTheme.shape! as RoundedRectangleBorder;

    expect(theme.textTheme.bodyMedium!.fontFamily, contains('Barlow'));
    expect(theme.textTheme.titleLarge!.fontFamily, contains('BarlowCondensed'));
    expect(theme.cardTheme.color, Colors.transparent);
    expect(cardShape.borderRadius, BorderRadius.zero);
  });
}
