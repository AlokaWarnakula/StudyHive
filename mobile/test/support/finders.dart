import 'dart:ui' show Size;

import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';

/// The reference draws a field's label *above* its box (`.field > label`), so
/// the label text is a sibling of the input rather than a child of it. Every
/// `ShTextField` therefore keys its control `field:<label>`; use this to target
/// one in a test.
Finder field(String label) => find.byKey(ValueKey('field:$label'));

/// The reference frames are 390 x 800; the default 800 x 600 test surface puts
/// a screen's bottom action off-view. Scroll to the target before tapping it —
/// a `ListView` builds its children lazily, so an off-screen action may not be
/// in the tree yet and has to be scrolled into range first.
Future<void> tapAndSettle(WidgetTester tester, Finder finder) async {
  if (finder.evaluate().isEmpty) {
    await tester.scrollUntilVisible(finder, 300);
  }
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

/// Renders the next `pumpWidget` at the reference frame size (390 x 800 at 3x)
/// so a screen is laid out the way it was drawn. Restores the surface on
/// teardown via [addTearDown].
void useReferenceFrame(WidgetTester tester) {
  tester.view.physicalSize = const Size(390 * 3, 800 * 3);
  tester.view.devicePixelRatio = 3;
  addTearDown(() {
    tester.view.resetPhysicalSize();
    tester.view.resetDevicePixelRatio();
  });
}
