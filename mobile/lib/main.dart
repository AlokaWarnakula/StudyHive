import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import 'api/booking_requests_api.dart';
import 'api/student_profiles_api.dart';
import 'app.dart';
import 'state/auth_provider.dart';
import 'state/booking_requests_provider.dart';
import 'state/profile_provider.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final authProvider = AuthProvider();
  await authProvider.tryRestoreSession();

  runApp(
    MultiProvider(
      providers: [
        ChangeNotifierProvider.value(value: authProvider),
        // Both share authProvider's single ApiClient instance, so they always send whichever
        // access token is currently active without needing to be recreated on login/logout.
        ChangeNotifierProvider(
            create: (_) =>
                ProfileProvider(StudentProfilesApi(authProvider.apiClient))),
        ChangeNotifierProvider(
            create: (_) => BookingRequestsProvider(
                BookingRequestsApi(authProvider.apiClient))),
      ],
      child: const StudyHiveApp(),
    ),
  );
}
