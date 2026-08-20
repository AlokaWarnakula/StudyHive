import 'package:flutter_test/flutter_test.dart';
import 'package:provider/provider.dart';

import 'package:mobile/app.dart';
import 'package:mobile/state/auth_provider.dart';

void main() {
  testWidgets('unauthenticated app shows the login screen', (tester) async {
    await tester.pumpWidget(
      ChangeNotifierProvider(
        create: (_) => AuthProvider(),
        child: const StudyHiveApp(),
      ),
    );

    expect(find.text('Sign in to StudyHive'), findsOneWidget);
  });
}
