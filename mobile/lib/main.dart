import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'app.dart';
import 'state/auth_provider.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final authProvider = AuthProvider();
  await authProvider.tryRestoreSession();

  runApp(
    ChangeNotifierProvider.value(
      value: authProvider,
      child: const StudyHiveApp(),
    ),
  );
}
