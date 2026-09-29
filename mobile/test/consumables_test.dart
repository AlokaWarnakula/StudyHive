import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:provider/provider.dart';

import 'package:mobile/models/consumable.dart';
import 'package:mobile/screens/consumables/browse_consumables_screen.dart';
import 'package:mobile/screens/consumables/consumable_detail_screen.dart';
import 'package:mobile/screens/consumables/select_consumables_screen.dart';
import 'package:mobile/state/consumables_provider.dart';
import 'package:mobile/theme/app_theme.dart';

import 'support/consumables.dart';
import 'support/finders.dart';

Future<void> _pump(
    WidgetTester tester, ConsumablesProvider provider, Widget screen) async {
  await tester.pumpWidget(
    ChangeNotifierProvider.value(
      value: provider,
      child: MaterialApp(theme: buildAppTheme(), home: screen),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  group('ConsumablesApi', () {
    test('lists the catalogue by name with an encoded search', () async {
      late Uri requested;
      final provider = consumablesProviderFor((request) async {
        requested = request.url;
        return consumablesRoute(request)!;
      });

      await provider.refresh(search: 'white board');

      expect(requested.path, '/api/consumables');
      expect(requested.queryParameters['search'], 'white board');
      expect(requested.queryParameters['sortBy'], 'name');
      expect(requested.queryParameters['pageSize'], '100');
      expect(provider.error, isNull);
    });

    test('reads the detail from the consumable envelope', () async {
      final provider = consumablesProviderFor();

      await provider.select('c-markers');

      final item = provider.selected!;
      expect(item.name, 'Whiteboard markers');
      expect(item.unitPrice, 60);
      expect(item.availableQuantity, 3, reason: '5 on hand, 2 already reserved');
      expect(item.description, 'Black and blue dry-erase markers.');
    });

    test('an API failure becomes the provider error, not a crash', () async {
      final provider = consumablesProviderFor((request) async =>
          http.Response('{"title":"Server error","detail":"Boom"}', 500,
              headers: {'content-type': 'application/json'}));

      await provider.refresh();

      expect(provider.items, isEmpty);
      expect(provider.error, 'Boom');
    });
  });

  test('stock status follows what is still free to reserve', () {
    ConsumableListItem item(int available, int min) => ConsumableListItem(
        id: 'x', name: 'x', unit: 'pcs', unitPrice: 1,
        availableQuantity: available, minStockLevel: min, isActive: true);

    expect(item(0, 5).stockLabel, 'Out of stock');
    expect(item(5, 5).stockLabel, 'Low stock');
    expect(item(6, 5).stockLabel, 'In stock');
  });

  testWidgets('browse lists live items with stock status and opens a detail',
      (tester) async {
    useReferenceFrame(tester);
    final provider = consumablesProviderFor();
    await _pump(tester, provider, const BrowseConsumablesScreen());

    expect(find.text('A4 printouts'), findsOneWidget);
    expect(find.text('HDMI cable'), findsOneWidget);
    expect(find.text('Out of stock'), findsOneWidget);
    expect(find.text('In stock'), findsWidgets);

    await tapAndSettle(tester, find.text('Whiteboard markers'));

    expect(find.byType(ConsumableDetailScreen), findsOneWidget);
    expect(find.text('Rs. 60 per marker'), findsOneWidget);
    expect(find.text('Black and blue dry-erase markers.'), findsOneWidget);
  });

  testWidgets('the picker caps a quantity at what is available and totals it',
      (tester) async {
    useReferenceFrame(tester);
    final provider = consumablesProviderFor();
    await _pump(tester, provider, const SelectConsumablesScreen());

    final markers = find.byKey(const ValueKey('pick:c-markers'));
    final plus = find.descendant(of: markers, matching: find.text('+'));
    for (var i = 0; i < 5; i++) {
      await tapAndSettle(tester, plus);
    }

    // 3 are free to reserve (5 on hand, 2 held), so the counter stops there.
    expect(provider.selection, {'c-markers': 3});
    expect(find.text('Rs. 180'), findsOneWidget);

    // An out-of-stock item has no counter at all.
    final hdmi = find.byKey(const ValueKey('pick:c-hdmi'));
    expect(find.descendant(of: hdmi, matching: find.text('+')), findsNothing);

    // Back to zero drops the line rather than sending a 0-quantity item.
    final minus = find.descendant(of: markers, matching: find.text('−'));
    for (var i = 0; i < 3; i++) {
      await tapAndSettle(tester, minus);
    }
    expect(provider.selection, isEmpty);
  });
}
